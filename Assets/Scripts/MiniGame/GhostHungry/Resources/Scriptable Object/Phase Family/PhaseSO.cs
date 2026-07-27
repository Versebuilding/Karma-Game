using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 
/// </summary>
[CreateAssetMenu(fileName = "NewPhaseSO", menuName = "Karma/Phase/Phase SO", order = 2)]
public class PhaseSO : ScriptableObject {
    [Tooltip("Duration of this phase in seconds (minimum 0.01).")]
    [SerializeField][Min(0.01f)] private float phaseDuration = 30f;
    [SerializeField] private List<ScriptableObject> phaseArguments = new();

    public float PhaseDuration => phaseDuration;
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