using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// FIX: null phaseSO checking
// FIX: remove constant active checking : Enabling/disability

/// <summary>
/// Controller of the linear progression for a predetermined set of <see cref="PhaseSO"/> objects, creating phase-based gameplay
/// </summary>
/// <remarks>
/// <b>Permits:</b><br/>
/// - round-style gameplay<br/>
/// - action looping<br/>
/// </remarks>
public class PhaseManager : MonoBehaviour {
	/// <summary>
	/// Indicates that a <see cref="PhaseManager"/> instance has transitioned to a new phase
	/// </summary>
    /// <remarks><b>Note:</b> Exists on startup and persists</remarks>
	public static readonly UnityEvent<PhaseSO> PhaseChange = new();


    // VARIABLES:
    // Inspector:
    [Tooltip("Will the phase manager be active on startup?")]
    [SerializeField] private bool isActive = false;
    [Tooltip("Ordered sequence of phases (executes linearly)")]
    [SerializeField] private List<PhaseSO> phases = new List<PhaseSO>();

    // Runtime:
    private int currentPhaseIndex = 0;
    private float phaseTimer = 0f;

	// Accessors:
	/// <summary>
	/// Is this <see cref="PhaseManager"/> instance currently running/updating?
	/// </summary>
	public bool IsActive => isActive;
	/// <summary>
	/// A read-only sequence for the linear progression that this <see cref="PhaseManager"/> instance will experience in terms of its set phases
	/// </summary>
	public IReadOnlyList<PhaseSO> Phases => phases; // FIX: null phases treated as non-argument flag phases
	/// <summary>
	/// The index of the currently set phase as it appears in <see cref="phases"/>
	/// </summary>
	public int CurrentPhaseIndex => currentPhaseIndex;
	/// <summary>
	/// The time left in the currently set phase
	/// </summary>
	public float PhaseTimer => phaseTimer;


    // Unity System Processing:
    private void Start() { // FIX: Move to Awake and OnEnable
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
        PhaseChange.Invoke(phases[currentPhaseIndex]);

        phaseTimer = phases[currentPhaseIndex].PhaseDuration;
    }


	// Public Control API:
	/// <summary>
	/// Attempt to activate/deactivate the <see cref="PhaseManager"/> instance based on <paramref name="active"/>
	/// </summary>
	/// <param name="active">The state to set the <see cref="PhaseManager"/> instance to, active (<see langword="true"/>) or inactive </param>
	/// <returns>The state of <see cref="isActive"/> after execution</returns>
	public bool ToggleActiveState(bool active) { // FIX: change to enabling/disabling
        // Only allow state setting if this manager could logically run
        if (phases.Count != 0) {
            return isActive = active;
        }

        // Otherwise return the default (isActive = false)
        return isActive;
    }

	/// <summary>
	/// Immediately jump to and start the phase referenced at <see cref="phases"/>[<paramref name="index"/>]
	/// </summary>
	/// <remarks>
	/// <b>Note:</b> If <see cref="isActive"/> is <see langword="false"/>, the <see cref="PhaseManager"/> instance will be primed with the
	/// <see cref="phases"/>[<paramref name="index"/>] data but will not automatically start
	/// </remarks>
	/// <param name="index">The index of the <see cref="phases"/> phase to jump to : <c>[0, <see cref="phases"/>.Count)</c></param>
	/// <returns>If the jump was successful</returns>
	public bool JumpPhase(int index) {
        if (index < 0 || index >= phases.Count) return false;

        currentPhaseIndex = index;

        StartPhase();

        return true;
    }


#if UNITY_EDITOR
    // Editor-Time Validation:
    private void OnValidate() {
        if (phases.Count == 0) {
            Debug.LogWarning($"{GetType()} '{name}': No phases configured. Add {typeof(PhaseSO)} assets to the '{nameof(phases)}' list or the {GetType()} will deactivate on startup...");
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
- flagging
*/