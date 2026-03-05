using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Fly-to-target karma popup: when karma changes, shows "+50" in green at screen center,
/// pops in with overshoot, pauses for readability, then flies toward the KarmaFlower HUD
/// element (top-left). On arrival, pulses the flower to signal receipt.
///
/// Three-phase animation:
///   1. Pop-in  (0.2s) — scale 0 → 1.3 → 1.0 at center screen
///   2. Pause   (0.5s) — hold for readability
///   3. Fly     (0.6s) — ease-in flight to KarmaFlower, scale down, then pulse target
///
/// Subscribes to KarmaManager.OnKarmaChanged.
///
/// Setup:
///   1. On HUDCanvas, add a TMP_Text child (starts hidden)
///   2. Attach this script, drag popupText reference
///   3. Set flyTarget to the KarmaFlower RectTransform
///   4. UISetupTool wires all of this automatically
/// </summary>
public class KarmaPopupUI : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────
    [Header("Popup Text")]
    [Tooltip("TMP_Text used for the popup display")]
    [SerializeField] private TMP_Text popupText;

    [Header("Fly Target")]
    [Tooltip("The KarmaFlower RectTransform to fly toward")]
    [SerializeField] private RectTransform flyTarget;

    // ─── Appearance ──────────────────────────────────────────
    [Header("Appearance")]
    [Tooltip("Font size for the popup")]
    [Range(16f, 72f)]
    [SerializeField] private float fontSize = 36f;

    [Tooltip("Color for positive karma changes (green to match bar gain color)")]
    [SerializeField] private Color positiveColor = new Color(0.2f, 0.9f, 0.3f, 1f); // green

    [Tooltip("Color for negative karma changes")]
    [SerializeField] private Color negativeColor = new Color(0.9f, 0.25f, 0.25f, 1f); // red

    // ─── Animation Timing ────────────────────────────────────
    [Header("Pop-In")]
    [Tooltip("Duration of the pop-in scale effect")]
    [Range(0.1f, 0.5f)]
    [SerializeField] private float popInDuration = 0.2f;

    [Header("Pause")]
    [Tooltip("How long the text pauses at center before flying")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float pauseDuration = 0.5f;

    [Header("Flight")]
    [Tooltip("Duration of the flight to target")]
    [Range(0.2f, 1.5f)]
    [SerializeField] private float flyDuration = 0.6f;

    // ─── Runtime ─────────────────────────────────────────────
    private Coroutine popupCoroutine;
    private Coroutine punchCoroutine;
    private RectTransform popupRect;
    private Vector3 startPosition; // screen-space position (overlay canvas)
    private bool isSubscribed;

    // ─── Unity Lifecycle ─────────────────────────────────────

    void Awake()
    {
        if (popupText != null)
        {
            popupRect = popupText.GetComponent<RectTransform>();
            if (popupRect != null)
                startPosition = popupRect.position; // screen-space for overlay

            popupText.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        // Fallback: KarmaManager.Instance may be null during OnEnable
        // (common when HUDCanvas initializes before GameManagers)
        TrySubscribe();
    }

    void OnDisable()
    {
        if (KarmaManager.Instance != null)
            KarmaManager.Instance.OnKarmaChanged -= HandleKarmaChanged;
        isSubscribed = false;
    }

    private void TrySubscribe()
    {
        if (isSubscribed) return;
        if (KarmaManager.Instance == null) return;

        KarmaManager.Instance.OnKarmaChanged += HandleKarmaChanged;
        isSubscribed = true;
    }

    // ─── Event Handler ───────────────────────────────────────

    private void HandleKarmaChanged(int newTotal, int delta)
    {
        if (delta == 0) return;
        ShowPopup(delta);
    }

    // ─── Popup Display ───────────────────────────────────────

    /// <summary>Show a karma change fly-to-target popup.</summary>
    public void ShowPopup(int delta)
    {
        if (popupText == null) return;

        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(FlyPopupCoroutine(delta));
    }

    private IEnumerator FlyPopupCoroutine(int delta)
    {
        // ── Setup text ──
        string prefix = delta > 0 ? "+" : "";
        popupText.text = $"{prefix}{delta}";
        popupText.color = delta > 0 ? positiveColor : negativeColor;
        popupText.fontSize = fontSize;
        popupText.gameObject.SetActive(true);

        // Reset to center screen position
        if (popupRect != null)
            popupRect.position = startPosition;

        popupText.transform.localScale = Vector3.zero;

        // ══════════════════════════════════════════════════════
        // Phase 1: Pop-in (scale 0 → 1.3 → 1.0)
        // ══════════════════════════════════════════════════════
        float elapsed = 0f;
        float overshootEnd = popInDuration * 0.6f;

        // Scale up with overshoot
        while (elapsed < popInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popInDuration);
            float scale;

            if (t < 0.6f)
            {
                // 0 → 1.3 (first 60% of pop-in)
                float subT = t / 0.6f;
                scale = Mathf.Lerp(0f, 1.3f, subT);
            }
            else
            {
                // 1.3 → 1.0 (settle, last 40%)
                float subT = (t - 0.6f) / 0.4f;
                scale = Mathf.Lerp(1.3f, 1f, subT);
            }

            popupText.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        popupText.transform.localScale = Vector3.one;

        // ══════════════════════════════════════════════════════
        // Phase 2: Pause (hold at center for readability)
        // ══════════════════════════════════════════════════════
        yield return new WaitForSeconds(pauseDuration);

        // ══════════════════════════════════════════════════════
        // Phase 3: Fly to target (ease-in, scale down)
        // ══════════════════════════════════════════════════════
        if (flyTarget != null && popupRect != null)
        {
            Vector3 flyStartPos = popupRect.position;
            Vector3 flyEndPos = flyTarget.position;

            elapsed = 0f;
            while (elapsed < flyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flyDuration);

                // Ease-in curve: accelerating toward target (like gravity)
                float eased = t * t;

                // Move toward target
                popupRect.position = Vector3.Lerp(flyStartPos, flyEndPos, eased);

                // Scale down during flight
                float scale = Mathf.Lerp(1f, 0.3f, t);
                popupText.transform.localScale = Vector3.one * scale;

                // Slight alpha fade
                Color c = popupText.color;
                c.a = Mathf.Lerp(1f, 0.6f, t);
                popupText.color = c;

                yield return null;
            }

            // Arrived — pulse the KarmaFlower
            if (punchCoroutine != null) StopCoroutine(punchCoroutine);
            punchCoroutine = StartCoroutine(PunchTargetCoroutine());
        }
        else
        {
            // Fallback: no fly target — just fade out
            elapsed = 0f;
            float fadeDuration = 0.5f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                Color c = popupText.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                popupText.color = c;
                yield return null;
            }
        }

        // Hide and reset
        popupText.gameObject.SetActive(false);
        popupText.transform.localScale = Vector3.one;
        if (popupRect != null)
            popupRect.position = startPosition;

        popupCoroutine = null;
    }

    // ─── Target Punch ────────────────────────────────────────

    /// <summary>Pulse the KarmaFlower to signal the karma arrived.</summary>
    private IEnumerator PunchTargetCoroutine()
    {
        if (flyTarget == null) yield break;

        Vector3 originalScale = flyTarget.localScale;
        float punchAmount = 1.25f;
        float punchDuration = 0.2f;
        float halfDuration = punchDuration * 0.5f;
        float elapsed = 0f;

        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            flyTarget.localScale = originalScale * Mathf.Lerp(1f, punchAmount, t);
            yield return null;
        }

        // Scale back down
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            flyTarget.localScale = originalScale * Mathf.Lerp(punchAmount, 1f, t);
            yield return null;
        }

        flyTarget.localScale = originalScale;
        punchCoroutine = null;
    }
}
