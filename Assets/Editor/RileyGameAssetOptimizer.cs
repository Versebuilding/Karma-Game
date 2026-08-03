using UnityEditor;
using UnityEngine;

/// <summary>
/// Auto-optimizes all Riley Game FBX assets for mobile on import.
/// Applies mesh compression, disables unused features, and sets
/// ASTC texture compression to minimize file size and runtime memory.
/// </summary>
public class RileyGameAssetOptimizer : AssetPostprocessor
{
    static readonly string[] RileyPaths = {
        "Assets/3D/RileyGame_Phase1",
        "Assets/3D/RileyGame_Phase2",
        "Assets/3D/RileyGame_Phase3",
        "Assets/3D/RileyGame_Tracks",
        "Assets/3D/RileyGame_Pancreas"
    };

    bool IsRileyAsset => System.Array.Exists(RileyPaths, p => assetPath.StartsWith(p));

    // ─── MODEL IMPORT ───────────────────────────────────────────
    void OnPreprocessModel()
    {
        if (!IsRileyAsset) return;

        var importer = assetImporter as ModelImporter;
        if (importer == null) return;

        // ── Mesh optimization ──
        importer.meshCompression       = ModelImporterMeshCompression.Medium;
        importer.isReadable            = false;   // saves runtime RAM
        importer.meshOptimizationFlags = MeshOptimizationFlags.Everything;
        importer.optimizeMeshPolygons  = true;
        importer.optimizeMeshVertices  = true;
        importer.weldVertices          = true;
        importer.importNormals         = ModelImporterNormals.Calculate; // recalc = smaller file
        importer.normalCalculationMode = ModelImporterNormalCalculationMode.AngleWeighted;
        importer.normalSmoothingAngle  = 60f;     // candy aesthetic = smooth shading
        importer.importTangents        = ModelImporterTangents.None;    // no normal maps

        // ── Strip animations (all static meshes) ──
        importer.animationType   = ModelImporterAnimationType.None;
        importer.importAnimation = false;

        // ── Strip unused data ──
        importer.importCameras    = false;
        importer.importLights     = false;
        importer.importBlendShapes = false;
        importer.importVisibility  = false;

        // ── Materials: use Unity defaults, don't import FBX materials ──
        importer.materialImportMode = ModelImporterMaterialImportMode.None;

        // ── Generate lightmap UVs for baked lighting (mobile-friendly) ──
        importer.generateSecondaryUV = true;

        // ── Scale: FBX was exported at 1 unit = 1 meter ──
        importer.globalScale    = 1f;
        importer.useFileScale   = true;
        importer.importConstraints = false;

        Debug.Log($"[RileyGame] Optimized model import: {assetPath}");
    }

    // ─── TEXTURE IMPORT ─────────────────────────────────────────
    void OnPreprocessTexture()
    {
        if (!IsRileyAsset) return;

        var importer = assetImporter as TextureImporter;
        if (importer == null) return;

        // ── Shared settings ──
        importer.textureType       = TextureImporterType.Default;
        importer.sRGBTexture       = true;
        importer.mipmapEnabled     = true;
        importer.streamingMipmaps  = true;        // stream from disk on mobile
        importer.isReadable        = false;
        importer.textureCompression = TextureImporterCompression.Compressed;

        // ── Size based on asset type ──
        bool isTerrain = assetPath.Contains("Terrain");
        int maxSize = isTerrain ? 512 : 256;  // props/islands get 256, terrain 512

        importer.maxTextureSize = maxSize;

        // ── Android: ASTC 6x6 (best size/quality for mobile) ──
        var android = new TextureImporterPlatformSettings {
            name                 = "Android",
            overridden           = true,
            maxTextureSize       = maxSize,
            format               = TextureImporterFormat.ASTC_6x6,
            compressionQuality   = (int)TextureCompressionQuality.Normal
        };
        importer.SetPlatformTextureSettings(android);

        // ── iOS: ASTC 6x6 ──
        var ios = new TextureImporterPlatformSettings {
            name                 = "iPhone",
            overridden           = true,
            maxTextureSize       = maxSize,
            format               = TextureImporterFormat.ASTC_6x6,
            compressionQuality   = (int)TextureCompressionQuality.Normal
        };
        importer.SetPlatformTextureSettings(ios);

        Debug.Log($"[RileyGame] Optimized texture: {assetPath} → {maxSize}px ASTC_6x6");
    }

    // ─── BULK RE-IMPORT MENU ────────────────────────────────────
    [MenuItem("Riley Game/Optimize All Riley Assets (Re-import)")]
    static void ReimportAllRileyAssets()
    {
        int count = 0;
        foreach (string folder in RileyPaths)
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }
        }
        Debug.Log($"[RileyGame] Re-imported and optimized {count} assets");
        EditorUtility.DisplayDialog("Riley Game Optimizer",
            $"Re-imported {count} FBX assets with mobile optimizations.\n\n" +
            "• Mesh compression: Medium\n" +
            "• Read/Write: OFF\n" +
            "• Animations: stripped\n" +
            "• Textures: ASTC 6×6\n" +
            "• Tangents: stripped\n" +
            "• Lightmap UVs: generated",
            "OK");
    }

    // ─── FILE SIZE REPORT ───────────────────────────────────────
    [MenuItem("Riley Game/Show Asset Size Report")]
    static void ShowSizeReport()
    {
        long totalBytes = 0;
        int fileCount = 0;
        var report = new System.Text.StringBuilder();
        report.AppendLine("Riley Game Asset Size Report");
        report.AppendLine("═══════════════════════════════");

        foreach (string folder in RileyPaths)
        {
            string fullPath = System.IO.Path.Combine(Application.dataPath,
                folder.Replace("Assets/", ""));
            if (!System.IO.Directory.Exists(fullPath)) continue;

            long folderBytes = 0;
            var files = System.IO.Directory.GetFiles(fullPath, "*.fbx");
            foreach (var f in files)
            {
                long size = new System.IO.FileInfo(f).Length;
                folderBytes += size;
                fileCount++;
            }
            totalBytes += folderBytes;
            report.AppendLine($"  {folder}: {fileCount} files, {folderBytes / 1024f:F0} KB");
        }

        report.AppendLine($"\n  TOTAL: {fileCount} FBX files, {totalBytes / 1024f:F0} KB ({totalBytes / (1024f * 1024f):F1} MB)");
        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog("Riley Game Size Report", report.ToString(), "OK");
    }
}
