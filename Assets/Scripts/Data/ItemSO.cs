using UnityEngine;

/// <summary>
/// Item definition ScriptableObject. Create via: Right-click > Create > Karma > Item.
/// Used for inventory items, quest items, collectibles, and reflection cards.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Karma/Item", order = 3)]
public class ItemSO : ScriptableObject
{
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

    [Tooltip("If true, this item is needed for a quest objective")]
    public bool isQuestItem;

    [Tooltip("Quest ID this item belongs to (if quest item)")]
    public string questId;

    [Header("Karma / Value")]
    [Tooltip("Karma awarded when this item is collected")]
    public int karmaOnCollect;

    [Tooltip("Coin value of this item")]
    public int coinValue;

    [Tooltip("Flavor text shown below description (earned in chapter X quest Y)")]
    public string flavorText;
}

/// <summary>
/// Categories for inventory items.
/// </summary>
public enum ItemCategory
{
    Collectible,     // General items (heart, scarf, backpack from mockup)
    QuestItem,       // Items needed for quests (bread, fish)
    ReflectionCard,  // Wisdom cards (one per chapter)
    KeyItem,         // Story-critical items
    Consumable       // Items that can be used
}
