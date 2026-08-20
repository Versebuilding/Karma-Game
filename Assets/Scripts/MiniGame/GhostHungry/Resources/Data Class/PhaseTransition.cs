using System;
using UnityEngine;

/// <summary>
/// Simple, serializable transition used by PhaseSO.ChangePhase(score).
/// Uses the existing ComparisonOp enum (from dialogue/conditions code) to compare a score threshold.
/// </summary>
[System.Serializable]
public class PhaseTransition : MonoBehaviour {

    enum TransitionConditions { ScoreAbove, ScoreBelow, ScoreEquals, Always, ScoreBetween }

    [SerializeField] private PhaseSO nextPhase;
    [SerializeField] private int[] scoreThreshold;
    [SerializeField] private TransitionConditions condition;

    public PhaseSO NextPhase => nextPhase;

    /// <summary>
    /// Evaluates the transition condition against the provided score.
    /// PhaseSO.ChangePhase(uint score) relies on this signature.
    /// </summary>
    public bool ConditionsMet(uint score) {
        switch (condition) {
            case TransitionConditions.ScoreAbove:
                return score > scoreThreshold[0];
            case TransitionConditions.ScoreBelow:
                return score < scoreThreshold[0];
            case TransitionConditions.ScoreEquals:
                return score == scoreThreshold[0];
            case TransitionConditions.ScoreBetween:
                return score > scoreThreshold[0] && score < scoreThreshold[1];
            case TransitionConditions.Always:
                return true;
            default:
                return false;
        }
    }

    private void OnValidate() {
        switch (scoreThreshold.Length) {
            case 0:
                condition = TransitionConditions.Always;

                break;
            case 1:
                if (condition > TransitionConditions.ScoreEquals) {
                    condition = TransitionConditions.ScoreEquals;
                }

                break;
            case 2:
                ValidateVector();

                break;
            default:
                Array.Resize(ref scoreThreshold, 2);

                ValidateVector();

                break;
        }
    }

    private void ValidateVector() {
        if (scoreThreshold[0] > scoreThreshold[1]) {
            scoreThreshold[1] = scoreThreshold[0];
        }

        condition = TransitionConditions.ScoreBetween;
    }
}