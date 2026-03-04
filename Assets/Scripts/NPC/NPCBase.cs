using System.Collections;
using UnityEngine;

/// <summary>
/// Abstract base class for all NPCs in the Karma game.
/// Provides shared functionality: player detection, animation, audio, facing.
///
/// NPC Hierarchy:
///   NPCBase (this)
///   ├── GhostNPC      — Roaming ambient ghosts (NavMesh)
///   ├── DialogueNPC   — NPCs that talk (Serna, quest givers, interactors)
///   └── (future) BossNPC — Main character heroes with unique interactions
///
/// Subclasses override OnPlayerEnterRange() / OnPlayerExitRange() for behavior.
/// </summary>
public abstract class NPCBase : MonoBehaviour
{
    // ─── Detection ──────────────────────────────────────────────
    [Header("NPC Detection")]
    [Tooltip("Distance at which the NPC detects the player")]
    [Range(2f, 25f)] [SerializeField] protected float detectionRadius = 8f;

    [Tooltip("Tag used to find the player")]
    [SerializeField] protected string playerTag = "Player";

    [Tooltip("How fast the NPC turns to face the player")]
    [Range(1f, 10f)] [SerializeField] protected float facePlayerSpeed = 3f;

    // ─── Audio ──────────────────────────────────────────────────
    [Header("NPC Audio")]
    [Tooltip("AudioSource (auto-found if not assigned)")]
    [SerializeField] protected AudioSource audioSource;

    [Tooltip("Audio fade speed for ambient sounds")]
    [Range(0.5f, 5f)] [SerializeField] protected float audioFadeSpeed = 2f;

    [Tooltip("Maximum audio volume")]
    [Range(0f, 1f)] [SerializeField] protected float maxVolume = 0.8f;

    // ─── Animation ──────────────────────────────────────────────
    [Header("NPC Animation")]
    [Tooltip("Animator on this or child object (auto-found if not assigned)")]
    [SerializeField] protected Animator animator;

    // ─── Runtime State ──────────────────────────────────────────
    protected Transform playerTransform;
    protected bool playerInRange;
    protected float distanceToPlayer = float.MaxValue;

    // ─── Properties ─────────────────────────────────────────────

    /// <summary>Whether the player is within detection range.</summary>
    public bool PlayerInRange => playerInRange;

    /// <summary>Distance to the player (updated each frame).</summary>
    public float DistanceToPlayer => distanceToPlayer;

    /// <summary>The player's transform (auto-found by tag).</summary>
    public Transform PlayerTransform => playerTransform;

    // ─── Virtual Hooks (override in subclasses) ──────────────────

    /// <summary>Called when the player enters detection range.</summary>
    protected virtual void OnPlayerEnterRange() { }

    /// <summary>Called when the player exits detection range.</summary>
    protected virtual void OnPlayerExitRange() { }

    /// <summary>Called each frame while the player is in range.</summary>
    protected virtual void OnPlayerInRange() { }

    // ─── Shared Methods ─────────────────────────────────────────

    /// <summary>Smoothly rotate to face the player (call from subclass Update).</summary>
    protected void FacePlayer()
    {
        if (playerTransform == null) return;

        Vector3 dir = (playerTransform.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, facePlayerSpeed * Time.deltaTime);
        }
    }

    /// <summary>Play a random clip from an array.</summary>
    protected void PlayRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || audioSource == null) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) audioSource.PlayOneShot(clip, maxVolume);
    }

    /// <summary>Play a single clip.</summary>
    protected void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, maxVolume);
    }

    /// <summary>Fade in a looping audio clip.</summary>
    protected Coroutine FadeAudioIn(AudioClip clip)
    {
        return StartCoroutine(FadeAudioInCoroutine(clip));
    }

    /// <summary>Fade out the current audio.</summary>
    protected Coroutine FadeAudioOut()
    {
        return StartCoroutine(FadeAudioOutCoroutine());
    }

    /// <summary>Find the player by tag.</summary>
    protected void FindPlayer()
    {
        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject playerObj = GameObject.FindWithTag(playerTag);
            if (playerObj != null)
                playerTransform = playerObj.transform;
        }
    }

    /// <summary>Check player proximity and fire enter/exit range events.</summary>
    protected void CheckPlayerProximity()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        distanceToPlayer = Vector3.Distance(playerTransform.position, transform.position);
        bool wasInRange = playerInRange;
        playerInRange = distanceToPlayer <= detectionRadius;

        if (playerInRange && !wasInRange)
            OnPlayerEnterRange();

        if (!playerInRange && wasInRange)
            OnPlayerExitRange();

        if (playerInRange)
            OnPlayerInRange();
    }

    // ─── Unity Lifecycle ────────────────────────────────────────

    protected virtual void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    protected virtual void Start()
    {
        FindPlayer();
    }

    protected virtual void Update()
    {
        CheckPlayerProximity();
    }

    // ─── Audio Coroutines ───────────────────────────────────────

    private IEnumerator FadeAudioInCoroutine(AudioClip clip)
    {
        if (audioSource == null) yield break;

        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.volume = 0f;
        audioSource.Play();

        while (audioSource.volume < maxVolume)
        {
            audioSource.volume = Mathf.MoveTowards(
                audioSource.volume, maxVolume, audioFadeSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator FadeAudioOutCoroutine()
    {
        if (audioSource == null) yield break;

        while (audioSource.volume > 0.01f)
        {
            audioSource.volume = Mathf.MoveTowards(
                audioSource.volume, 0f, audioFadeSpeed * Time.deltaTime);
            yield return null;
        }

        audioSource.Stop();
        audioSource.loop = false;
        audioSource.volume = 0f;
    }

    // ─── Gizmos ─────────────────────────────────────────────────

    protected virtual void OnDrawGizmosSelected()
    {
        // Detection radius
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
