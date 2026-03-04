using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized game state store for flags, counters, and NPC relationships.
/// Used by dialogue conditions/actions to check and modify game state.
///
/// Storage: Uses serializable key-value pair lists (Unity can't serialize
/// Dictionary directly). Builds runtime dictionaries on Awake for fast lookup.
///
/// Setup:
///   1. Create via: Right-click > Create > Karma > Variable Store
///   2. Save as Assets/Data/GameVariables.asset
///   3. Place in a Resources folder OR assign to a manager
///
/// Access: VariableStore.Instance.GetFlag("hasMetAnanda")
/// </summary>
[CreateAssetMenu(fileName = "GameVariables", menuName = "Karma/Variable Store", order = 5)]
public class VariableStore : ScriptableObject
{
    // ─── Singleton (found via Resources) ──────────────────────
    private static VariableStore _instance;
    public static VariableStore Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<VariableStore>("GameVariables");
                if (_instance == null)
                {
                    // Fallback: find any VariableStore in project
                    var all = Resources.FindObjectsOfTypeAll<VariableStore>();
                    if (all.Length > 0) _instance = all[0];
                }
                if (_instance != null)
                    _instance.BuildCaches();
            }
            return _instance;
        }
    }

    // ─── Serializable Data ────────────────────────────────────
    [Header("Flags (boolean state)")]
    [Tooltip("Boolean flags like 'hasMetAnanda', 'chapter1Complete'")]
    [SerializeField] private List<StringBoolEntry> flagEntries = new List<StringBoolEntry>();

    [Header("Counters (numeric state)")]
    [Tooltip("Integer counters like 'ghostsHelped', 'deathCount'")]
    [SerializeField] private List<StringIntEntry> counterEntries = new List<StringIntEntry>();

    [Header("Relationships (NPC affinity)")]
    [Tooltip("NPC relationship scores like 'serna: 50', 'ananda: 0'")]
    [SerializeField] private List<StringIntEntry> relationshipEntries = new List<StringIntEntry>();

    // ─── Runtime Caches ───────────────────────────────────────
    [NonSerialized] private Dictionary<string, bool> flagCache;
    [NonSerialized] private Dictionary<string, int> counterCache;
    [NonSerialized] private Dictionary<string, int> relationshipCache;
    [NonSerialized] private bool cacheBuilt;

    // ─── Events ───────────────────────────────────────────────

    /// <summary>Fired when any variable changes. Args: (key, category, newValue as string).</summary>
    public event Action<string, string, string> OnVariableChanged;

    // ─── Flags API ────────────────────────────────────────────

    public bool GetFlag(string name)
    {
        EnsureCache();
        if (string.IsNullOrEmpty(name)) return false;
        return flagCache.TryGetValue(name, out bool val) && val;
    }

    public void SetFlag(string name, bool value)
    {
        EnsureCache();
        if (string.IsNullOrEmpty(name)) return;

        flagCache[name] = value;
        SyncFlagToEntries(name, value);
        OnVariableChanged?.Invoke(name, "flag", value.ToString());
    }

    // ─── Counters API ─────────────────────────────────────────

    public int GetCounter(string name)
    {
        EnsureCache();
        if (string.IsNullOrEmpty(name)) return 0;
        return counterCache.TryGetValue(name, out int val) ? val : 0;
    }

    public void SetCounter(string name, int value)
    {
        EnsureCache();
        if (string.IsNullOrEmpty(name)) return;

        counterCache[name] = value;
        SyncCounterToEntries(name, value);
        OnVariableChanged?.Invoke(name, "counter", value.ToString());
    }

    public void ModifyCounter(string name, int delta)
    {
        SetCounter(name, GetCounter(name) + delta);
    }

    // ─── Relationships API ────────────────────────────────────

    public int GetRelationship(string npcId)
    {
        EnsureCache();
        if (string.IsNullOrEmpty(npcId)) return 0;
        return relationshipCache.TryGetValue(npcId, out int val) ? val : 0;
    }

    public void SetRelationship(string npcId, int value)
    {
        EnsureCache();
        if (string.IsNullOrEmpty(npcId)) return;

        relationshipCache[npcId] = value;
        SyncRelationshipToEntries(npcId, value);
        OnVariableChanged?.Invoke(npcId, "relationship", value.ToString());
    }

    public void ModifyRelationship(string npcId, int delta)
    {
        SetRelationship(npcId, GetRelationship(npcId) + delta);
    }

    // ─── Utility ──────────────────────────────────────────────

    /// <summary>Reset all variables to empty.</summary>
    public void ResetAll()
    {
        flagEntries.Clear();
        counterEntries.Clear();
        relationshipEntries.Clear();
        flagCache?.Clear();
        counterCache?.Clear();
        relationshipCache?.Clear();
        Debug.Log("VariableStore: All variables reset.");
    }

    /// <summary>Get all flag names (for editor browser).</summary>
    public List<string> GetAllFlagNames()
    {
        EnsureCache();
        return new List<string>(flagCache.Keys);
    }

    /// <summary>Get all counter names (for editor browser).</summary>
    public List<string> GetAllCounterNames()
    {
        EnsureCache();
        return new List<string>(counterCache.Keys);
    }

    /// <summary>Get all relationship NPC IDs (for editor browser).</summary>
    public List<string> GetAllRelationshipIds()
    {
        EnsureCache();
        return new List<string>(relationshipCache.Keys);
    }

    // ─── Cache Management ─────────────────────────────────────

    private void EnsureCache()
    {
        if (!cacheBuilt) BuildCaches();
    }

    public void BuildCaches()
    {
        flagCache = new Dictionary<string, bool>();
        foreach (var entry in flagEntries)
            if (!string.IsNullOrEmpty(entry.key))
                flagCache[entry.key] = entry.value;

        counterCache = new Dictionary<string, int>();
        foreach (var entry in counterEntries)
            if (!string.IsNullOrEmpty(entry.key))
                counterCache[entry.key] = entry.value;

        relationshipCache = new Dictionary<string, int>();
        foreach (var entry in relationshipEntries)
            if (!string.IsNullOrEmpty(entry.key))
                relationshipCache[entry.key] = entry.value;

        cacheBuilt = true;
    }

    // ─── Sync Cache → Serialized Lists ────────────────────────

    private void SyncFlagToEntries(string name, bool value)
    {
        for (int i = 0; i < flagEntries.Count; i++)
        {
            if (flagEntries[i].key == name)
            {
                flagEntries[i] = new StringBoolEntry { key = name, value = value };
                return;
            }
        }
        flagEntries.Add(new StringBoolEntry { key = name, value = value });
    }

    private void SyncCounterToEntries(string name, int value)
    {
        for (int i = 0; i < counterEntries.Count; i++)
        {
            if (counterEntries[i].key == name)
            {
                counterEntries[i] = new StringIntEntry { key = name, value = value };
                return;
            }
        }
        counterEntries.Add(new StringIntEntry { key = name, value = value });
    }

    private void SyncRelationshipToEntries(string npcId, int value)
    {
        for (int i = 0; i < relationshipEntries.Count; i++)
        {
            if (relationshipEntries[i].key == npcId)
            {
                relationshipEntries[i] = new StringIntEntry { key = npcId, value = value };
                return;
            }
        }
        relationshipEntries.Add(new StringIntEntry { key = npcId, value = value });
    }

    // ─── Lifecycle ────────────────────────────────────────────

    void OnEnable()
    {
        _instance = this;
        BuildCaches();
    }
}

// ═══════════════════════════════════════════════════════════════════
//  Serializable key-value pair wrappers (Unity can't serialize Dictionary)
// ═══════════════════════════════════════════════════════════════════

[Serializable]
public struct StringBoolEntry
{
    public string key;
    public bool value;
}

[Serializable]
public struct StringIntEntry
{
    public string key;
    public int value;
}
