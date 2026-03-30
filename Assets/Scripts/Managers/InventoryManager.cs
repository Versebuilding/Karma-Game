using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple singleton inventory manager. Holds ItemSO references at runtime.
/// Also syncs with VariableStore by setting "has_{itemName}" flags on add/remove.
///
/// Setup: Add to the "GameManagers" GameObject via Karma > Setup Game Systems.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────
    public static InventoryManager Instance { get; private set; }

    // ─── Runtime State ──────────────────────────────────────────
    private List<ItemSO> items = new List<ItemSO>();

    // ─── Events ─────────────────────────────────────────────────

    /// <summary>Fired when an item is added. Arg: the item.</summary>
    public event Action<ItemSO> OnItemAdded;

    /// <summary>Fired when an item is removed. Arg: the item.</summary>
    public event Action<ItemSO> OnItemRemoved;

    // ─── Public API ─────────────────────────────────────────────

    /// <summary>Add an item to the inventory.</summary>
    public void AddItem(ItemSO item)
    {
        if (item == null) return;

        items.Add(item);
        OnItemAdded?.Invoke(item);

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
            OnItemRemoved?.Invoke(item);

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

    /// <summary>Get all items in the inventory.</summary>
    public List<ItemSO> GetItems() => new List<ItemSO>(items);

    /// <summary>Get the count of a specific item by name.</summary>
    public int GetItemCount(string itemName)
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].itemName == itemName)
                count++;
        }
        return count;
    }

    /// <summary>Clear all items (for game reset).</summary>
    public void ClearItems()
    {
        items.Clear();
        Debug.Log("InventoryManager: All items cleared.");
    }

    // ─── Unity Lifecycle ────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("InventoryManager: Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
