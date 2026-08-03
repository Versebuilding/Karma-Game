using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Editor tool to batch-generate NPC voice audio using Kokoro TTS.
/// Menu: Tools > Generate NPC Voice Audio
///
/// Prerequisites:
///   1. Docker Desktop running
///   2. Kokoro server: docker run -d -p 8880:8880 remsky/kokoro-fastapi:latest
///   3. TTSVoiceConfig asset at Assets/Data/TTSVoiceConfig with voice mappings
///
/// Workflow:
///   - Scans all DialogueSO assets in Assets/Data/Dialogues/
///   - For each node/choice without a voiceClip (or with changed text), generates audio
///   - Saves mp3 to Assets/Audio/Generated/{dialogueId}/
///   - Assigns the imported AudioClip to the voiceClip field
/// </summary>
public class TTSBatchGenerator : EditorWindow
{
    private TTSVoiceConfig config;
    private bool forceRegenerate;
    private Vector2 scrollPos;
    private string statusMessage = "";
    private int totalNodes;
    private int generatedCount;
    private int skippedCount;
    private int errorCount;
    private bool isGenerating;

    private const string GENERATED_AUDIO_ROOT = "Assets/Audio/Generated";
    private const string HASH_FOLDER = "Assets/Audio/Generated/.hashes";

    [MenuItem("Tools/Generate NPC Voice Audio")]
    public static void ShowWindow()
    {
        var window = GetWindow<TTSBatchGenerator>("TTS Voice Generator");
        window.minSize = new Vector2(400, 300);
    }

    private void OnGUI()
    {
        GUILayout.Label("Kokoro TTS Voice Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        config = (TTSVoiceConfig)EditorGUILayout.ObjectField(
            "Voice Config", config, typeof(TTSVoiceConfig), false);

        if (config == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a TTSVoiceConfig asset. Create one via:\nRight-click > Create > Karma > TTS Voice Config",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Server URL", config.serverUrl);
        EditorGUILayout.LabelField("Default Voice", config.defaultVoiceId);
        EditorGUILayout.LabelField("Speed", config.speed.ToString("F1"));

        EditorGUILayout.Space();
        forceRegenerate = EditorGUILayout.Toggle(
            new GUIContent("Force Regenerate All",
                "Regenerate even if audio already exists and text hasn't changed"),
            forceRegenerate);

        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(isGenerating);
        if (GUILayout.Button("Generate Voice Audio", GUILayout.Height(35)))
        {
            GenerateAll();
        }
        EditorGUI.EndDisabledGroup();

        if (isGenerating)
        {
            float progress = totalNodes > 0 ? (float)(generatedCount + skippedCount + errorCount) / totalNodes : 0;
            EditorGUI.ProgressBar(
                EditorGUILayout.GetControlRect(false, 20),
                progress,
                $"Processing... {generatedCount + skippedCount + errorCount}/{totalNodes}");
        }

        EditorGUILayout.Space();
        if (!string.IsNullOrEmpty(statusMessage))
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }

