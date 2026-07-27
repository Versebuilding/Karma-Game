using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 
/// </summary>
public class PhaseManager : MonoBehaviour {
    public static UnityEvent<PhaseSO> PhaseChange = new();


    [Tooltip("Will the phase manager be active on startup?")]
    [SerializeField] private bool isActive = false;
    [Tooltip("Ordered sequence of phases (executes linearly)")]
    [SerializeField] private List<PhaseSO> phases = new List<PhaseSO>();

    private int currentPhaseIndex = 0;
    private float phaseTimer = 0f;

    public bool IsActive => isActive;
    public IReadOnlyList<PhaseSO> Phases => phases;
    public int CurrentPhaseIndex => currentPhaseIndex;
    public float PhaseTimer => phaseTimer;
    

    // Unity System Processing:
    private void Start() {
        // No phase configuration => automatically deactivated manager
        if (phases.Count == 0) {
            isActive = false;
            return;
        }

        StartPhase();
    }

    private void Update() {
        if (!isActive) return;

        phaseTimer -= Time.deltaTime;

        if (phaseTimer <= 0) {
            AdvancePhase();
        }
    }


    // Helper Functions:
    // Iterate to the next phase (or reset the manager if at end)
    private void AdvancePhase() {
        // if at end => reset
        if (++currentPhaseIndex == phases.Count) {// ++x : auto iterates phase index
            currentPhaseIndex = 0;
            StartPhase();

            isActive = false;

            return;
        }

        StartPhase();
    }

    // Initialize the phase & signal its change
    private void StartPhase() {
        if (PhaseChange != null) {
            PhaseChange.Invoke(phases[currentPhaseIndex]);
        }

        phaseTimer = phases[currentPhaseIndex].PhaseDuration;
    }


    // Public Control API: (all functions return if the action was successful)
    // Toggle manager's active state
    public bool ToggleActiveState(bool active) {
        // Only allow state setting if this manager could logically run
        if (phases.Count != 0) {
            return isActive = active;
        }

        // Otherwise return the default (isActive = false)
        return isActive;
    }

    // Jump immediately to a specific phase index
    public bool JumpPhase(int index) {
        if (index < 0 || index >= phases.Count) return false;

        currentPhaseIndex = index;

        StartPhase();

        return true;
    }


#if UNITY_EDITOR
    // Editor-Time Validation:
    void OnValidate() {
        if (phases.Count == 0) {
            Debug.LogWarning($"PhaseManager '{name}': no phases configured. Add PhaseSO assets to the 'phases' list.");
        }
    }
#endif
}

/* Programmer's Notes:
- Originally currentPhaseIndex was settable in the editor, however, I can't think of a logical reason for this functionality given the added
complexity and little benefit (open to suggestions)
*/

/* Future Build Ideas:
- pausing/resuming phases
- uninterrupted looping
*/