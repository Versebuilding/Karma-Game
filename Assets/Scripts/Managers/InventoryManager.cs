using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Simple singleton inventory manager. Holds ItemSO references at runtime.
/// Also syncs with VariableStore by setting "has_{itemName}" flags on add/remove.
///
/// Setup: Add to the "GameManagers" GameObject via Karma > Setup Game Systems.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────
    [HideInInspector] public static InventoryManager Instance { get; private set; }

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
		DontDestroyOnLoad(gameObject);

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

        // Sync flag to VariableStore for dialogue conditions
        if (VariableStore.Instance != null && !string.IsNullOrEmpty(item.itemName))
        {
            string flagName = "has_" + item.itemName.Replace(" ", "_").Replace("'", "").ToLower();
            VariableStore.Instance.SetFlag(flagName, true);
        }

        Debug.Log($"InventoryManager: Added '{item.itemName}'. Total items: {items.Count}");
    }

    /// <summary>Remove the first occurrence of an item from the inventory.</summary>
    public bool RemoveItem(ItemSO item)
    {
        if (item == null) return false;

        bool removed = items.Remove(item);
        if (removed)
        {
		OnInventoryRemove.Invoke(item);

            // Clear flag if no more of this item remain
            if (!HasItem(item.itemName) && VariableStore.Instance != null)
            {
                string flagName = "has_" + item.itemName.Replace(" ", "_").Replace("'", "").ToLower();
                VariableStore.Instance.SetFlag(flagName, false);
            }

            Debug.Log($"InventoryManager: Removed '{item.itemName}'. Total items: {items.Count}");
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
        Debug.Log("InventoryManager: All items cleared.");
    }


}
