using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Karma.UI.Compass
{
    /// <summary>
    /// A single pooled compass icon slot. Cached references avoid
    /// per-tick GetComponent calls; Apply() is the hot path.
    /// </summary>
    public class CompassIconSlot
    {
        public readonly GameObject gameObject;
        public readonly RectTransform rect;
        public readonly Image image;
        public readonly CanvasGroup canvasGroup;
        public bool inUse;

        public CompassIconSlot(GameObject go)
        {
            gameObject = go;
            rect = go.GetComponent<RectTransform>();
            image = go.GetComponent<Image>();
            canvasGroup = go.GetComponent<CanvasGroup>();
            Hide();
        }

        public void Apply(Vector2 anchoredPos, Sprite sprite, Color tint, float alpha)
        {
            if (!inUse)
            {
                gameObject.SetActive(true);
                inUse = true;
            }
            rect.anchoredPosition = anchoredPos;
            if (image.sprite != sprite) image.sprite = sprite;
            image.color = tint;
            canvasGroup.alpha = alpha;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            inUse = false;
        }
    }

    /// <summary>
    /// Fixed-capacity pool of CompassIconSlot. All slots are instantiated at
    /// startup under a shared parent RectTransform; the HUD controller calls
    /// Acquire() during update and ReleaseUnused() at the end of the tick.
    /// Zero allocation in the steady state.
    /// </summary>
    public class CompassIconPool
    {
        private readonly List<CompassIconSlot> _slots;
        private int _cursor;

        public int Count => _slots.Count;
        public CompassIconSlot this[int i] => _slots[i];

        public CompassIconPool(RectTransform parent, GameObject prefab, int capacity)
        {
            _slots = new List<CompassIconSlot>(capacity);
            for (int i = 0; i < capacity; i++)
            {
                var go = Object.Instantiate(prefab, parent, false);
                go.name = $"CompassIcon_{i:D2}";
                _slots.Add(new CompassIconSlot(go));
            }
        }

        /// <summary>Reset the acquire cursor; call at the start of each update tick.</summary>
        public void BeginFrame() => _cursor = 0;

        /// <summary>Get the next slot. Returns null if the pool is exhausted.</summary>
        public CompassIconSlot Acquire()
        {
            if (_cursor >= _slots.Count) return null;
            return _slots[_cursor++];
        }

        /// <summary>Hide any slots that weren't Acquired this tick.</summary>
        public void HideUnused()
        {
            for (int i = _cursor; i < _slots.Count; i++)
                _slots[i].Hide();
        }
    }
}
