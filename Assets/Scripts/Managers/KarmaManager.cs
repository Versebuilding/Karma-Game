using System;
using UnityEngine;

/// <summary>
/// Singleton manager for the Karma progression system.
/// Tracks karma points, handles leveling, and fires events for UI.
///
/// The karma bar fills with points. When it reaches xpPerLevel, the player
/// levels up and a new petal blooms on the Flower of Life.
///
/// Setup: Add to a "GameManagers" GameObject in the scene.
///        Assign a KarmaConfig asset in the Inspector.
/// </summary>
public class KarmaManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────
    public static KarmaManager Instance { get; private set; }

    // ─── Configuration ──────────────────────────────────────────
    [Header("Configuration")]
    [Tooltip("Karma config asset (create via Create > Karma > Karma Config)")]
    [SerializeField] private KarmaConfig config;

    // ─── Runtime State ──────────────────────────────────────────
    private int currentKarma;
    private AudioSource audioSource;

    // ─── Public Properties ──────────────────────────────────────

    /// <summary>Total accumulated karma points.</summary>
    public int CurrentKarma => currentKarma;

    /// <summary>Current karma level (0-based, each level = one flower petal).</summary>
    public int CurrentLevel => config != null && config.xpPerLevel > 0
        ? Mathf.Min(currentKarma / config.xpPerLevel, config.maxLevel)
        : 0;

    /// <summary>Progress within the current level (0.0 to 1.0).</summary>
    public float CurrentLevelProgress
    {
        get
        {
            if (config == null || config.xpPerLevel <= 0) return 0f;
            if (CurrentLevel >= config.maxLevel) return 1f; // maxed out
            int karmaInCurrentLevel = currentKarma % config.xpPerLevel;
            return (float)karmaInCurrentLevel / config.xpPerLevel;
        }
    }

    /// <summary>Normalized karma score (0.0 to 1.0) for NPC behavior hooks.</summary>
    public float GetNormalizedKarma()
    {
        if (config == null || config.maxLevel <= 0 || config.xpPerLevel <= 0) return 0.5f;
        float maxKarma = config.maxLevel * config.xpPerLevel;
        return Mathf.Clamp01(currentKarma / maxKarma);
    }

    /// <summary>The karma config asset.</summary>
    public KarmaConfig Config => config;

    // ─── Events ─────────────────────────────────────────────────

    /// <summary>Fired when karma changes. Args: (newTotal, delta).</summary>
    public event Action<int, int> OnKarmaChanged;

    /// <summary>Fired when the player levels up. Arg: newLevel.</summary>
    public event Action<int> OnKarmaLevelUp;

    // ─── Public API ─────────────────────────────────────────────

    /// <summary>
    /// Add or subtract karma points. Positive = good karma, negative = bad.
    /// Fires OnKarmaChanged and potentially OnKarmaLevelUp.
    /// </summary>
    public void AddKarma(int amount)
    {
        if (amount == 0) return;

        int previousLevel = CurrentLevel;
        currentKarma = Mathf.Max(0, currentKarma + amount);
        int newLevel = CurrentLevel;

        // Play audio feedback
        if (audioSource != null && config != null)
        {
            AudioClip clip = amount > 0 ? config.karmaGainClip : config.karmaLossClip;
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        OnKarmaChanged?.Invoke(currentKarma, amount);

        // Check for level up
        if (newLevel > previousLevel)
        {
            if (audioSource != null && config != null && config.levelUpClip != null)
                audioSource.PlayOneShot(config.levelUpClip);

            OnKarmaLevelUp?.Invoke(newLevel);
            Debug.Log($"KarmaManager: Level up! {previousLevel} → {newLevel} (Karma: {currentKarma})");
        }
    }

    /// <summary>Set karma to a specific value (for save/load).</summary>
    public void SetKarma(int value)
    {
        currentKarma = Mathf.Max(0, value);
        OnKarmaChanged?.Invoke(currentKarma, 0);
    }

    // ─── Unity Lifecycle ────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("KarmaManager: Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Initialize from config
        if (config != null)
            currentKarma = config.startingKarma;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
