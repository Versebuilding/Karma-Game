using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manager responsible for tracking which <see cref="ReflectionCardSO"/> instances
/// the player has obtained and selecting new cards to award.
/// </summary>
public class ReflectionCardManager : MonoBehaviour
{
	//[Tooltip("Drop weight per CardRarity for randomly selecting among eligible cards")]
	/*[SerializeField]*/ Dictionary<CardRarity, int> rarityDropChance = new(); // FIX: Add auto population tool & Dictionary can't be serialized
	[Tooltip("All reflection cards available in this game mode/scene")]
	[SerializeField] List<ReflectionCardSO> availableCards = new();
	long reflectionCardFlags = 0;

	private void Awake() {
		for (int i = 0; i < availableCards.Count; i++) {
			availableCards[i].SetFlagIndex(i);

			availableCards[i].FlagSet.AddListener((state) => SetFlag(i, state)); // FIX: should be disconnected
		}
	}

    void SetFlag(int flagIndex, bool state) {
        long bitField = (long)1 << flagIndex;
		// Left Shift : 1 << 3 == (binary) 1000
		// Creates a bit mask for the focus flag

		if (state) {
            reflectionCardFlags |= bitField; // Bitwise OR assignment
        }
        else if ((reflectionCardFlags & bitField) != 0) { // Bitwise XOR will erroneously set the bit if a check for it already being active is not performed
			reflectionCardFlags ^= bitField; // Bitwise XOR assignment
		}
	}

	/// <returns>Read-only list of unobtained <see cref="ReflectionCardSO"/>s</returns>
	public ReadOnlyCollection<ReflectionCardSO> GetUnobtainedCards() {
		return availableCards.Where(card => (reflectionCardFlags & ((long)1 << card.FlagIndex)) == 0).ToList().AsReadOnly();
	}

	/// <returns>Read-only list of obtained <see cref="ReflectionCardSO"/>s</returns>
	public ReadOnlyCollection<ReflectionCardSO> GetObtainedCards() {
		return availableCards.Where(card => (reflectionCardFlags & ((long)1 << card.FlagIndex)) != 0).ToList().AsReadOnly();
	}

	/// <summary>
	/// Award the player a new <see cref="ReflectionCardSO"/> based on provided criteria
	/// </summary>
	/// <remarks>
	/// <b>Selection Criteria:</b><br/>
	/// - <see cref="CardRarity"/> : the pre-determined rarity of each card<br/>
	/// - <paramref name="karma"/> with <see cref="ScoreRequirement"/> : if the player is within the pre-determined span for achieving each card
	/// </remarks>
	/// <param name="karma">The karma score utilized in determining card obtainment eligibility</param>
	/// <returns>The awarded <see cref="ReflectionCardSO"/>, or <see langword="null"/> if none are eligible</returns>
	public ReflectionCardSO GetNewCard(int karma) {
		ReflectionCardSO[] potentialCards = availableCards.Where(card => (reflectionCardFlags & ((long)1 << card.FlagIndex)) == 0 && card.KarmaRequirement.IsRequirementSatisfied(karma)).ToArray();
		// Get all cards which are eligible and unobtained

		switch (potentialCards.Length) {
			case 0: // No Obtainable Cards: (rarity irrelevant)
				return null;
			case 1: // 1 Obtainable Card: (rarity irrelevant)
				return potentialCards[0];
			default: // >1 Obtainable Cards: (rarity required)
				int[] winRange = new int[potentialCards.Length];

				int totalWeight = 0;
				for (int i = 0; i < potentialCards.Length; i++) {
					winRange[i] = totalWeight += rarityDropChance[potentialCards[i].Rarity];
				}
				// Populate winRange with the upper bounds (excluded) of each card's awarding range

				int cardIndex = Array.BinarySearch(winRange, UnityEngine.Random.Range(0, totalWeight));
				// Takes a random value [0, totalWeight - 1] and searches winRange for it in O(lg n) time
				// Returns the index of a hit value or the two's compliment (negative value) of where it should be in the array
				ReflectionCardSO card;

				if (cardIndex >= 0) card = potentialCards[cardIndex + 1]; // If a hit occurs, being that its excluded, the next index is the awarded card
				else card = potentialCards[~cardIndex]; // If the search fails, undo the two's compliment for the range its in, the associated index is the awarded card

				SetFlag(card.FlagIndex, true);
				return card;
		}
	}
	/* Programmer's Notes:
	- GetNewCard() currently only uses the following attributes to decide which card is given
		- Karma Score
		- Rarity
	- Other requirements' systems will need to be fleshed out before they can be implemented here
		- Category/Pattern
		- Realm
	*/
}