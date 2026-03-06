using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Editor helper for FoodRainManager.
/// Menu: Karma > Setup Food Rain
/// Menu: Karma > Fix Food FBX Materials (diagnose and fix FBX import settings)
/// Also adds a context menu on FoodRainManager to auto-load prefabs.
/// </summary>
public static class FoodRainSetup
{
    private const string FoodPrefabFolder = "Assets/Prefab/Environment/Food";
    private const string FoodFBXFolder = "Assets/3D/Environment/Food";

    /// <summary>
    /// Menu item: Creates a FoodRainManager in the scene and auto-loads all food prefabs.
    /// </summary>
    [MenuItem("Karma/Setup Food Rain")]
    public static void SetupFoodRain()
    {
        // Find or create
        FoodRainManager existing = Object.FindFirstObjectByType<FoodRainManager>();
        if (existing != null)
        {
            LoadFoodPrefabs(existing);
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("FoodRainManager: Updated existing manager with food prefabs.");
            return;
        }

        // Create new
        GameObject obj = new GameObject("FoodRainManager");
        var manager = obj.AddComponent<FoodRainManager>();
        LoadFoodPrefabs(manager);

        Undo.RegisterCreatedObjectUndo(obj, "Create Food Rain Manager");
        Selection.activeGameObject = obj;

        Debug.Log($"FoodRainManager: Created and loaded food prefabs from {FoodPrefabFolder}");
    }

    /// <summary>
    /// Menu item: Diagnose and fix FBX import settings for food models.
    /// Checks each FBX for meshes, materials, and proper URP compatibility.
    /// </summary>
    [MenuItem("Karma/Fix Food FBX Materials")]
    public static void FixFoodFBXMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { FoodFBXFolder });

        if (guids.Length == 0)
        {
            Debug.LogWarning($"FoodRainSetup: No models found in {FoodFBXFolder}!");
            return;
        }

        int fixedCount = 0;
        int totalModels = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

            totalModels++;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            // Load the model to check its state
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                Debug.LogWarning($"  [{path}] Could not load model!");
                continue;
            }

            // Check renderers and materials
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);

            int rendererCount = renderers.Length;
            int materialCount = 0;
            int nullMaterialCount = 0;
            string shaderInfo = "none";

            foreach (var r in renderers)
            {
                if (r.sharedMaterial != null)
                {
                    materialCount++;
                    shaderInfo = r.sharedMaterial.shader.name;
                }
                else
                {
                    nullMaterialCount++;
                }
            }

            // Check mesh bounds for size
            float maxSize = 0f;
            int vertCount = 0;
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                {
                    float s = mf.sharedMesh.bounds.size.magnitude;
                    if (s > maxSize) maxSize = s;
                    vertCount += mf.sharedMesh.vertexCount;
                }
            }

            string status = $"  [{System.IO.Path.GetFileName(path)}] " +
                $"renderers={rendererCount} materials={materialCount} nullMats={nullMaterialCount} " +
                $"meshes={meshFilters.Length} verts={vertCount} " +
                $"meshBounds={maxSize:F4} " +
                $"shader={shaderInfo} " +
                $"importMode={importer.materialImportMode}";

            // Fix: if materials are missing, try changing import mode
            bool needsFix = false;

            if (materialCount == 0 && rendererCount > 0)
            {
                needsFix = true;
                status += " ← MISSING MATERIALS";
            }
            else if (nullMaterialCount > 0)
            {
                needsFix = true;
                status += " ← SOME NULL MATERIALS";
            }
            else if (rendererCount == 0 && meshFilters.Length > 0)
            {
                needsFix = true;
                status += " ← HAS MESHES BUT NO RENDERERS";
            }

            // Check if shader is non-URP
            if (materialCount > 0 && shaderInfo.Contains("Standard") && !shaderInfo.Contains("Universal"))
            {
                needsFix = true;
                status += " ← NON-URP SHADER";
            }

            Debug.Log(status);

            if (needsFix)
            {
                // Try toggling material import mode to force re-generation
                var originalMode = importer.materialImportMode;

                // Set to ImportViaMaterialDescription (1) which uses the render pipeline's defaults
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

                // Extract textures if embedded
                importer.ExtractTextures(System.IO.Path.GetDirectoryName(path));

                importer.SaveAndReimport();
                fixedCount++;

                Debug.Log($"    → Fixed: Changed materialImportMode from {originalMode} to ImportViaMaterialDescription. Reimported.");
            }
        }

        Debug.Log($"FoodRainSetup: Diagnosed {totalModels} food models, fixed {fixedCount}.");

        if (fixedCount > 0)
        {
            AssetDatabase.Refresh();
            Debug.Log("FoodRainSetup: Asset database refreshed. Check food models in Inspector for materials.");
        }
    }

    /// <summary>
    /// Load all prefabs from the food folder into the FoodRainManager's foodPrefabs array.
    /// </summary>
    private static void LoadFoodPrefabs(FoodRainManager manager)
    {
        // Find all prefab GUIDs in the food folder
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { FoodPrefabFolder });

        if (guids.Length == 0)
        {
            Debug.LogWarning($"FoodRainSetup: No prefabs found in {FoodPrefabFolder}!");
            return;
        }

        List<GameObject> prefabs = new List<GameObject>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                prefabs.Add(prefab);
        }

        // Use SerializedObject to set the private field
        SerializedObject so = new SerializedObject(manager);
        SerializedProperty prop = so.FindProperty("foodPrefabs");
        prop.arraySize = prefabs.Count;

        for (int i = 0; i < prefabs.Count; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);

        Debug.Log($"FoodRainSetup: Loaded {prefabs.Count} food prefabs: " +
            string.Join(", ", prefabs.ConvertAll(p => p.name)));
    }
}

/// <summary>
/// Custom Inspector for FoodRainManager — adds "Load Food Prefabs" button.
/// </summary>
[CustomEditor(typeof(FoodRainManager))]
public class FoodRainManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        // Fix FBX materials button (one-click fix for invisible food)
        if (GUILayout.Button("Fix Food FBX Materials (if invisible)", GUILayout.Height(25)))
        {
            FoodRainSetup.FixFoodFBXMaterials();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("Load Food Prefabs from Folder", GUILayout.Height(30)))
        {
            FoodRainManager manager = (FoodRainManager)target;

            string[] guids = AssetDatabase.FindAssets("t:Prefab",
                new[] { "Assets/Prefab/Environment/Food" });

            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Food Rain",
                    "No prefabs found in Assets/Prefab/Environment/Food!", "OK");
                return;
            }

            var prefabs = new List<GameObject>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    prefabs.Add(prefab);
            }

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty prop = so.FindProperty("foodPrefabs");
            prop.arraySize = prefabs.Count;

            for (int i = 0; i < prefabs.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);

            Debug.Log($"Loaded {prefabs.Count} food prefabs.");
        }

        // Runtime controls
        if (Application.isPlaying)
        {
            FoodRainManager manager = (FoodRainManager)target;
            GUILayout.Space(5);
            EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(manager.IsRaining ? "Stop Rain" : "Start Rain"))
            {
                if (manager.IsRaining)
                    manager.StopRain();
                else
                    manager.StartRain();
            }
            if (GUILayout.Button("Clear All"))
            {
                manager.StopRainImmediate();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
