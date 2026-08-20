using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Defines a reflection card to be awarded at the end of a given chapter
/// </summary>
[CreateAssetMenu(fileName = "NewReflectionCard", menuName = "Karma/Reflection Card")]
public class ReflectionCardSO : ScriptableObject
{
	// Events:
	/// <summary>
	/// Indicates that the associated reflection card was obtained (<see langword="bool"/> is <see langword="true"/>) or unobtained (<see langword="bool"/> is <see langword="false"/>)
	/// </summary>
	public UnityEvent<bool> FlagSet;
	

	// Identifiers:
	[SerializeField][HideInInspector] string uid = Guid.NewGuid().ToString();
	/// <summary>
	/// Internal unique identifier for this asset
	/// </summary>
	/// <remarks>
	/// - Set on creation/compilation and constant after<br/>
	/// - Stable across runtime
	/// </remarks>
	public string UID => uid;

	int flagIndex = -1;
	/// <summary>
	/// Index of the card's bit in the global reflection-card bitfield
	/// </summary>
	/// <remarks>
	/// <c>-1</c> indicates no index has been assigned yet
	/// </remarks>
	public int FlagIndex => flagIndex;
	

	[Header("Card Text")]
	[Tooltip("Title shown at the top of the reflection card")]
	[SerializeField] string title = string.Empty;
	/// <summary>
	/// Title shown at the top of the reflection card
	/// </summary>
	public string Title => title;

	[Tooltip("Main message shown on the reflection card")]
	[TextArea(2, 4)]
	[SerializeField] string message = string.Empty;
	/// <summary>
	/// Message text displayed on the reflection card
	/// </summary>
	public string Message => message;


	[Header("Card Tags")]
	[Tooltip("Pattern tag used for categorizing card behaviors")]
	[SerializeField] CardPattern pattern; // FIX?: currently unused in deciding which card is given
	/// <summary>
	/// Pattern tag describing the card's behavioral attribute
	/// </summary>
	public CardPattern Pattern => pattern;

	[Tooltip("High-level category of this card")]
	[SerializeField] CardCategory category; // FIX?: currently unused in deciding which card is given
	/// <summary>
	/// Card category for sorting/filtering
	/// </summary>
	public CardCategory Category => category;

	[Tooltip("Rarity of this card, used for weighted awarding")]
	[SerializeField] CardRarity rarity;
	/// <summary>
	/// Card rarity for selecting which card to award
	/// </summary>
	public CardRarity Rarity => rarity;


	[Header("Achievement Parameters")]
	[Tooltip("Karma requirement to be satisfied for this card to be awarded")]
	[SerializeField] ScoreRequirement karmaRequirement;
	/// <summary>
	/// Karma requirement to determine eligibility for this card
	/// </summary>
	public ScoreRequirement KarmaRequirement => karmaRequirement;
	// Programmer's Note: Add additional ScoreRequirement attributes HERE for any numeric-based requirements


	[Header("Visuals")]
	[Tooltip("Icon used when presenting the card in UI")]
	[SerializeField] Sprite icon;
	/// <summary>
	/// Card icon sprite
	/// </summary>
	public Sprite Icon => icon;

	[Tooltip("Primary color used in the card UI")]
	[SerializeField] Color cardColor;
	/// <summary>
	/// Color used for the card's visual frame/background
	/// </summary>
	public Color CardColor => cardColor;


	// Public API:
	/// <summary>
	/// Assign an index (<c>[0, 63]</c>) for this card in the global bitfield
	/// </summary>
	/// <param name="value">Index to assign : <c>[0, 63]</c></param>
	/// <returns><see langword="true"/> on success; <see langword="false"/> if the index is out of range.</returns>
	public bool SetFlagIndex(int value) {
		if (value < 0 || value > 63) {
			return false;
		}

		flagIndex = value;

		return true;
	}


	// Unity Processing
#if UNITY_EDITOR
	private void OnEnable() {
		if (karmaRequirement == null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(this))) {
			karmaRequirement = CreateInstance<ScoreRequirement>();
			karmaRequirement.name = nameof(karmaRequirement);

			AssetDatabase.AddObjectToAsset(karmaRequirement, this);
			AssetDatabase.SaveAssets();
		}
	}
#endif
}

/// <summary>Behavioral pattern categories for <see cref="ReflectionCardSO"/>, represents a manner in which the player acts</summary>
public enum CardPattern
{
	Overload,
	Crash,
	Balanced,
	Impulsive,
	Recovery
}

/// <summary>High level <see cref="ReflectionCardSO"/> categories</summary>
public enum CardCategory {
	Emotional,
	Behavioral,
	Achievement,
	Insight
}

/// <summary>Card rarity levels</summary>
public enum CardRarity {
	Common,
	Uncommon,
	Rare,
	Epic
}