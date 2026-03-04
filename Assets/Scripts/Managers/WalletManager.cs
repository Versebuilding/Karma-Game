using System;
using UnityEngine;

/// <summary>
/// Singleton manager for the coin/currency wallet.
/// Tracks coins, handles spending, and fires events for UI.
///
/// Setup: Add to the "GameManagers" GameObject in the scene.
/// </summary>
public class WalletManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────
    public static WalletManager Instance { get; private set; }

    // ─── Configuration ──────────────────────────────────────────
    [Header("Configuration")]
    [Tooltip("Starting coins for a new game")]
    [SerializeField] private int startingCoins = 1500;

    // ─── Runtime State ──────────────────────────────────────────
    private int coins;

    // ─── Public Properties ──────────────────────────────────────

    /// <summary>Current coin balance.</summary>
    public int Coins => coins;

    // ─── Events ─────────────────────────────────────────────────

    /// <summary>Fired when coins change. Args: (newTotal, delta).</summary>
    public event Action<int, int> OnCoinsChanged;

    // ─── Public API ─────────────────────────────────────────────

    /// <summary>
    /// Add coins (positive) or deduct coins (negative).
    /// For deduction, use SpendCoins() instead for safety checks.
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount == 0) return;
        coins = Mathf.Max(0, coins + amount);
        OnCoinsChanged?.Invoke(coins, amount);
    }

    /// <summary>
    /// Attempt to spend coins. Returns true if the player had enough.
    /// </summary>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0) return true;
        if (coins < amount) return false;

        coins -= amount;
        OnCoinsChanged?.Invoke(coins, -amount);
        return true;
    }

    /// <summary>Set coins to a specific value (for save/load).</summary>
    public void SetCoins(int value)
    {
        coins = Mathf.Max(0, value);
        OnCoinsChanged?.Invoke(coins, 0);
    }

    // ─── Unity Lifecycle ────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("WalletManager: Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        coins = startingCoins;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
