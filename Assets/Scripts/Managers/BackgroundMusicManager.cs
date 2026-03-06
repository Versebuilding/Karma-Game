using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton manager for background music with automatic volume ducking.
///
/// Plays a background music track on loop. Automatically detects when other
/// AudioSources in the scene are playing and smoothly ducks (lowers) the music
/// volume. When all other audio stops, the music smoothly returns to normal volume.
///
/// How it works:
///   - A coroutine periodically scans all AudioSources in the scene (~every 0.15s)
///   - If any non-music AudioSource is playing → smoothly fades to duckedVolume
///   - When all other sources stop → smoothly fades back to normalVolume
///   - AudioSource cache is refreshed every ~2s to catch dynamically created sources
///     (e.g. AudioSource.PlayClipAtPoint creates temporary AudioSources)
///
/// Setup:
///   1. Run 'Karma > Setup Game Systems' — auto-creates and assigns the track
///   2. Or manually: add to GameManagers, drag GameBackgroundTrack into Inspector
///
/// Zero changes needed to existing audio scripts — fully self-contained.
/// </summary>
public class BackgroundMusicManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────
    public static BackgroundMusicManager Instance { get; private set; }

    // ─── Music ──────────────────────────────────────────────────
    [Header("Music")]
    [Tooltip("The background music track to play on loop")]
    [SerializeField] private AudioClip backgroundTrack;

    // ─── Volume ─────────────────────────────────────────────────
    [Header("Volume")]
    [Tooltip("Normal music volume when no other audio is playing")]
    [Range(0f, 1f)]
    [SerializeField] private float normalVolume = 0.4f;

    [Tooltip("Ducked music volume when other audio is playing")]
    [Range(0f, 1f)]
    [SerializeField] private float duckedVolume = 0.15f;

    [Tooltip("How fast the volume fades (units per second). Higher = faster transitions")]
    [Range(0.5f, 10f)]
    [SerializeField] private float duckFadeSpeed = 2f;

    // ─── Detection ──────────────────────────────────────────────
    [Header("Detection")]
    [Tooltip("How often to scan for other playing AudioSources (seconds)")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float scanInterval = 0.15f;

    [Tooltip("How often to refresh the AudioSource cache (seconds). Catches dynamically created sources.")]
    [Range(1f, 5f)]
    [SerializeField] private float cacheRefreshInterval = 2f;

    // ─── Runtime ────────────────────────────────────────────────
    private AudioSource musicSource;
    private AudioSource[] cachedSources;
    private float targetVolume;
    private bool isForceDucked;
    private float lastCacheRefresh;

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════

    /// <summary>Whether the music is currently playing.</summary>
    public bool IsPlaying => musicSource != null && musicSource.isPlaying;

    /// <summary>Whether the music is currently ducked.</summary>
    public bool IsDucked => targetVolume < normalVolume;

    /// <summary>Change the base music volume at runtime.</summary>
    public void SetNormalVolume(float vol)
    {
        normalVolume = Mathf.Clamp01(vol);
        if (!IsDucked && !isForceDucked)
            targetVolume = normalVolume;
    }

    /// <summary>Force-duck the music (e.g. for cutscenes). Call ForceUnduck() to release.</summary>
    public void ForceDuck()
    {
        isForceDucked = true;
        targetVolume = duckedVolume;
    }

    /// <summary>Release force-duck. Music will unduck if no other audio is playing.</summary>
    public void ForceUnduck()
    {
        isForceDucked = false;
        // ScanCoroutine will set the correct targetVolume on next tick
    }

    /// <summary>Pause the music without fading.</summary>
    public void PauseMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
            musicSource.Pause();
    }

    /// <summary>Resume the music from where it was paused.</summary>
    public void ResumeMusic()
    {
        if (musicSource != null && !musicSource.isPlaying)
            musicSource.UnPause();
    }

    /// <summary>Stop the music completely.</summary>
    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    /// <summary>Start playing (or restart) the background track.</summary>
    public void PlayMusic()
    {
        if (musicSource == null) return;
        if (backgroundTrack == null)
        {
            Debug.LogWarning("BackgroundMusicManager: No background track assigned!");
            return;
        }
        musicSource.clip = backgroundTrack;
        musicSource.Play();
    }

    // ═══════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    void Awake()
    {
        // ── Singleton ──
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("BackgroundMusicManager: Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ── Setup AudioSource ──
        musicSource = GetComponent<AudioSource>();
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = normalVolume;
        musicSource.priority = 256; // lowest priority — won't steal channels from SFX/voice
        musicSource.spatialBlend = 0f; // 2D audio (not affected by listener position)

        if (backgroundTrack != null)
        {
            musicSource.clip = backgroundTrack;
        }

        targetVolume = normalVolume;
    }

    void Start()
    {
        // Start playing music
        if (backgroundTrack != null)
        {
            musicSource.Play();
            Debug.Log($"BackgroundMusicManager: Playing '{backgroundTrack.name}' at volume {normalVolume}");
        }
        else
        {
            Debug.LogWarning("BackgroundMusicManager: No background track assigned! " +
                "Assign in Inspector or run 'Karma > Setup Game Systems'.");
        }

        // Start the scan coroutine
        StartCoroutine(ScanCoroutine());
    }

    void Update()
    {
        if (musicSource == null) return;

        // Smooth volume transition
        if (!Mathf.Approximately(musicSource.volume, targetVolume))
        {
            musicSource.volume = Mathf.MoveTowards(
                musicSource.volume,
                targetVolume,
                duckFadeSpeed * Time.deltaTime
            );
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ═══════════════════════════════════════════════════════════
    //  AUDIO SOURCE SCANNING
    // ═══════════════════════════════════════════════════════════

    private IEnumerator ScanCoroutine()
    {
        // Initial cache build (small delay to let other scripts initialize)
        yield return new WaitForSeconds(0.5f);
        RefreshSourceCache();

        var wait = new WaitForSeconds(scanInterval);
        float timeSinceRefresh = 0f;

        while (true)
        {
            yield return wait;

            // Periodically refresh the cache to catch dynamically created AudioSources
            timeSinceRefresh += scanInterval;
            if (timeSinceRefresh >= cacheRefreshInterval)
            {
                RefreshSourceCache();
                timeSinceRefresh = 0f;
            }

            // Don't auto-detect if force-ducked (manual control)
            if (isForceDucked) continue;

            // Check if any other AudioSource is currently playing
            bool otherAudioPlaying = false;

            if (cachedSources != null)
            {
                for (int i = 0; i < cachedSources.Length; i++)
                {
                    AudioSource src = cachedSources[i];

                    // Skip null (destroyed), self, and non-playing sources
                    if (src == null) continue;
                    if (src == musicSource) continue;
                    if (!src.isPlaying) continue;

                    // Found another AudioSource that's playing
                    otherAudioPlaying = true;
                    break;
                }
            }

            // Set target volume based on detection
            targetVolume = otherAudioPlaying ? duckedVolume : normalVolume;
        }
    }

    /// <summary>
    /// Refresh the cached list of all AudioSources in the scene.
    /// Catches dynamically created sources (e.g. PlayClipAtPoint).
    /// </summary>
    private void RefreshSourceCache()
    {
        cachedSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
    }
}
