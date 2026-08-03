using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AnandaAnimationImporter
{
    static readonly string AnimFolder = "Assets/3D/Character/Ananda/animations";

    static readonly Dictionary<string, bool> LoopingClips = new Dictionary<string, bool>
    {
        { "idleSit1", true }, { "idleSit2", true }, { "idleSit3", true },
        { "idleStand1", true }, { "idleStand2", true }, { "idleStand3", true },
        { "sitTalk1", true }, { "sitTalk2", true }, { "sitTalk3", true },
        { "standTalk1", true }, { "standTalk2", true }, { "standTalk3", true },
        { "walk", true },
        { "sitMeditate", true }, { "sitMeditateEnter", false }, { "sitMeditateExit", false },
        { "floatMeditate", true }, { "floatMeditateEnter", false }, { "floatMeditateExit", false },
        { "nod", false }, { "headShake", false }, { "bow", false },
        { "surprise", false }, { "laugh", false },
    };

    [MenuItem("Karma/Import Ananda Animations")]
    public static void ConfigureAnandaAnimations()
    {
        string[] fbxFiles = Directory.GetFiles(AnimFolder, "*.fbx");
        int count = 0;

        foreach (string filePath in fbxFiles)
        {
            string assetPath = filePath.Replace("\\", "/");
            string fileName = Path.GetFileNameWithoutExtension(assetPath);

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"Could not get importer for {assetPath}");
                continue;
            }

            // Set to Generic animation type
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;

            // Configure the clip
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"No default clips found in {fileName}, creating manually");
                clips = new ModelImporterClipAnimation[1];
                clips[0] = new ModelImporterClipAnimation();
                clips[0].name = fileName;
                clips[0].takeName = importer.importedTakeInfos != null && importer.importedTakeInfos.Length > 0
                    ? importer.importedTakeInfos[0].name
                    : fileName;
                clips[0].firstFrame = 0;
                clips[0].lastFrame = 1;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = fileName;

                bool shouldLoop = LoopingClips.ContainsKey(fileName) && LoopingClips[fileName];
                clips[i].loopTime = shouldLoop;
                clips[i].loopPose = shouldLoop;
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            count++;
            Debug.Log($"Configured: {fileName} (loop={LoopingClips.GetValueOrDefault(fileName, false)})");
        }

        Debug.Log($"Configured {count} Ananda animation FBX files.");
        AssetDatabase.Refresh();
    }
}
