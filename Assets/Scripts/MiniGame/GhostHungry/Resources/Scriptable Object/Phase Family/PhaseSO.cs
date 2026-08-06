using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The base <see langword="class"/> for all phase types utilized by the <see cref="PhaseManager"/>. It defines a single,
/// basic game phase without any additional inherent functionality.
/// </summary>
[CreateAssetMenu(fileName = "NewPhaseSO", menuName = "Karma/Phase/Phase SO", order = 0)]
public class PhaseSO : ScriptableObject {
    [Tooltip("Duration of this phase in seconds (minimum 0.01).")]
    [SerializeField][Min(0.01f)] private float phaseDuration = 30f;
	[Tooltip("Any additional arguments required by other game objects for this phase to properly function")]
    [SerializeField] private List<ScriptableObject> phaseArguments = new();

	/// <summary>
	/// The duration of this phase in seconds
	/// </summary>
	/// <remarks>
	/// <b>Range: </b> <c>[0, ...)</c><br/>
	/// A duration of <c>0</c> seconds indicates that this is a flag (untimed) phase where the associated <see cref="PhaseManager"/> will
	/// pause, iterating only on reactivation
	/// </remarks>
    public float PhaseDuration => phaseDuration;
	/// <summary>
	/// The read-only argument data utilized by other game objects to function as intended during this phase
	/// </summary>
    public IReadOnlyList<ScriptableObject> PhaseArguments => phaseArguments;
}

/* Future Build Ideas:
- Have PhaseSO hold the phase logic
    - [Tooltip("Optional custom phase logic (scriptable) executed for this phase.")]
    - [SerializeField] private MonoScript phaseLogic;
    - public MonoScript PhaseLogic => phaseLogic;

- Have PhaseSO utilize transition logic
    - [Tooltip("List of transitions evaluated to determine the next phase.")]
    - [SerializeField] private List<PhaseTransition> transitions = new List<PhaseTransition>();
    - public IReadOnlyList<PhaseTransition> Transitions => transitions;
    - Iteration:
        public PhaseSO ChangePhase(uint score) {
            foreach (PhaseTransition transition in transitions) {
                if (transition.ConditionsMet(score)) {
                    return transition.NextPhase;
                }
            }

            return null;
        }
    - Validation:
        #if UNITY_EDITOR
            private void OnValidate() {

                for (int i = 0; i < transitions.Count; i++) {
                    if (transitions[i] == null) {
                        transitions[i] = new();
                    }
                }
            }
        #endif
*/