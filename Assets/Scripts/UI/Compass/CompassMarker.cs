using UnityEngine;

namespace Karma.UI.Compass
{
    /// <summary>
    /// Attach to any world object you want tracked on the compass HUD
    /// (NPCs, altars, shops, discovery points, quest waypoints).
    /// Self-registers with CompassService on enable; unregisters on disable.
    ///
    /// The HUD does not search the scene — every visible marker must have
    /// this component, and toggling the component enables / disables the
    /// marker on the compass without destroying the GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    public class CompassMarker : MonoBehaviour
    {
        [Header("Type & Icon")]
        [Tooltip("Marker category. Controls default icon + priority.")]
        public CompassMarkerType type = CompassMarkerType.NPC;

        [Tooltip("Override sprite. Used only when type == CustomIcon, or to replace the default for this specific marker.")]
        public Sprite overrideSprite;

        [Tooltip("Override tint. Used if non-black; otherwise the HUD default tint for this type is used.")]
        public Color overrideTint = Color.clear;

        [Header("Visibility")]
        [Tooltip("Marker is hidden while the player is closer than this (meters). 0 = always show.")]
        public float minRangeToShow = 0f;

        [Tooltip("Optional per-marker max range override. 0 = use HUD default.")]
        public float maxRangeOverride = 0f;

        [Tooltip("Hide this marker while a dialogue is active (e.g., NPC markers).")]
        public bool hideDuringDialogue = false;

        [Tooltip("Render priority. Higher = drawn on top. PrimaryQuest auto-uses 100.")]
        public int sortPriority = 0;

        /// <summary>Cached transform, avoids per-tick property access.</summary>
        [System.NonSerialized] public Transform cachedTransform;

        void Awake()
        {
            cachedTransform = transform;
            if (type == CompassMarkerType.PrimaryQuest && sortPriority == 0)
                sortPriority = 100;
        }

        void OnEnable()
        {
            CompassService.Register(this);
        }

        void OnDisable()
        {
            CompassService.Unregister(this);
        }
    }
}
