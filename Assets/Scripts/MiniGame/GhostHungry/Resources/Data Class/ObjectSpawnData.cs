using System.Collections.Generic;
using UnityEngine;

/* Implement:
- Optional Rotation data
*/

/// <summary>
/// A data container which is utilized for the instantiation of an identical object group.
/// </summary>
[System.Serializable]
public class ObjectSpawnData {
    [Tooltip("An instantiable object which will be cloned to create all sub-objects")]
    [SerializeField] private GameObject prefab = null;
    [Tooltip("Locations to instantiate the prefab instance")]
    [SerializeField] private List<Vector3> positions = new();

    public GameObject Prefab => prefab;

    /// <remarks>Read-Only Property</remarks>
    public IReadOnlyList<Vector3> Positions => positions;
    // Positions.Count == number of spawned instances : programmatically ensures equivalency between instance count and list length w/o requiring validation
}