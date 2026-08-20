using System;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// Describes a numeric requirement using a comparison operator and one or two thresholds
/// </summary>
public class ScoreRequirement : ScriptableObject
{
	[Tooltip("Comparison to apply against the threshold value(s)")]
	[SerializeField] Comparison comparison;
	/// <summary>
	/// The comparison operator used when evaluating this <see cref="ScoreRequirement"/>
	/// </summary>
	public Comparison Comparison => comparison;

	[Tooltip("The numeric value(s) used for threshold comparisons (0 elements = no requirement, 1 = single comparison, 2 = range (lower, upper))")]
	[SerializeField] int[] threshold = Array.Empty<int>();
	/// <summary>
	/// Read-only access to the configured <see cref="threshold"/> value(s).
	/// </summary>
	public ReadOnlyCollection<int> Threshold => Array.AsReadOnly(threshold);

	/// <summary>
	/// Evaluate whether the provided <paramref name="value"/> satisfies this <see cref="ScoreRequirement"/>
	/// </summary>
	/// <param name="value">Numeric value to test against</param>
	/// <returns><see langword="true"/> if the <see cref="ScoreRequirement"/> is satisfied; otherwise <see langword="false"/></returns>
	public bool IsRequirementSatisfied(int value) {
		switch (threshold.Length) {
			case 0:
				return true;
			case 1: {
				return comparison switch {
					Comparison.Equal => value == threshold[0],
					Comparison.GreaterThan => value > threshold[0],
					Comparison.GreaterThanEqual => value >= threshold[0],
					Comparison.LessThan => value < threshold[0],
					Comparison.LessThanEqual => value <= threshold[0],
					_ => value >= threshold[0] // fallback
				};
			}
			case 2: {
				return comparison switch {
					Comparison.InsideInclusive => value >= threshold[0] && value <= threshold[1],
					Comparison.Inside => value > threshold[0] && value < threshold[1],
					Comparison.OutsideInclusive => value <= threshold[0] || value >= threshold[1],
					Comparison.Outside => value < threshold[0] || value > threshold[1],
					_ => value >= threshold[0] && value <= threshold[1] // fallback
				};
			}
			default:
				Debug.LogError($"karmaBounds has {threshold.Length} elements; only 0-2 supported.");
				return false;
		}
	}

#if UNITY_EDITOR
	// Unity Processing
	private void OnValidate() {
		switch (threshold.Length) {
			case 0:
				if (comparison != Comparison.None) comparison = Comparison.None;
				return;
			case 1:
				if (comparison < Comparison.Equal) comparison = Comparison.Equal;
				else if (comparison > Comparison.LessThanEqual) comparison = Comparison.LessThanEqual;
				return;
			case 2:
				if (comparison < Comparison.Inside) comparison = Comparison.Inside;

				if (threshold[0] > threshold[1]) (threshold[0], threshold[1]) = (threshold[1], threshold[0]);

				break;
			default:
				Array.Resize(ref threshold, 2);

				if (comparison < Comparison.Inside) comparison = Comparison.Inside;

				if (threshold[0] > threshold[1]) (threshold[0], threshold[1]) = (threshold[1], threshold[0]);

				break;
		}
	}
#endif
}

/// <summary>
/// Comparison operations available for a <see cref="ScoreRequirement"/>
/// </summary>
/// <remarks>
/// <b>Groupings:</b><br/>
/// - <c>0</c> : absolute comparisons<br/>
/// - <c>1-5</c> : single-value comparisons<br/>
/// - <c>6-9</c> : range comparisons
/// </remarks>
public enum Comparison {
	None,
	Equal,
	GreaterThan,
	LessThan,
	GreaterThanEqual,
	LessThanEqual,
	Inside,
	Outside,
	InsideInclusive,
	OutsideInclusive
}