using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool to create the SparkleVFX prefab.
/// Menu: Karma > Create Sparkle VFX Prefab
///
/// Creates a GameObject with ParticleSystem + SparkleVFX component,
/// saves it as a prefab, and auto-assigns it to SparkleVFXManager if present.
/// </summary>
public class VFXPrefabCreator
{
    [MenuItem("Karma/Create Sparkle VFX Prefab")]
    public static void CreateSparkleVFXPrefab()
    {
        Debug.Log("── Creating Sparkle VFX Prefab ──────────────────────");

        // Ensure directory exists
        if (!AssetDatabase.IsValidFolder("Assets/Prefab/VFX"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
                AssetDatabase.CreateFolder("Assets", "Prefab");
            AssetDatabase.CreateFolder("Assets/Prefab", "VFX");
        }

        string prefabPath = "Assets/Prefab/VFX/SparkleVFX.prefab";

        // Check if prefab already exists
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null)
        {
            Debug.Log($"  SparkleVFX prefab already exists at: {prefabPath}");
            Debug.Log("  Delete it first if you want to recreate.");
            Selection.activeObject = existing;
            return;
        }

        // Create temporary GameObject
        GameObject sparkleObj = new GameObject("SparkleVFX");

        // Add ParticleSystem (required by SparkleVFX)
        var ps = sparkleObj.AddComponent<ParticleSystem>();

        // Stop it from playing in editor
        var main = ps.main;
        main.playOnAwake = false;

        // Add SparkleVFX component (configures PS at runtime)
        sparkleObj.AddComponent<SparkleVFX>();

        // Save as prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(sparkleObj, prefabPath);

        // Cleanup temp object
        Object.DestroyImmediate(sparkleObj);

        if (prefab != null)
        {
            Debug.Log($"  Created SparkleVFX prefab at: {prefabPath}");

            // Auto-assign to SparkleVFXManager in scene
            var manager = Object.FindFirstObjectByType<SparkleVFXManager>();
            if (manager != null)
            {
                var so = new SerializedObject(manager);
                var prefabProp = so.FindProperty("sparklePrefab");
                if (prefabProp != null)
                {
                    prefabProp.objectReferenceValue = prefab;
                    so.ApplyModifiedProperties();
                    Debug.Log("  Auto-assigned to SparkleVFXManager in scene!");
                }
            }
            else
            {
                Debug.Log("  No SparkleVFXManager in scene. Run: Karma > Setup Game Systems");
            }

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        else
        {
            Debug.LogError("  Failed to create SparkleVFX prefab!");
        }
    }
}
