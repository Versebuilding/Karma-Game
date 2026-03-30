using System;
using UnityEngine;

/// <summary>
/// Dialogue action that gives an item to the player's inventory.
/// Example: Ananda gives bread to the player at the end of their conversation.
///
/// The item is added to InventoryManager and a VariableStore flag "has_{itemName}"
/// is automatically set for use in dialogue conditions.
/// </summary>
[Serializable]
public class GiveItemAction : IDialogueAction
{
    [Tooltip("Item to give to the player")]
    public ItemSO item;

    public string Label
    {
        get
        {
            if (item == null) return "Give Item: (none)";
            return $"Give: {item.itemName}";
        }
    }

    public void Execute()
    {
        if (item == null) return;

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(item);
        }
        else
        {
            // Fallback: set flag directly if InventoryManager isn't available
            Debug.LogWarning($"GiveItemAction: InventoryManager not found. Setting flag directly.");
            if (VariableStore.Instance != null && !string.IsNullOrEmpty(item.itemName))
            {
                string flagName = "has_" + item.itemName.Replace(" ", "_").Replace("'", "").ToLower();
                VariableStore.Instance.SetFlag(flagName, true);
            }
        }
    }
}
