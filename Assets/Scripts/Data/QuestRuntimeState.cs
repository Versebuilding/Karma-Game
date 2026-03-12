using System;
using System.Collections.Generic;

/// <summary>
/// Mutable runtime state for a single quest. Tracks current progress.
/// Serializable for save/load (JSON). NOT a MonoBehaviour or ScriptableObject.
///
/// QuestSO holds the immutable definition. This class holds the mutable state.
/// QuestManager owns instances of this class.
/// </summary>
[Serializable]
public class QuestRuntimeState
{
    /// <summary>Links back to the QuestSO definition.</summary>
    public string questId;

    /// <summary>Current quest state.</summary>
    public QuestState state;

    /// <summary>Progress per objective. Key = objectiveId, Value = current count.</summary>
    public SerializableDictionary objectiveProgress;

    public QuestRuntimeState(string questId)
    {
        this.questId = questId;
        this.state = QuestState.Locked;
        this.objectiveProgress = new SerializableDictionary();
    }

    /// <summary>Get current progress for an objective (0 if not started).</summary>
    public int GetProgress(string objectiveId)
    {
        return objectiveProgress.Get(objectiveId);
    }

    /// <summary>Set progress for an objective.</summary>
    public void SetProgress(string objectiveId, int value)
    {
        objectiveProgress.Set(objectiveId, value);
    }
}

/// <summary>
/// Unity-serializable string→int dictionary (Unity can't serialize Dictionary directly).
/// Uses parallel lists like VariableStore's pattern.
/// </summary>
[Serializable]
public class SerializableDictionary
{
    public List<string> keys = new List<string>();
    public List<int> values = new List<int>();

    public int Get(string key)
    {
        int idx = keys.IndexOf(key);
        return idx >= 0 ? values[idx] : 0;
    }

    public void Set(string key, int value)
    {
        int idx = keys.IndexOf(key);
        if (idx >= 0)
        {
            values[idx] = value;
        }
        else
        {
            keys.Add(key);
            values.Add(value);
        }
    }

    public bool ContainsKey(string key) => keys.Contains(key);
}
