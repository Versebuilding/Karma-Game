using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Editor tool that automatically adds missing animation states to an NPC's Animator Controller.
/// Reads clips from DialogueNPC Inspector fields (defaultIdleClips, defaultTalkClips)
/// and from DialogueSO nodeAnimation fields, then adds any missing states.
///
/// Menu: Karma > Sync NPC Animator States
///
/// This saves you from manually dragging clips into the Animator window.
/// After running, all clips referenced in DialogueNPC will have matching states
/// in the Animator Controller, so CrossFade can find them.
/// </summary>
public class AnimatorStateSync
{
    [MenuItem("Karma/Sync NPC Animator States")]
    public static void SyncAllNPCAnimatorStates()
    {
        Debug.Log("═══════════════════════════════════════════════════════");
        Debug.Log("  SYNC NPC ANIMATOR STATES");
        Debug.Log("═══════════════════════════════════════════════════════");

        var allNPCs = Object.FindObjectsByType<DialogueNPC>(FindObjectsSortMode.None);
        if (allNPCs.Length == 0)
        {
            Debug.LogWarning("  No DialogueNPC components found in scene!");
            return;
        }

        int totalAdded = 0;

        foreach (var npc in allNPCs)
        {
            int added = SyncNPCAnimatorStates(npc);
            totalAdded += added;
        }

        Debug.Log("");
        Debug.Log($"  DONE — Added {totalAdded} new state(s) across {allNPCs.Length} NPC(s).");
        Debug.Log("═══════════════════════════════════════════════════════");

        if (totalAdded > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("  Assets saved.");
        }
    }

    /// <summary>
    /// Sync states for a single DialogueNPC. Also handles selected object via menu validation.
    /// </summary>
    [MenuItem("Karma/Sync Selected NPC Animator")]
    public static void SyncSelectedNPC()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("No GameObject selected!");
            return;
        }

        var npc = Selection.activeGameObject.GetComponent<DialogueNPC>();
        if (npc == null)
        {
            npc = Selection.activeGameObject.GetComponentInChildren<DialogueNPC>();
        }

        if (npc == null)
        {
            Debug.LogWarning($"'{Selection.activeGameObject.name}' has no DialogueNPC component!");
            return;
        }

        Debug.Log("═══════════════════════════════════════════════════════");
        Debug.Log($"  SYNC ANIMATOR STATES — {npc.gameObject.name}");
        Debug.Log("═══════════════════════════════════════════════════════");

        int added = SyncNPCAnimatorStates(npc);

        Debug.Log($"\n  DONE — Added {added} new state(s).");
        Debug.Log("═══════════════════════════════════════════════════════");

        if (added > 0)
            AssetDatabase.SaveAssets();
    }

    private static int SyncNPCAnimatorStates(DialogueNPC npc)
    {
        Debug.Log($"\n── {npc.gameObject.name} ──────────────────────────────");

        // Find the Animator
        var animator = npc.GetComponent<Animator>();
        if (animator == null)
            animator = npc.GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning($"  ! No Animator found on '{npc.gameObject.name}'");
            return 0;
        }

        // Get the AnimatorController asset
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            // Might be an AnimatorOverrideController
            var overrideController = animator.runtimeAnimatorController as AnimatorOverrideController;
            if (overrideController != null)
                controller = overrideController.runtimeAnimatorController as AnimatorController;
        }

        if (controller == null)
        {
            Debug.LogWarning($"  ! No AnimatorController found on '{npc.gameObject.name}'");
            return 0;
        }

        Debug.Log($"  Animator Controller: {controller.name}");

        // Get the base layer state machine
        if (controller.layers.Length == 0)
        {
            Debug.LogWarning($"  ! AnimatorController has no layers!");
            return 0;
        }

        var stateMachine = controller.layers[0].stateMachine;

        // Collect all existing state names
        var existingStates = new HashSet<string>();
        foreach (var state in stateMachine.states)
        {
            existingStates.Add(state.state.name);
        }

        Debug.Log($"  Existing states: {string.Join(", ", existingStates)}");

        // Collect all clips that need states
        var clipsToAdd = new Dictionary<string, AnimationClip>(); // name → clip

        // 1) From DialogueNPC serialized fields (use SerializedObject to access private fields)
        var so = new SerializedObject(npc);

        CollectClipsFromArray(so.FindProperty("defaultIdleClips"), clipsToAdd, "defaultIdleClips");
        CollectClipsFromArray(so.FindProperty("defaultTalkClips"), clipsToAdd, "defaultTalkClips");

        // 2) From the NPC's assigned dialogue asset(s) — check nodeAnimation on each node
        var dialogueProp = so.FindProperty("dialogue");
        if (dialogueProp != null && dialogueProp.objectReferenceValue != null)
        {
            var dialogue = dialogueProp.objectReferenceValue as DialogueSO;
            if (dialogue != null && dialogue.nodes != null)
            {
                foreach (var node in dialogue.nodes)
                {
                    if (node.nodeAnimation != null && !string.IsNullOrEmpty(node.nodeAnimation.name))
                    {
                        if (!clipsToAdd.ContainsKey(node.nodeAnimation.name))
                        {
                            clipsToAdd[node.nodeAnimation.name] = node.nodeAnimation;
                            Debug.Log($"  Found clip in dialogue node '{node.nodeId}': {node.nodeAnimation.name}");
                        }
                    }
                }
            }
        }

        // 3) Add missing states
        int addedCount = 0;
        float xOffset = 250f;
        float yStart = 0f;

        // Find the rightmost state position to place new states next to it
        float maxX = 0f;
        foreach (var state in stateMachine.states)
        {
            if (state.position.x > maxX) maxX = state.position.x;
        }
        xOffset = maxX + 250f;

        foreach (var kvp in clipsToAdd)
        {
            string clipName = kvp.Key;
            AnimationClip clip = kvp.Value;

            if (existingStates.Contains(clipName))
            {
                Debug.Log($"  ✓ State '{clipName}' already exists");
                continue;
            }

            // Create new state
            var newState = stateMachine.AddState(clipName, new Vector3(xOffset, yStart + addedCount * 60f, 0f));
            newState.motion = clip;

            Debug.Log($"  + Added state '{clipName}' (clip: {clip.name}, length: {clip.length:F2}s)");
            addedCount++;
        }

        if (addedCount > 0)
        {
            EditorUtility.SetDirty(controller);
            Debug.Log($"  Saved {addedCount} new states to '{controller.name}'");
        }
        else
        {
            Debug.Log($"  All states already present — nothing to add.");
        }

        return addedCount;
    }

    private static void CollectClipsFromArray(SerializedProperty arrayProp, Dictionary<string, AnimationClip> clips, string fieldName)
    {
        if (arrayProp == null || !arrayProp.isArray) return;

        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            var element = arrayProp.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue is AnimationClip clip)
            {
                if (!string.IsNullOrEmpty(clip.name) && !clips.ContainsKey(clip.name))
                {
                    clips[clip.name] = clip;
                    Debug.Log($"  Found clip in {fieldName}[{i}]: {clip.name}");
                }
            }
        }
    }
}
