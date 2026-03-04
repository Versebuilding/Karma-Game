using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reflection-based discovery of all IDialogueCondition and IDialogueAction types.
/// Caches results and rebuilds on recompile.
///
/// Usage:
///   DialogueTypeCache.GetConditionTypes()  → all classes implementing IDialogueCondition
///   DialogueTypeCache.BuildConditionMenu() → GenericMenu dropdown for adding conditions
///   DialogueTypeCache.BuildActionMenu()    → GenericMenu dropdown for adding actions
///
/// Adding new types: just create a [Serializable] class implementing IDialogueCondition
/// or IDialogueAction. It will auto-appear in the dropdown next recompile.
/// </summary>
[InitializeOnLoad]
public static class DialogueTypeCache
{
    private static List<Type> _conditionTypes;
    private static List<Type> _actionTypes;

    static DialogueTypeCache()
    {
        // Rebuild caches on domain reload (recompile)
        _conditionTypes = null;
        _actionTypes = null;
    }

    // ─── Condition Types ─────────────────────────────────────

    /// <summary>Get all concrete types implementing IDialogueCondition.</summary>
    public static List<Type> GetConditionTypes()
    {
        if (_conditionTypes == null)
        {
            _conditionTypes = FindImplementations<IDialogueCondition>();
        }
        return _conditionTypes;
    }

    /// <summary>Build a GenericMenu dropdown of all condition types.</summary>
    public static GenericMenu BuildConditionMenu(Action<IDialogueCondition> onSelect)
    {
        var menu = new GenericMenu();
        foreach (var type in GetConditionTypes())
        {
            string displayName = FormatTypeName(type);
            Type capturedType = type;
            menu.AddItem(new GUIContent(displayName), false, () =>
            {
                var instance = (IDialogueCondition)Activator.CreateInstance(capturedType);
                onSelect?.Invoke(instance);
            });
        }
        if (menu.GetItemCount() == 0)
            menu.AddDisabledItem(new GUIContent("No condition types found"));
        return menu;
    }

    // ─── Action Types ────────────────────────────────────────

    /// <summary>Get all concrete types implementing IDialogueAction.</summary>
    public static List<Type> GetActionTypes()
    {
        if (_actionTypes == null)
        {
            _actionTypes = FindImplementations<IDialogueAction>();
        }
        return _actionTypes;
    }

    /// <summary>Build a GenericMenu dropdown of all action types.</summary>
    public static GenericMenu BuildActionMenu(Action<IDialogueAction> onSelect)
    {
        var menu = new GenericMenu();
        foreach (var type in GetActionTypes())
        {
            string displayName = FormatTypeName(type);
            Type capturedType = type;
            menu.AddItem(new GUIContent(displayName), false, () =>
            {
                var instance = (IDialogueAction)Activator.CreateInstance(capturedType);
                onSelect?.Invoke(instance);
            });
        }
        if (menu.GetItemCount() == 0)
            menu.AddDisabledItem(new GUIContent("No action types found"));
        return menu;
    }

    // ─── Helpers ─────────────────────────────────────────────

    /// <summary>Find all concrete [Serializable] classes implementing T.</summary>
    private static List<Type> FindImplementations<T>()
    {
        var interfaceType = typeof(T);
        var results = new List<Type>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // Skip Unity internal assemblies for performance
            var name = assembly.GetName().Name;
            if (name.StartsWith("Unity") || name.StartsWith("System") ||
                name.StartsWith("Mono") || name.StartsWith("mscorlib") ||
                name.StartsWith("netstandard") || name.StartsWith("nunit"))
                continue;

            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsClass && !type.IsAbstract &&
                        interfaceType.IsAssignableFrom(type) &&
                        type.GetCustomAttribute<SerializableAttribute>() != null)
                    {
                        results.Add(type);
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Some assemblies may fail to load types — skip silently
            }
        }

        results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        return results;
    }

    /// <summary>Convert "ModifyKarmaAction" → "Modify Karma"</summary>
    public static string FormatTypeName(Type type)
    {
        string name = type.Name;

        // Remove common suffixes
        if (name.EndsWith("Action")) name = name.Substring(0, name.Length - 6);
        else if (name.EndsWith("Condition")) name = name.Substring(0, name.Length - 9);

        // Insert spaces before capitals: "ModifyKarma" → "Modify Karma"
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                result.Append(' ');
            result.Append(name[i]);
        }

        return result.ToString();
    }
}
