using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// A <see cref="PhaseSO"/> object which denotes dynamic spawning capabilities for the given phase state. This object should be 
/// utilized in conjunction with the <see cref="DynamicObjectSpawner"/> system to instantiate phase-specific objects.
/// </summary>
[CreateAssetMenu(fileName = "NewSpawnerPhase", menuName = "Karma/Phase/Spawner Phase", order = 1)]
public class SpawnerPhaseSO : PhaseSO {
    [Tooltip("The instantiation data for all objects spawned during this phase")]
    [SerializeField] private List<ObjectSpawnData> objectData = new();

    /// <summary>
    /// The read-only instantiation data for all objects spawned during this phase
    /// </summary>
    public ReadOnlyCollection<ObjectSpawnData> ObjectData => objectData.AsReadOnly(); // FIX?: if used often, store the wrapper on runtime due to inefficiency
	/* ReadOnlyCollection FIX Imp.
    private readonly ReadOnlyCollection<string> _readOnlyWrapper;
    _readOnlyWrapper = Array.AsReadOnly(_internalArray);
    public ReadOnlyCollection<string> A => _readOnlyWrapper;
    */


#if UNITY_EDITOR
	// Editor-Time Validation:
	private void OnValidate() {
        // ensure all entityData have prefabs assigned
        for (int i = 0; i < objectData.Count; i++) {
            if (objectData[i].Prefab == null) {
                Debug.LogError($"{GetType()} '{name}': {nameof(objectData)}[{i}] has no prefab assigned...");
                continue;
            }
        }
    }
#endif
}