    private void GenerateAll()
    {
        isGenerating = true;
        generatedCount = 0;
        skippedCount = 0;
        errorCount = 0;
        var log = new StringBuilder();

        try
        {
            // Verify server is reachable
            if (!IsServerReachable())
            {
                statusMessage = "ERROR: Cannot reach Kokoro server at " + config.serverUrl +
                    "\n\nMake sure Docker is running and Kokoro is started:\n" +
                    "  docker run -d -p 8880:8880 remsky/kokoro-fastapi:latest";
                isGenerating = false;
                return;
            }

            // Ensure output directories exist
            EnsureDirectory(GENERATED_AUDIO_ROOT);
            EnsureDirectory(HASH_FOLDER);

            // Find all DialogueSO assets
            string[] guids = AssetDatabase.FindAssets("t:DialogueSO", new[] { "Assets/Data/Dialogues" });
            if (guids.Length == 0)
            {
                statusMessage = "No DialogueSO assets found in Assets/Data/Dialogues/";
                isGenerating = false;
                return;
            }

            // Count total work
            var dialogues = new List<DialogueSO>();
            totalNodes = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var dialogue = AssetDatabase.LoadAssetAtPath<DialogueSO>(path);
                if (dialogue != null && dialogue.nodes != null)
                {
                    dialogues.Add(dialogue);
                    foreach (var node in dialogue.nodes)
                    {
                        if (!config.ShouldSkip(node.speakerName))
                            totalNodes++;
                        if (node.choices != null)
                        {
                            foreach (var choice in node.choices)
                            {
                                // Count choices that have associated text spoken by NPCs
                                // (player choices typically aren't voiced by TTS)
                            }
                        }
                    }
                }
            }

            log.AppendLine($"Found {dialogues.Count} dialogue(s), {totalNodes} node(s) to process.\n");

            // Process each dialogue
            foreach (var dialogue in dialogues)
            {
                string dialoguePath = AssetDatabase.GetAssetPath(dialogue);
                var serializedObj = new SerializedObject(dialogue);
                string dialogueFolder = $"{GENERATED_AUDIO_ROOT}/{SanitizeFileName(dialogue.dialogueId)}";
                EnsureDirectory(dialogueFolder);

                log.AppendLine($"--- {dialogue.dialogueId} ---");

                var nodesProperty = serializedObj.FindProperty("nodes");

                for (int i = 0; i < dialogue.nodes.Length; i++)
                {
                    var node = dialogue.nodes[i];

                    if (config.ShouldSkip(node.speakerName))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(node.dialogueText))
                    {
                        skippedCount++;
                        continue;
                    }

                    string voiceId = config.GetVoiceId(node.speakerName);
                    string textHash = ComputeHash(node.dialogueText + "|" + voiceId);
                    string clipFileName = $"{SanitizeFileName(node.nodeId)}.mp3";
                    string clipAssetPath = $"{dialogueFolder}/{clipFileName}";
                    string hashFilePath = $"{HASH_FOLDER}/{dialogue.dialogueId}_{node.nodeId}.hash";

                    // Check if we can skip
                    bool needsGeneration = forceRegenerate
                        || !File.Exists(clipAssetPath)
                        || !HashMatches(hashFilePath, textHash);

                    if (!needsGeneration && node.voiceClip != null)
                    {
                        skippedCount++;
                        log.AppendLine($"  [{node.nodeId}] Skipped (up-to-date)");
                        continue;
                    }

                    // Show progress
                    float progress = (float)(generatedCount + skippedCount + errorCount) / totalNodes;
                    if (EditorUtility.DisplayCancelableProgressBar(
                        "Generating Voice Audio",
                        $"[{dialogue.dialogueId}] {node.nodeId} ({node.speakerName})",
                        progress))
                    {
                        log.AppendLine("\n--- CANCELLED BY USER ---");
                        break;
                    }

                    // Generate audio via Kokoro API
                    byte[] audioBytes = CallKokoroAPI(node.dialogueText, voiceId);
                    if (audioBytes == null || audioBytes.Length == 0)
                    {
                        errorCount++;
                        log.AppendLine($"  [{node.nodeId}] ERROR: Failed to generate audio");
                        continue;
                    }

                    // Save mp3 file
                    string fullPath = Path.Combine(
                        Directory.GetParent(Application.dataPath).FullName,
                        clipAssetPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    File.WriteAllBytes(fullPath, audioBytes);

                    // Save text hash for incremental builds
                    string hashFullPath = Path.Combine(
                        Directory.GetParent(Application.dataPath).FullName,
                        hashFilePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(hashFullPath));
                    File.WriteAllText(hashFullPath, textHash);

                    // Import the asset
                    AssetDatabase.ImportAsset(clipAssetPath, ImportAssetOptions.ForceUpdate);

                    // Configure import settings for mobile
                    var importer = AssetImporter.GetAtPath(clipAssetPath) as AudioImporter;
                    if (importer != null)
                    {
                        var sampleSettings = importer.defaultSampleSettings;
                        sampleSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                        sampleSettings.quality = 0.7f;
                        sampleSettings.loadType = AudioClipLoadType.CompressedInMemory;
                        importer.defaultSampleSettings = sampleSettings;
                        importer.SaveAndReimport();
                    }

                    // Assign the clip to the voiceClip field
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipAssetPath);
                    if (clip != null)
                    {
                        var nodeElement = nodesProperty.GetArrayElementAtIndex(i);
                        var voiceClipProp = nodeElement.FindPropertyRelative("voiceClip");
                        voiceClipProp.objectReferenceValue = clip;
                        generatedCount++;
                        log.AppendLine($"  [{node.nodeId}] Generated ({node.speakerName} -> {voiceId})");
                    }
                    else
                    {
                        errorCount++;
                        log.AppendLine($"  [{node.nodeId}] ERROR: Could not load imported clip");
                    }
                }

                serializedObj.ApplyModifiedProperties();
                EditorUtility.SetDirty(dialogue);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine($"\n=== DONE ===");
            log.AppendLine($"Generated: {generatedCount}");
            log.AppendLine($"Skipped: {skippedCount}");
            log.AppendLine($"Errors: {errorCount}");
        }
        catch (Exception ex)
        {
            log.AppendLine($"\nEXCEPTION: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            isGenerating = false;
            statusMessage = log.ToString();
            Repaint();
        }
    }

    private bool IsServerReachable()
    {
        try
        {
            // Quick health check — try the models endpoint
            var request = UnityWebRequest.Get(config.serverUrl + "/v1/models");
            request.timeout = 5;
            var op = request.SendWebRequest();

            // Spin-wait in editor context (acceptable for a short health check)
            while (!op.isDone) { }

            bool ok = request.result == UnityWebRequest.Result.Success;
            request.Dispose();
            return ok;
        }
        catch
        {
            return false;
        }
    }

    private byte[] CallKokoroAPI(string text, string voiceId)
    {
        try
        {
            // Kokoro FastAPI uses OpenAI-compatible endpoint
            string url = config.serverUrl + "/v1/audio/speech";
            string jsonBody = JsonUtility.ToJson(new KokoroRequest
            {
                model = "kokoro",
                input = text,
                voice = voiceId,
                response_format = "mp3",
                speed = config.speed
            });

            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;

            var op = request.SendWebRequest();
            while (!op.isDone) { }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Kokoro API error: {request.error} — {request.downloadHandler.text}");
                request.Dispose();
                return null;
            }

            byte[] data = request.downloadHandler.data;
            request.Dispose();
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Kokoro API exception: {ex.Message}");
            return null;
        }
    }

    private static string ComputeHash(string input)
    {
        using (var md5 = MD5.Create())
        {
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(32);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private static bool HashMatches(string hashFilePath, string expectedHash)
    {
        string fullPath = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            hashFilePath);
        if (!File.Exists(fullPath)) return false;
        return File.ReadAllText(fullPath).Trim() == expectedHash;
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "unnamed";
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Replace(' ', '_');
    }

    private static void EnsureDirectory(string assetPath)
    {
        string fullPath = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            assetPath);
        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);
    }

    [Serializable]
    private class KokoroRequest
    {
        public string model;
        public string input;
        public string voice;
        public string response_format;
        public float speed;
    }
}
