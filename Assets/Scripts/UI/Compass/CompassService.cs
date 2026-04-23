using System.Collections.Generic;

namespace Karma.UI.Compass
{
    /// <summary>
    /// Static registry of active compass markers. CompassMarker components
    /// call Register / Unregister on enable / disable. The CompassHUDController
    /// reads AllMarkers each update tick.
    ///
    /// Not a MonoBehaviour — survives scene loads and has no GameObject cost.
    /// A HashSet guards against double-registration; the parallel List keeps
    /// iteration cheap (no enumerator allocation per tick).
    /// </summary>
    public static class CompassService
    {
        private static readonly List<CompassMarker> _markers = new List<CompassMarker>(64);
        private static readonly HashSet<CompassMarker> _set = new HashSet<CompassMarker>();

        public static IReadOnlyList<CompassMarker> AllMarkers => _markers;

        public static void Register(CompassMarker m)
        {
            if (m == null) return;
            if (_set.Add(m)) _markers.Add(m);
        }

        public static void Unregister(CompassMarker m)
        {
            if (m == null) return;
            if (_set.Remove(m)) _markers.Remove(m);
        }

        /// <summary>Clear all registrations (test / scene-reset hook).</summary>
        public static void ClearAll()
        {
            _markers.Clear();
            _set.Clear();
        }
    }
}
