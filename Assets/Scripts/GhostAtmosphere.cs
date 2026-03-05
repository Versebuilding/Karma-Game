using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Runtime URP Volume that adds a ghostly/ethereal atmosphere to the world.
/// Quality-adaptive: heavier effects (film grain, chromatic aberration) on PC,
/// lighter on mobile for performance.
///
/// Creates a global Volume at priority 50 (below dialogue DoF at 100).
/// Always active — this is the world's ambient atmosphere.
///
/// Effects:
///   - Color Adjustments: desaturation + cool temperature for ghostly tone
///   - Bloom: soft ethereal glow
///   - Vignette: darkened edges for focus
///   - Film Grain: subtle noise (PC only)
///   - Chromatic Aberration: very subtle fringing (PC only)
///
/// Attach to GameManagers or any persistent object.
/// </summary>
public class GhostAtmosphere : MonoBehaviour
{
    // ─── Settings ─────────────────────────────────────────────
    [Header("Color")]
    [Tooltip("Color saturation offset (negative = desaturated, ghostly)")]
    [Range(-100f, 0f)]
    [SerializeField] private float saturation = -30f;

    [Tooltip("Color temperature offset (negative = cooler/blue)")]
    [Range(-50f, 0f)]
    [SerializeField] private float temperature = -15f;

    [Header("Bloom")]
    [Tooltip("Bloom threshold (lower = more glow)")]
    [Range(0.5f, 2f)]
    [SerializeField] private float bloomThreshold = 0.8f;

    [Tooltip("Bloom intensity")]
    [Range(0f, 2f)]
    [SerializeField] private float bloomIntensity = 0.6f;

    [Tooltip("Bloom scatter (spread of glow)")]
    [Range(0f, 1f)]
    [SerializeField] private float bloomScatter = 0.5f;

    [Header("Vignette")]
    [Tooltip("Vignette intensity (darkened edges)")]
    [Range(0f, 0.6f)]
    [SerializeField] private float vignetteIntensity = 0.35f;

    [Header("Film Grain (PC Only)")]
    [Tooltip("Film grain intensity (subtle noise)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float filmGrainIntensity = 0.25f;

    [Header("Chromatic Aberration (PC Only)")]
    [Tooltip("Chromatic aberration intensity (color fringing)")]
    [Range(0f, 0.2f)]
    [SerializeField] private float chromaticAberrationIntensity = 0.08f;

    // ─── Runtime ──────────────────────────────────────────────
    private Volume atmosphereVolume;

    // ─── Unity Lifecycle ──────────────────────────────────────

    void Awake()
    {
        SetupAtmosphere();
    }

    void OnDestroy()
    {
        // Clean up runtime VolumeProfile to avoid memory leak
        if (atmosphereVolume != null && atmosphereVolume.profile != null)
            Destroy(atmosphereVolume.profile);
    }

    // ─── Setup ────────────────────────────────────────────────

    private void SetupAtmosphere()
    {
        bool lowQuality = IsLowQuality();

        // Create a global Volume for atmosphere effects
        var volumeObj = new GameObject("GhostAtmosphereVolume");
        volumeObj.transform.SetParent(transform, false);
        volumeObj.hideFlags = HideFlags.HideAndDontSave;

        atmosphereVolume = volumeObj.AddComponent<Volume>();
        atmosphereVolume.isGlobal = true;
        atmosphereVolume.priority = 50;  // Below dialogue DoF (100)
        atmosphereVolume.weight = 1f;    // Always active

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        // ── Bloom: soft ethereal glow (only filter kept — not a color alteration) ──
        var bloom = profile.Add<Bloom>();
        bloom.threshold.Override(bloomThreshold);
        bloom.intensity.Override(lowQuality ? bloomIntensity * 0.5f : bloomIntensity);
        bloom.scatter.Override(bloomScatter);

        // NOTE: Desaturation, vignette, film grain, chromatic aberration, white balance,
        // and color filter have been removed to let world colors render naturally.

        atmosphereVolume.profile = profile;

        Debug.Log($"GhostAtmosphere: Initialized ({(lowQuality ? "Mobile" : "PC")} quality)");
    }

    // ─── Quality Detection ────────────────────────────────────

    /// <summary>
    /// Returns true if running on a low-quality tier (mobile URP asset or low VRAM).
    /// Used to skip expensive effects like Film Grain and Chromatic Aberration.
    /// </summary>
    private bool IsLowQuality()
    {
        var rpAsset = GraphicsSettings.currentRenderPipeline;
        if (rpAsset != null && rpAsset.name.Contains("Mobile"))
            return true;
        return SystemInfo.graphicsMemorySize < 2048;
    }
}
