using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unity.VisualScripting;
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

	[Tooltip("Indicates that an ItemSO of a certain quantity were added to the inventory")]
	/// <summary>
	/// Indicates that <see cref="int"/> <see cref="ItemSO"/>'s have been added to the player's inventory 
	/// </summary>
	public UnityEvent<ItemSO, int> OnInventoryAdd;
	[Tooltip("Indicates that an ItemSO of a certain quantity were removed to the inventory")]
	/// <summary>
	/// Indicates that <see cref="int"/> <see cref="ItemSO"/>'s have been removed from the player's inventory 
	/// </summary>
	public UnityEvent<ItemSO, int> OnInventoryRemove;


    // ─── Runtime State ──────────────────────────────────────────

	private Dictionary<string, int> inventory = new Dictionary<string, int>();
	private Dictionary<string, ItemSO> itemRegistry = new Dictionary<string, ItemSO>(); // global meta registry


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

	/// <summary>
	/// Read-only view of the player's inventory, a series of <see cref="string"/> UIDs representing a specific <see cref="ItemSO"/> tied to an <see cref="int"/> quantity
	/// </summary>
	public ReadOnlyDictionary<string, int> Inventory => (ReadOnlyDictionary<string, int>)inventory.AsReadOnlyCollection(); // FIX?: if used often, store the wrapper on runtime due to inefficiency

	/// <summary>
	/// Check if the player has a given <paramref name="item"/> in their <see cref="inventory"/>
	/// </summary>
	/// <param name="item"><see cref="ItemSO"/> which is being checked for</param>
	/// <returns><see langword="true"/> if the player has the item; <see langword="false"/> otherwise</returns>
	public bool HasItem(ItemSO item) => item != null && inventory.ContainsKey(item.UID);
	/// <summary>
	/// Check if the player has a given <see cref="ItemSO"/>, represented by its <see cref="string"/> UID, in their <see cref="inventory"/>
	/// </summary>
	/// <param name="uid">UID associated with the <see cref="ItemSO"/> to check for</param>
	/// <returns><see langword="true"/> if the player has the item; <see langword="false"/> otherwise</returns>
	public bool HasItem(string uid) => !string.IsNullOrEmpty(uid) && inventory.ContainsKey(uid);

	/// <summary>
	/// Add an <paramref name="amount"/> of the given <paramref name="item"/> to the <see cref="inventory"/>
	/// </summary>
	/// <param name="item"><see cref="ItemSO"/> to add</param>
	/// <param name="amount">Quantity to add</param>
	/// <returns><see langword="true"/> if the operation succeeded; <see langword="false"/> for invalid input</returns>
	public bool AddItem(ItemSO item, int amount = 1)
    {
		if (item == null || amount <= 0) return false;

		if (!inventory.TryAdd(item.UID, amount)) {
			inventory[item.UID] += amount;
		}

		OnInventoryAdd.Invoke(item, amount);

		// Sync flag to VariableStore for dialogue conditions // FIX: remove this code block and use HasItem() for dialog (this system is inefficient)
        if (VariableStore.Instance != null && !string.IsNullOrEmpty(item.itemName))
        {
            string flagName = "has_" + item.itemName.Replace(" ", "_").Replace("'", "").ToLower();
            VariableStore.Instance.SetFlag(flagName, true);
        }

        Debug.Log($"InventoryManager: Added '{item.itemName}'. Total items: {inventory.Count}"); // FIX: remove or move to logger in final build
		
		return true;
    }
	/// <summary>
	/// Add an <paramref name="amount"/> of the given <see cref="ItemSO"/>, reference by its <paramref name="uid"/>, to the <see cref="inventory"/>
	/// </summary>
	/// <param name="uid">UID of the <see cref="ItemSO"/> to add</param>
	/// <param name="amount">Quantity to add</param>
	/// <returns><see langword="true"/> if the operation succeeded; <see langword="false"/> for invalid input</returns>
	public bool AddItem(string uid, int amount = 1) {
		if (string.IsNullOrEmpty(uid) || amount <= 0) return false;

		if (!inventory.TryAdd(uid, amount)) {
			inventory[uid] += amount;
		}

		OnInventoryAdd.Invoke(itemRegistry[uid], amount);

		return true;
	}

	/// <summary>
	/// Remove an <paramref name="amount"/> of the provided <paramref name="item"/> from <see cref="inventory"/>
	/// </summary>
	/// <param name="item"><see cref="ItemSO"/> to remove.</param>
	/// <param name="amount">Quantity to remove</param>
	/// <returns><see langword="true"/> if the remove operation was successful; otherwise <see langword="false"/>.</returns>
	public bool RemoveItem(ItemSO item, int amount = 1)
    {
        if (item == null || amount <= 0 || !inventory.ContainsKey(item.UID)) return false;

		inventory[item.UID] -= amount;
		OnInventoryRemove.Invoke(item, amount);
		bool removed = true;

		if (inventory[item.UID] <= 0 && (removed = inventory.Remove(item.UID))) {
		// Clear flag if no more of this item remain // FIX: remove this code block (this system is inefficient)
			if (!HasItem(item.UID) && VariableStore.Instance != null) {
                string flagName = "has_" + item.itemName.Replace(" ", "_").Replace("'", "").ToLower();
                VariableStore.Instance.SetFlag(flagName, false);
            }

			Debug.Log($"InventoryManager: Removed '{item.itemName}'. Total items: {inventory.Count}"); // FIX: remove or move to logger in final build
        }

        return removed;
    }
	/// <summary>
	/// Remove an <paramref name="amount"/> of the provided <see cref="ItemSO"/>, reference by its <paramref name="uid"/>, from <see cref="inventory"/>
	/// </summary>
	/// /// <param name="uid">UID of the <see cref="ItemSO"/> to remove</param>
	/// <param name="amount">Quantity to remove</param>
	/// <returns><see langword="true"/> if the remove operation was successful; otherwise <see langword="false"/>.</returns>
	public bool RemoveItem(string uid, int amount = 1) {
		if (string.IsNullOrEmpty(uid) || amount <= 0 || !inventory.ContainsKey(uid)) return false;

		inventory[uid] -= amount;
		OnInventoryRemove.Invoke(itemRegistry[uid], amount);

		if (inventory[uid] <= 0) {
			return inventory.Remove(uid);
		}

                return true;
        }

	/// <summary>
	/// Clear all items from <see cref="inventory"/>
	/// </summary>
	/// <remarks>
	/// - Used for game reset
	/// </remarks>
	public void ClearInventory()
    {
        inventory.Clear();
        Debug.Log("InventoryManager: All items cleared."); // FIX: remove or move to logger in final build
    }

	
}