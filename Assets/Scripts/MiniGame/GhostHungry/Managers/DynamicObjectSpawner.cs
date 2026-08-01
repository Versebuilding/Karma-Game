using System.Collections.Generic;
using UnityEngine;

// FiX: Too much inter-phase overhead : replicated object inter-phase persistence _OR_ total phase spawning & staging
// FiX: System Too dependent on Phase System, needs to be broken away : Extension Builder
// FIX: allow between phase staying

/// <summary>
/// A lifecycle manager for a collection of predetermined objects which must be instantiated and destroyed dynamically during the program's runtime
/// </summary>
/// <remarks>
/// <b>Primary Use:</b> <see cref="PhaseManager"/> system
/// </remarks>
public class DynamicObjectSpawner : MonoBehaviour {
    // Variables:
    [Tooltip("Where to store/instantiate spawned objects within the game's hierarchy (null = use itself as the parent)")]
    [SerializeField] private GameObject spawnParent;

    // the instances currently spawned from this manager (used for efficient tracking)
    private List<GameObject> instances = new();


    // Unity Processing:
    private void Awake() {
        PhaseManager.PhaseChange.AddListener(PhaseChange);
        // PhaseChange is guaranteed to exist at runtime start
    }

    private void OnDestroy() {
        PhaseManager.PhaseChange.RemoveListener(PhaseChange); // prevents memory leaks
    }


    // Data Manipulation:
    private void PhaseChange(PhaseSO phaseSO) {
        ClearSpawnedObjects();

        // Build New Objects:
        if (phaseSO != null && phaseSO is SpawnerPhaseSO spawnerPhaseSO) { // -phase exists and is correct type?> cast to use
            GameObject parent = (spawnParent != null) ? spawnParent : gameObject; // instantiate under spawnParent, if set, or this object otherwise

            // Data Iteration: (List[object group -> position])
            foreach (ObjectSpawnData spawnData in spawnerPhaseSO.ObjectData) {
                foreach (Vector3 position in spawnData.Positions) {
                    GameObject instance = Instantiate(spawnData.Prefab, position, Quaternion.identity, parent.transform);
                    // Quaternion.identity : no rotation

                    if (instance.TryGetComponent(out ISpawnInitializable component)) {
                        component.InitializeSpawnedInstance(phaseSO);
                    }

                    instances.Add(instance);
                    instance.SetActive(true);
                    // ensures the instance attempts activation rather than silently existing
                }
            }
        }
    }

    private void ClearSpawnedObjects() {
        // Destroy Each Object: (List Size = n, List[1...n] = null - Empty by theory)
        for (int i = instances.Count - 1; i >= 0; i--) {
            if (instances[i] != null) {
                Destroy(instances[i]);
            }
        }
        // explicit deallocation of each object will prevent orphaned objects/memory leaks

        // Resize List: (List Size = 0 - True Empty)
        instances.Clear();
    }
}

/* Future Build Ideas:
- List Pooling/dynamic allocation and indexing
    [Tooltip("The instantiation data for all non-phase specific objects")]
    [SerializeField] private List<ObjectSpawnData> objectData = new(); // FIX: why do we need this for a dynamic spawner?
    private int extendedDataStartIndex = 0;
    
    Listener => {
        if (objectData.Count > extendedDataStartIndex) {
            objectData.RemoveRange(extendedDataStartIndex, objectData.Count - extendedDataStartIndex);
        }
    }

    #if UNITY_EDITOR
        // Editor-Time Validation:
        private void OnValidate() {
            // Ensure all entityData have prefabs assigned...
            for (int i = 0; i < objectData.Count; i++) {
                if (objectData[i].Prefab == null) {
                    Debug.LogError($"{GetType()} '{name}': {nameof(objectData)}[{i}] has no prefab assigned...");
                    continue;
                }
            }
        }
    #endif
- Keep entities on non-spawner phase change
*/