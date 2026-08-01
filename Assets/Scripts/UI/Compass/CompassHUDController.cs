using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Karma.UI.Compass
{
    /// <summary>
    /// Skyrim-style top-center compass. Tracks all active CompassMarker
    /// components, maps each into camera-yaw space, and places a pooled
    /// icon on a horizontal bar. Markers outside the FOV window clamp to
    /// the nearest edge with a directional arrow.
    ///
    /// Runs at 25 Hz via a coroutine (not per-frame Update) to stay under
    /// the mobile performance budget. Zero allocations per tick.
    /// </summary>
    [DisallowMultipleComponent]
    public class CompassHUDController : MonoBehaviour
    {
        // ─── Per-Type Icon Mapping ──────────────────────────────────

        [System.Serializable]
        public struct MarkerTypeSprite
        {
            public CompassMarkerType type;
            public Sprite sprite;
            public Color tint;
        }

        [Header("Icon Atlas (assign a sprite per type)")]
        [SerializeField] private MarkerTypeSprite[] defaults;
        [SerializeField] private Sprite edgeArrowLeft;
        [SerializeField] private Sprite edgeArrowRight;

        // ─── Layout ─────────────────────────────────────────────────

        [Header("Layout")]
        [Tooltip("Parent RectTransform for pooled marker icons. Its width sets the bar extent.")]
        [SerializeField] private RectTransform markerRoot;

        [Tooltip("Prefab for a pooled marker icon (RectTransform + Image + CanvasGroup).")]
        [SerializeField] private GameObject markerIconPrefab;

        [Tooltip("Number of pooled icon slots. More than the expected max concurrent markers.")]
        [SerializeField] private int iconPoolSize = 16;

        [Tooltip("Cardinal labels (N/E/S/W). Any count is fine.")]
        [SerializeField] private CompassCardinalLabel[] cardinalLabels;

        [Tooltip("Total angular window mapped across the bar width, in degrees. 180 = half-sphere visible at once.")]
        [SerializeField] private float compassFovDegrees = 180f;

        [Tooltip("Visual arc amplitude in px (subtle downward curve at the edges).")]
        [SerializeField] private float arcAmplitude = 8f;

        // ─── Visibility / Distance ──────────────────────────────────

        [Header("Visibility")]
        [Tooltip("Default max detection range in meters. Per-marker overrides take priority.")]
        [SerializeField] private float defaultMaxRange = 150f;

        [Tooltip("Alpha fade band at the outer edge of the range, in meters.")]
        [SerializeField] private float fadeBand = 25f;

        [Tooltip("Update tick frequency in Hz. 20-30 is a good mobile range.")]
        [SerializeField] private float updateHz = 25f;

        // ─── Camera Source ──────────────────────────────────────────

        [Header("Camera")]
        [Tooltip("Optional explicit camera transform. Leave empty to auto-find Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        // ─── Runtime State ──────────────────────────────────────────

        private CompassIconPool _pool;
        private float _halfBarWidth;
        private float _halfFov;
        private float _maxRangeSqr;
        private bool _dialogueActive;
        private readonly Dictionary<CompassMarkerType, (Sprite sprite, Color tint)> _lookup
            = new Dictionary<CompassMarkerType, (Sprite, Color)>();
        private readonly List<CompassMarker> _sorted = new List<CompassMarker>(64);
        private WaitForSeconds _wait;
        private Coroutine _tickRoutine;

        // ─── Unity Lifecycle ────────────────────────────────────────

        void Awake()
        {
            BuildLookup();
            if (markerRoot == null) markerRoot = (RectTransform)transform;
            _halfBarWidth = markerRoot.rect.width * 0.5f;
            _halfFov = compassFovDegrees * 0.5f;
            _maxRangeSqr = defaultMaxRange * defaultMaxRange;

            if (markerIconPrefab != null)
                _pool = new CompassIconPool(markerRoot, markerIconPrefab, iconPoolSize);
            else
                Debug.LogError("CompassHUDController: markerIconPrefab is not assigned.");

            TryCacheCamera();
            _wait = new WaitForSeconds(1f / Mathf.Max(1f, updateHz));
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySubscribeDialogue();
            _tickRoutine = StartCoroutine(TickLoop());
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeDialogue();
            if (_tickRoutine != null) StopCoroutine(_tickRoutine);
            _tickRoutine = null;
            if (_pool != null)
            {
                _pool.BeginFrame();
                _pool.HideUnused();
            }
        }

        void OnRectTransformDimensionsChange()
        {
            if (markerRoot != null)
                _halfBarWidth = markerRoot.rect.width * 0.5f;
        }

        // ─── Tick Loop ──────────────────────────────────────────────

        private IEnumerator TickLoop()
        {
            while (true)
            {
                if (cameraTransform == null) TryCacheCamera();
                if (cameraTransform != null && _pool != null) UpdateCompass();
                yield return _wait;
            }
        }

        private void UpdateCompass()
        {
            _pool.BeginFrame();

            float camYaw = cameraTransform.eulerAngles.y;
            Vector3 camPos = cameraTransform.position;

            UpdateCardinals(camYaw);

            var markers = CompassService.AllMarkers;
            int n = markers.Count;
            if (n == 0) { _pool.HideUnused(); return; }

            // Copy to local list for stable sort (zero alloc when capacity is sufficient).
            _sorted.Clear();
            for (int i = 0; i < n; i++)
            {
                var m = markers[i];
                if (m == null || !m.isActiveAndEnabled) continue;
                if (_dialogueActive && m.hideDuringDialogue) continue;
                _sorted.Add(m);
            }
            // Lower priority first so higher priority draws on top (later = top in UGUI sibling order).
            _sorted.Sort(CompareByPriority);

            int count = _sorted.Count;
            for (int i = 0; i < count; i++)
            {
                var m = _sorted[i];
                var mt = m.cachedTransform != null ? m.cachedTransform : m.transform;

                Vector3 to = mt.position - camPos;
                to.y = 0f;
                float sqr = to.x * to.x + to.z * to.z;

                float maxRange = m.maxRangeOverride > 0f ? m.maxRangeOverride : defaultMaxRange;
                float maxSqr = maxRange * maxRange;
                if (sqr > maxSqr) continue;
                if (m.minRangeToShow > 0f && sqr < m.minRangeToShow * m.minRangeToShow) continue;

                float dist = Mathf.Sqrt(sqr);
                float alpha = fadeBand > 0f ? Mathf.Clamp01((maxRange - dist) / fadeBand) : 1f;

                float markerYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(camYaw, markerYaw);
                bool offFov = Mathf.Abs(delta) > _halfFov;

                float t = Mathf.Clamp(delta / _halfFov, -1f, 1f);
                float x = t * _halfBarWidth;
                float y = -arcAmplitude * (1f - Mathf.Cos(Mathf.Abs(t) * Mathf.PI * 0.5f));

                Sprite icon;
                Color tint;
                ResolveIcon(m, offFov, delta, out icon, out tint);
                tint.a = 1f; // CanvasGroup drives alpha to keep sprite color separate

                var slot = _pool.Acquire();
                if (slot == null) break;
                slot.Apply(new Vector2(x, y), icon, tint, alpha);
                slot.rect.SetAsLastSibling();
            }

            _pool.HideUnused();
        }

        private void UpdateCardinals(float camYaw)
        {
            if (cardinalLabels == null) return;
            for (int i = 0; i < cardinalLabels.Length; i++)
            {
                var c = cardinalLabels[i];
                if (c == null || c.cachedRect == null) continue;
                float delta = Mathf.DeltaAngle(camYaw, c.worldYaw);
                float t = Mathf.Clamp(delta / _halfFov, -1f, 1f);
                float x = t * _halfBarWidth;
                bool visible = Mathf.Abs(delta) <= _halfFov;
                c.cachedRect.anchoredPosition = new Vector2(x, c.cachedRect.anchoredPosition.y);
                if (c.cachedGroup != null) c.cachedGroup.alpha = visible ? 1f : 0f;
                else if (c.gameObject.activeSelf != visible) c.gameObject.SetActive(visible);
            }
        }

        private void ResolveIcon(CompassMarker m, bool offFov, float delta, out Sprite icon, out Color tint)
        {
            if (offFov)
            {
                icon = delta > 0f ? edgeArrowRight : edgeArrowLeft;
                tint = m.overrideTint.a > 0f ? m.overrideTint : Color.white;
                return;
            }

            if (m.type == CompassMarkerType.CustomIcon && m.overrideSprite != null)
            {
                icon = m.overrideSprite;
                tint = m.overrideTint.a > 0f ? m.overrideTint : Color.white;
                return;
            }

            if (_lookup.TryGetValue(m.type, out var entry))
            {
                icon = m.overrideSprite != null ? m.overrideSprite : entry.sprite;
                tint = m.overrideTint.a > 0f ? m.overrideTint : entry.tint;
                return;
            }

            icon = m.overrideSprite;
            tint = m.overrideTint.a > 0f ? m.overrideTint : Color.white;
        }

        private static int CompareByPriority(CompassMarker a, CompassMarker b)
        {
            return a.sortPriority.CompareTo(b.sortPriority);
        }

        // ─── Dialogue Hook ──────────────────────────────────────────

        private void TrySubscribeDialogue()
        {
            if (DialogueManager.Instance == null) return;
            DialogueManager.Instance.OnDialogueStarted += HandleDialogueStarted;
            DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;
        }

        private void UnsubscribeDialogue()
        {
            if (DialogueManager.Instance == null) return;
            DialogueManager.Instance.OnDialogueStarted -= HandleDialogueStarted;
            DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;
        }

        private void HandleDialogueStarted(DialogueSO _) { _dialogueActive = true; }
        private void HandleDialogueEnded() { _dialogueActive = false; }

        // ─── Camera Ref ─────────────────────────────────────────────

        private void TryCacheCamera()
        {
            if (cameraTransform != null) return;
            var cam = Camera.main;
            if (cam != null) cameraTransform = cam.transform;
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            cameraTransform = null;
            TryCacheCamera();
            TrySubscribeDialogue();
        }

        // ─── Lookup Build ───────────────────────────────────────────

        private void BuildLookup()
        {
            _lookup.Clear();
            if (defaults == null) return;
            for (int i = 0; i < defaults.Length; i++)
                _lookup[defaults[i].type] = (defaults[i].sprite, defaults[i].tint.a > 0f ? defaults[i].tint : Color.white);
        }
    }
}
