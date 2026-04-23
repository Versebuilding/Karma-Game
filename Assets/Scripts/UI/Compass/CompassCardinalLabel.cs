using UnityEngine;

namespace Karma.UI.Compass
{
    /// <summary>
    /// A single cardinal label (N / E / S / W) on the compass bar.
    /// Driven by the CompassHUDController each tick: given a world yaw and the
    /// current camera yaw, the controller computes the anchored X and applies it.
    ///
    /// This component is intentionally data-only — it owns the RectTransform
    /// and the world yaw it represents. No Update() of its own.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class CompassCardinalLabel : MonoBehaviour
    {
        [Tooltip("World yaw this label represents, in degrees. N=0, E=90, S=180, W=270.")]
        public float worldYaw;

        [System.NonSerialized] public RectTransform cachedRect;
        [System.NonSerialized] public CanvasGroup cachedGroup;

        void Awake()
        {
            cachedRect = (RectTransform)transform;
            cachedGroup = GetComponent<CanvasGroup>();
        }
    }
}
