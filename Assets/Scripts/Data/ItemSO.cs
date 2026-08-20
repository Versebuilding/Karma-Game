using System;
using UnityEngine;

// FIX: restrict write access

/// <summary>
/// Describes an item which can be acquired and stored in the inventory as a consumable, quest item, or collectible
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Karma/Item", order = 3)]
public class ItemSO : ScriptableObject
{
	// Unique Identifier
    [SerializeField][HideInInspector] string uid = Guid.NewGuid().ToString();
	/// <summary>
	/// Internal unique identifier for this asset
	/// </summary>
	/// <remarks>
	/// - Set on creation/compilation and constant after<br/>
	/// - Stable across runtime
	/// </remarks>
	public string UID => uid;


	[Header("Item Info")]
    [Tooltip("Display name shown in inventory")]
	/// <summary>
	/// Display name shown to the player.
	/// </summary>
	public string itemName;

    [Tooltip("Description shown when item is selected in inventory")]
    [TextArea(2, 4)]
	/// <summary>
	/// Longer description / details about the item.
	/// </summary>
	public string description;

    [Tooltip("Item icon used for inventory UI")]
	/// <summary>
	/// Sprite used for inventory UI
	/// </summary>
	public Sprite icon;


    [Header("Item Type")]
    [Tooltip("Category of this item")]
	/// <summary>
	/// Item category used by systems that filter or handle behavior per-category
	/// </summary>
	public ItemCategory category = ItemCategory.Collectible;


    [Header("Karma / Value")]
    [Tooltip("Karma awarded when this item is collected")]
	/// <summary>
	/// Amount of karma to award on collection
	/// </summary>
	public int karmaOnCollect;

    [Tooltip("Coin value of this item")]
	/// <summary>
	/// In-game currency value of the item
	/// </summary>
	public int coinValue;

    [Tooltip("Flavor text shown below description (earned in chapter X quest Y)")]
	/// <summary>
	/// Optional flavor text
	/// </summary>
	public string flavorText;


	// DEP // FIX: remove this stuff from the greater codebase (is currently being used in dialog system)
	public bool isQuestItem;
    public string questId;
}

/// <summary>
/// Categories for inventory items.
/// </summary>
public enum ItemCategory // FIX: revamp
{
    Collectible,    // General items (heart, scarf, backpack from mockup)
	QuestItem,      // Items needed for quests (bread, fish)
	KeyItem,        // Story-critical items
	Consumable      // Items that can be used
}