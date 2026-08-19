using System;
using UnityEngine;

/// <summary>
/// Item definition ScriptableObject. Create via: Right-click > Create > Karma > Item.
/// Used for inventory items, quest items, collectibles, and reflection cards.
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
    public string itemName;

    [Tooltip("Description shown when item is selected in inventory")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Item icon for inventory grid")]
    public Sprite icon;

    [Tooltip("Larger item image for detail panel")]
    public Sprite detailImage;

    [Header("Item Type")]
    [Tooltip("Category of this item")]
    public ItemCategory category = ItemCategory.Collectible;


    [Header("Karma / Value")]
    [Tooltip("Karma awarded when this item is collected")]
    public int karmaOnCollect;

    [Tooltip("Coin value of this item")]
    public int coinValue;

    [Tooltip("Flavor text shown below description (earned in chapter X quest Y)")]
    public string flavorText;


	// DEP // FIX: remove this stuff from the greater codebase (is currently being used in dialog system)
	public bool isQuestItem;
    public string questId;
}

/// <summary>
/// Categories for inventory items.
/// </summary>
public enum ItemCategory
{
    Collectible,    // General items (heart, scarf, backpack from mockup)
	QuestItem,      // Items needed for quests (bread, fish)
	KeyItem,        // Story-critical items
	Consumable      // Items that can be used
}
