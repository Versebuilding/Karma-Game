using System.Collections;
using UnityEngine;

/// <summary>
/// Reusable sparkle burst VFX with faded yellow orbs.
/// Configures a ParticleSystem entirely in code — just add this component
/// and call Play(). Auto-deactivates when done (for object pooling).
///
/// Usage:
///   - Drag the prefab (created via Karma > Create Sparkle VFX Prefab)
///   - SparkleVFXManager handles pooling and spawning
///   - Or manually: sparkleVFX.Play(worldPosition);
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class SparkleVFX : MonoBehaviour
{
    // ─── Settings ────────────────────────────────────────────
    [Header("Burst")]
    [Tooltip("Number of particles in the burst")]
    [Range(5, 60)]
    [SerializeField] private int burstCount = 25;

    [Tooltip("Duration of the entire effect")]
    [Range(0.3f, 2f)]
    [SerializeField] private float effectDuration = 0.8f;

    [Header("Particles")]
    [Tooltip("Minimum particle lifetime")]
    [Range(0.1f, 2f)]
    [SerializeField] private float lifetimeMin = 0.4f;

    [Tooltip("Maximum particle lifetime")]
    [Range(0.2f, 3f)]
    [SerializeField] private float lifetimeMax = 0.8f;

    [Tooltip("Minimum particle size")]
    [Range(0.02f, 0.5f)]
    [SerializeField] private float sizeMin = 0.08f;

    [Tooltip("Maximum particle size")]
    [Range(0.05f, 1f)]
    [SerializeField] private float sizeMax = 0.2f;

    [Header("Color")]
    [Tooltip("Sparkle color (faded yellow orbs by default)")]
    [SerializeField] private Color sparkleColor = new Color(1f, 0.95f, 0.5f, 0.7f);

    [Header("Shape")]
    [Tooltip("Burst sphere radius")]
    [Range(0.1f, 2f)]
    [SerializeField] private float burstRadius = 0.3f;

    [Tooltip("Outward speed of particles")]
    [Range(0.5f, 8f)]
    [SerializeField] private float burstSpeed = 2f;

    [Header("Pooling")]
    [Tooltip("Auto-deactivate when particles finish (for pool reuse)")]
    [SerializeField] private bool autoDeactivate = true;

    // ─── Runtime ─────────────────────────────────────────────
    private ParticleSystem ps;
    private bool isConfigured;
    private Coroutine deactivateCoroutine;

    /// <summary>Whether particles are currently playing.</summary>
    public bool IsPlaying => ps != null && ps.isPlaying;

    // ─── Unity Lifecycle ─────────────────────────────────────

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        ConfigureParticleSystem();
    }

    // ─── Public API ──────────────────────────────────────────

    /// <summary>Play the sparkle burst at the current position.</summary>
    public void Play()
    {
        if (ps == null) return;

        if (!isConfigured)
            ConfigureParticleSystem();

        if (deactivateCoroutine != null)
            StopCoroutine(deactivateCoroutine);

        gameObject.SetActive(true);
        ps.Clear();
        ps.Play();

        if (autoDeactivate)
            deactivateCoroutine = StartCoroutine(DeactivateAfterFinish());
    }

    /// <summary>Play the sparkle burst at a specific world position.</summary>
    public void Play(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        Play();
    }

    /// <summary>Stop the effect immediately.</summary>
    public void Stop()
    {
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (deactivateCoroutine != null)
        {
            StopCoroutine(deactivateCoroutine);
            deactivateCoroutine = null;
        }
    }

    // ─── Particle System Configuration ───────────────────────

    private void ConfigureParticleSystem()
    {
        if (ps == null) return;

        // ── Main Module ──
        var main = ps.main;
        main.duration = effectDuration;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = sparkleColor;
        main.startSpeed = new ParticleSystem.MinMaxCurve(burstSpeed * 0.5f, burstSpeed);
        main.maxParticles = burstCount + 5;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.2f; // slight upward drift for dreamy feel

        // ── Emission Module ──
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f; // no continuous emission
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, burstCount)
        });

        // ── Shape Module ──
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = burstRadius;

        // ── Color over Lifetime ──
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient alphaFade = new Gradient();
        alphaFade.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.3f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaFade);

        // ── Size over Lifetime ──
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;

        AnimationCurve shrinkCurve = new AnimationCurve();
        shrinkCurve.AddKey(0f, 1f);
        shrinkCurve.AddKey(0.4f, 0.9f);
        shrinkCurve.AddKey(1f, 0.3f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, shrinkCurve);

        // ── Renderer Module ──
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Use Unity's default particle material if none assigned
            if (renderer.sharedMaterial == null)
            {
                renderer.sharedMaterial = GetDefaultParticleMaterial();
            }
        }

        isConfigured = true;
    }

    /// <summary>
    /// Gets a basic additive particle material using Unity's built-in resources.
    /// Falls back to the default particle shader.
    /// </summary>
    private Material GetDefaultParticleMaterial()
    {
        // URP project: try URP particle shader first (most likely to exist)
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Mobile/Particles/Additive");

        if (shader != null)
        {
            Material mat = new Material(shader);
            // URP Particles/Unlit: set surface type to Additive for glow
            mat.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
            mat.SetFloat("_Blend", 1f);   // 0=Alpha, 1=Additive
            return mat;
        }

        // Last resort: try built-in default particle material
        Material builtIn = Resources.GetBuiltinResource<Material>("Default-Particle.mat");
        return builtIn;
    }

    // ─── Auto-Deactivate ─────────────────────────────────────

    private IEnumerator DeactivateAfterFinish()
    {
        // Wait for effect duration + max particle lifetime
        yield return new WaitForSeconds(effectDuration + lifetimeMax + 0.1f);

        deactivateCoroutine = null;
        gameObject.SetActive(false);
    }
}
