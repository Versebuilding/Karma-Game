using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration for TTS voice generation. Maps NPC speaker names to Kokoro voice IDs.
/// Create via: Right-click > Create > Karma > TTS Voice Config
/// </summary>
[CreateAssetMenu(fileName = "TTSVoiceConfig", menuName = "Karma/TTS Voice Config", order = 10)]
public class TTSVoiceConfig : ScriptableObject
{
    [Tooltip("Kokoro server URL (run locally via Docker)")]
    public string serverUrl = "http://localhost:8880"; // Start server: docker run -d -p 8880:8880 ghcr.io/remsky/kokoro-fastapi-cpu:latest

    [Tooltip("Audio generation speed multiplier (1.0 = normal)")]
    [Range(0.5f, 2.0f)]
    public float speed = 1.0f;

    [Tooltip("Map each NPC speaker name to a Kokoro voice ID")]
    public List<VoiceMapping> voiceMappings = new List<VoiceMapping>();

    [Tooltip("Default voice ID used when no mapping is found for a speaker")]
    public string defaultVoiceId = "af_heart";

    [Tooltip("Speakers to skip TTS generation for (e.g. the player character)")]
    public List<string> skipSpeakers = new List<string>();

    /// <summary>Get the Kokoro voice ID for a given speaker name.</summary>
    public string GetVoiceId(string speakerName)
    {
        if (string.IsNullOrEmpty(speakerName)) return defaultVoiceId;

        foreach (var mapping in voiceMappings)
        {
            if (string.Equals(mapping.speakerName, speakerName, StringComparison.OrdinalIgnoreCase))
                return mapping.voiceId;
        }
        return defaultVoiceId;
    }

    /// <summary>Check if a speaker should be skipped (e.g. player character).</summary>
    public bool ShouldSkip(string speakerName)
    {
        if (string.IsNullOrEmpty(speakerName)) return true;

        foreach (var skip in skipSpeakers)
        {
            if (string.Equals(skip, speakerName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

[Serializable]
public class VoiceMapping
{
    [Tooltip("NPC speaker name (must match speakerName in DialogueSO nodes)")]
    public string speakerName;

    [Tooltip("Kokoro voice ID (e.g. af_heart, am_adam, af_bella)")]
    public string voiceId;
}
