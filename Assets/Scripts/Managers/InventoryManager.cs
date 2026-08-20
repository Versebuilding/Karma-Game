using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// FIX: transition from using ItemSO to string (uid)

/// <summary>
/// Represents the player's inventory and backpack while managing its lifecycle
/// </summary>
public class InventoryManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────
    [HideInInspector] public static InventoryManager Instance { get; private set; } // FIX: dependency injection/signal bussing adheres better to our coding standards

	// ─── Events ─────────────────────────────────────────────────
	public UnityEvent<ItemSO> OnInventoryAdd;
	public UnityEvent<ItemSO> OnInventoryRemove;

    // ─── Runtime State ──────────────────────────────────────────
    private List<ItemSO> items = new List<ItemSO>();
	// ─── Unity Lifecycle ────────────────────────────────────────

	private void Awake() {
		if (Instance != null && Instance != this) {
			Debug.LogWarning("InventoryManager: Duplicate instance destroyed.");
			Destroy(gameObject);
			return;
		}

		Instance = this;
		DontDestroyOnLoad(gameObject); // FIX: could cause loading problems based on where the script is located (local, in scene = bad, global = good)

		if (OnInventoryAdd == null) OnInventoryAdd = new();
		if (OnInventoryRemove == null) OnInventoryRemove = new();
	}

	private void OnDestroy() {
		if (Instance == this) Instance = null;
	}


    // ─── Public API ─────────────────────────────────────────────

    /// <summary>Add an item to the inventory.</summary>
    public void AddItem(ItemSO item)
    {
        if (item == null) return;

        items.Add(item);
		OnInventoryAdd.Invoke(item);

		// Sync flag to VariableStore for dialogue conditions // FIX: remove this code block and use HasItem() for dialog (this system is inefficient)
        if (VariableStore.Instance != null && !string.IsNullOrEmpty(item.itemName))
        {
            string flagName = "has_" + item.itemName.Replace(" ", "_").Replace("'", "").ToLower();
            VariableStore.Instance.SetFlag(flagName, true);
        }
		Debug.Log($"InventoryManager: Added '{item.itemName}'. Total items: {items.Count}"); // FIX: remove or move to logger in final build

		
    }

    /// <summary>Remove the first occurrence of an item from the inventory.</summary>
    public bool RemoveItem(ItemSO item)
    {
        if (item == null) return false;

        bool removed = items.Remove(item);
        if (removed)
        {
		OnInventoryRemove.Invoke(item);

		// Clear flag if no more of this item remain // FIX: remove this code block (this system is inefficient)
            if (!HasItem(item.itemName) && VariableStore.Instance != null)
            {
                string flagName = "has_" + item.itemName.Replace(" ", "_").Replace("'", "").ToLower();
                VariableStore.Instance.SetFlag(flagName, false);
            }

			Debug.Log($"InventoryManager: Removed '{item.itemName}'. Total items: {items.Count}"); // FIX: remove or move to logger in final build
        }
        return removed;
    }

    /// <summary>Check if the inventory contains an item by name.</summary>
    public bool HasItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return false;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].itemName == itemName)
                return true;
        }
        return false;
    }

	
	
	

	/// <summary>Clear all items (for game reset).</summary>
	public void ClearInventory() {
        items.Clear();
		Debug.Log("InventoryManager: All items cleared."); // FIX: remove or move to logger in final build
    }


}
