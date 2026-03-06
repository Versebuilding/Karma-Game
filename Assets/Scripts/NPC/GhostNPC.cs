using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Ghost NPC controller: NavMesh roaming, player detection, reaction system, and audio.
/// Replaces the old GhostRoamer + GhostAmbientNPC + GhostTerrainSnap scripts.
///
/// Setup:
///   Root object: NavMeshAgent + GhostNPC + AudioSource + Collider
///   Child object: Animator + mesh + GhostFloatEffect (visual only)
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class GhostNPC : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  INSPECTOR FIELDS — All editable from Inspector
    // ═══════════════════════════════════════════════════════════════

    // ─── Ground / Height ────────────────────────────────────────
    [Header("Ground / Height")]
    [Tooltip("Height offset above terrain. Set to half the ghost model height so the " +
             "bottom of the mesh sits at ground level. Set to 0 to auto-detect from child mesh.")]
    [Range(0f, 10f)] [SerializeField] private float groundOffset = 0f;

    [Tooltip("If true, automatically calculates groundOffset from the child mesh " +
             "renderer bounds at Start (recommended for first-time setup)")]
    [SerializeField] private bool autoDetectGroundOffset = true;

    // ─── Roaming ─────────────────────────────────────────────────
    [Header("Roaming")]
    [Tooltip("Maximum distance from spawn point the ghost will wander")]
    [Range(5f, 50f)] [SerializeField] private float roamRadius = 15f;

    [Tooltip("Movement speed while roaming")]
    [Range(0.5f, 8f)] [SerializeField] private float roamSpeed = 2f;

    [Tooltip("Random speed variation per ghost instance (e.g. 0.3 = ±30%)")]
    [Range(0f, 0.5f)] [SerializeField] private float speedVariance = 0.25f;

    [Tooltip("Minimum seconds to idle at each waypoint")]
    [Range(0f, 10f)] [SerializeField] private float idleDurationMin = 2f;

    [Tooltip("Maximum seconds to idle at each waypoint")]
    [Range(0f, 15f)] [SerializeField] private float idleDurationMax = 5f;

    [Tooltip("How close the ghost needs to be to a waypoint before picking the next one")]
    [Range(0.5f, 3f)] [SerializeField] private float waypointReachedDist = 1f;

    [Tooltip("Minimum distance for a new random waypoint (avoids tiny moves)")]
    [Range(2f, 15f)] [SerializeField] private float minRoamDistance = 5f;

    [Tooltip("NavMesh sample radius when picking new destinations")]
    [Range(2f, 10f)] [SerializeField] private float navMeshSampleRadius = 5f;

    // ─── Player Detection ────────────────────────────────────────
    [Header("Player Detection")]
    [Tooltip("Distance at which the ghost detects the player")]
    [Range(3f, 20f)] [SerializeField] private float detectionRadius = 8f;

    [Tooltip("Seconds the ghost pauses before reacting (tells story)")]
    [Range(0f, 5f)] [SerializeField] private float reactionDelay = 1.5f;

    [Tooltip("Cooldown before the ghost can react to the player again")]
    [Range(5f, 30f)] [SerializeField] private float reactionCooldown = 10f;

    [Tooltip("How fast the ghost turns to face the player")]
    [Range(1f, 10f)] [SerializeField] private float facePlayerSpeed = 3f;

    [Tooltip("Tag used to find the player automatically")]
    [SerializeField] private string playerTag = "Player";

    // ─── Audio ───────────────────────────────────────────────────
    [Header("Audio")]
    [Tooltip("Audio clips played when ghost greets (random pick)")]
    [SerializeField] private AudioClip[] greetClips;

    [Tooltip("Audio clips played when ghost screams/is angry (random pick)")]
    [SerializeField] private AudioClip[] screamClips;

    [Tooltip("Ambient audio that plays while ghost is paused near player")]
    [SerializeField] private AudioClip ambientClip;

    [Tooltip("How fast audio fades in/out")]
    [Range(0.5f, 5f)] [SerializeField] private float audioFadeSpeed = 2f;

    [Tooltip("Maximum audio volume")]
    [Range(0f, 1f)] [SerializeField] private float maxVolume = 0.8f;

    // ─── Animation ───────────────────────────────────────────────
    [Header("Animation")]
    [Tooltip("Animator on the child mesh object (auto-found if empty)")]
    [SerializeField] private Animator animator;

    [Tooltip("Float parameter name for movement speed in Animator")]
    [SerializeField] private string speedParam = "Speed";

    [Tooltip("Trigger parameter name for greet animation")]
    [SerializeField] private string greetTrigger = "Greet";

    [Tooltip("Trigger parameter name for scream/angry animation")]
    [SerializeField] private string screamTrigger = "Scream";

    // ─── Reaction Settings ───────────────────────────────────────
    [Header("Reaction Behavior")]
    [Tooltip("If true, ghost will greet when player greets first")]
    [SerializeField] private bool canGreet = true;

    [Tooltip("If true, ghost can scream/be angry")]
    [SerializeField] private bool canScream = true;

    [Tooltip("Default reaction when player doesn't interact: Greet, Scream, or Nothing")]
    [SerializeField] private DefaultReaction defaultReaction = DefaultReaction.Nothing;

    // ─── Events (for UI team / Karma system) ─────────────────────
    [Header("Events")]
    [Tooltip("Fired when the player enters detection range")]
    public UnityEvent OnPlayerApproached;

    [Tooltip("Fired when the player leaves detection range")]
    public UnityEvent OnPlayerLeft;

    [Tooltip("Fired when the ghost plays a greet reaction")]
    public UnityEvent OnGreetReaction;

    [Tooltip("Fired when the ghost plays a scream reaction")]
    public UnityEvent OnScreamReaction;

    // ═══════════════════════════════════════════════════════════════
    //  ENUMS
    // ═══════════════════════════════════════════════════════════════

    public enum GhostState { Roaming, Paused, Reacting }
    public enum DefaultReaction { Nothing, Greet, Scream }

    // ═══════════════════════════════════════════════════════════════
    //  RUNTIME STATE (not in Inspector)
    // ═══════════════════════════════════════════════════════════════

    private NavMeshAgent agent;
    private AudioSource audioSource;
    private Transform playerTransform;

    private GhostState currentState = GhostState.Roaming;
    private Vector3 spawnPosition;
    private float idleTimer;
    private float reactionCooldownTimer;
    private bool playerGreeted;
    private bool playerInRange;

    // Karma evaluator hook — set by external systems
    private Func<float> karmaEvaluator;

    // Animator parameter hashes (cached for performance)
    private int hashSpeed;
    private bool hasSpeedParam;
    private int hashGreet;
    private int hashScream;

    // Audio tracking
    private Coroutine ambientFadeCoroutine;

    // Outline highlight — cached like InteractableBase pattern
    private QuickOutline cachedOutline;
    private bool outlineLookedUp;

    // ═══════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Current ghost state (read-only).</summary>
    public GhostState CurrentState => currentState;

    /// <summary>
    /// Trigger the greet reaction. Only works when ghost is Paused (player in range).
    /// Called by interaction system, dialogue events, or cutscene scripts.
    /// Greet/scream clips do NOT play automatically — they require this explicit trigger.
    /// </summary>
    public void PlayerGreeted()
    {
        if (currentState != GhostState.Paused && currentState != GhostState.Roaming) return;

        // If roaming and player is in range, pause first
        if (currentState == GhostState.Roaming && playerInRange)
            TransitionTo(GhostState.Paused);

        playerGreeted = true;
        StartCoroutine(DoReactionAfterDelay(true));
    }

    /// <summary>
    /// Trigger the scream reaction. Only works when ghost is Paused or Roaming (player in range).
    /// Called by interaction triggers, dialogue events, or cutscene scripts.
    /// </summary>
    public void TriggerScream()
    {
        if (currentState != GhostState.Paused && currentState != GhostState.Roaming) return;

        // If roaming and player is in range, pause first
        if (currentState == GhostState.Roaming && playerInRange)
            TransitionTo(GhostState.Paused);

        StartCoroutine(DoReactionAfterDelay(false));
    }

    /// <summary>
    /// Trigger a reaction based on current karma level.
    /// High karma (>= 0.5) → greet. Low karma (&lt; 0.5) → scream.
    /// Called by external trigger zones, dialogue actions, or interaction system.
    /// </summary>
    public void TriggerKarmaReaction()
    {
        if (currentState != GhostState.Paused && currentState != GhostState.Roaming) return;

        if (currentState == GhostState.Roaming && playerInRange)
            TransitionTo(GhostState.Paused);

        float karma = karmaEvaluator != null ? karmaEvaluator() : 0.5f;
        if (karma >= 0.5f && canGreet)
            StartCoroutine(DoReactionAfterDelay(true));
        else if (karma < 0.5f && canScream)
            StartCoroutine(DoReactionAfterDelay(false));
    }

    /// <summary>
    /// Hook for karma system. Provide a function that returns the current karma score (0-1).
    /// Used by TriggerKarmaReaction() to decide greet vs scream.
    /// </summary>
    public void SetKarmaEvaluator(Func<float> evaluator)
    {
        karmaEvaluator = evaluator;
    }

    /// <summary>
    /// Force the ghost into a specific state (useful for cutscenes/triggers).
    /// </summary>
    public void ForceState(GhostState newState)
    {
        TransitionTo(newState);
    }

    // ═══════════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();

        // Auto-find animator on child if not assigned
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        // Cache animator hashes
        hashSpeed = Animator.StringToHash(speedParam);
        hashGreet = Animator.StringToHash(greetTrigger);
        hashScream = Animator.StringToHash(screamTrigger);

        // Check if speed parameter exists in the animator (avoids per-frame warnings)
        hasSpeedParam = false;
        if (animator != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.nameHash == hashSpeed && param.type == AnimatorControllerParameterType.Float)
                {
                    hasSpeedParam = true;
                    break;
                }
            }
        }

        // Auto-detect ground offset from child mesh bounds
        if (autoDetectGroundOffset || groundOffset <= 0f)
        {
            Renderer meshRenderer = GetComponentInChildren<Renderer>();
            if (meshRenderer != null)
            {
                // The mesh bounds min.y relative to this transform tells us
                // how far below the pivot the mesh extends. We need to raise
                // the agent by that amount so the mesh bottom sits at ground.
                float meshBottomLocal = transform.InverseTransformPoint(meshRenderer.bounds.min).y;
                groundOffset = Mathf.Max(0f, -meshBottomLocal) + 0.1f; // +0.1 small padding above terrain
                Debug.Log($"GhostNPC '{gameObject.name}': Auto-detected groundOffset = {groundOffset:F2}");
            }
            else
            {
                groundOffset = 1.5f; // safe fallback
                Debug.LogWarning($"GhostNPC '{gameObject.name}': No Renderer found for auto-detect. " +
                    "Using fallback groundOffset = {groundOffset}. Set manually in Inspector.");
            }
        }

        // Randomize speed per instance so ghosts don't all move identically
        float instanceSpeed = roamSpeed * UnityEngine.Random.Range(1f - speedVariance, 1f + speedVariance);

        // Configure NavMeshAgent defaults
        agent.speed = instanceSpeed;
        agent.angularSpeed = UnityEngine.Random.Range(90f, 150f); // Vary turn speed too
        agent.stoppingDistance = waypointReachedDist;
        agent.autoBraking = true;
        agent.updateRotation = false; // we rotate manually for smoothness
        agent.baseOffset = groundOffset; // raise agent so mesh bottom sits on terrain

        // Configure AudioSource
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f; // 3D audio
        audioSource.volume = 0f;

        // Pre-find outline on child mesh and ensure it starts disabled
        cachedOutline = GetComponentInChildren<QuickOutline>(true);
        outlineLookedUp = true;
        if (cachedOutline != null)
            cachedOutline.enabled = false;

        // Warn if ambient clip is missing — helps identify prefabs that won't play audio
        if (ambientClip == null)
            Debug.LogWarning($"GhostNPC '{gameObject.name}': No ambientClip assigned! " +
                "Drag an audio clip into the Ambient Clip field in Inspector.");

        spawnPosition = transform.position;
    }

    void Start()
    {
        // Find player by tag
        FindPlayer();

        // Auto-wire karma evaluator to KarmaManager if available
        if (karmaEvaluator == null && KarmaManager.Instance != null)
        {
            SetKarmaEvaluator(() => KarmaManager.Instance.GetNormalizedKarma());
        }

        // Snap to NavMesh if not already on it
        SnapToNavMesh();

        // Randomize initial state so prefab instances don't move in sync:
        // 1. Stagger first move with a random idle delay
        idleTimer = UnityEngine.Random.Range(0f, idleDurationMax);
        // 2. Random initial facing direction
        transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        // 3. Pick first destination (each ghost gets a different random point)
        PickRandomDestination();
    }

    void Update()
    {
        // Tick cooldown
        if (reactionCooldownTimer > 0f)
            reactionCooldownTimer -= Time.deltaTime;

        switch (currentState)
        {
            case GhostState.Roaming:
                UpdateRoaming();
                break;
            case GhostState.Paused:
                UpdatePaused();
                break;
            case GhostState.Reacting:
                UpdateReacting();
                break;
        }

        // Always check for player proximity
        CheckPlayerProximity();

        // Update animator speed param
        UpdateAnimatorSpeed();
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE UPDATES
    // ═══════════════════════════════════════════════════════════════

    private void UpdateRoaming()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        // If idling at a waypoint, count down
        if (idleTimer > 0f)
        {
            idleTimer -= Time.deltaTime;
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;

        // Check if we reached the destination
        if (!agent.pathPending && agent.remainingDistance <= waypointReachedDist)
        {
            // Idle at this point
            idleTimer = UnityEngine.Random.Range(idleDurationMin, idleDurationMax);
            PickRandomDestination();
        }

        // Smooth rotation toward movement direction
        SmoothRotateToVelocity();
    }

    private void UpdatePaused()
    {
        // Face the player smoothly
        if (playerTransform != null)
        {
            Vector3 dirToPlayer = (playerTransform.position - transform.position);
            dirToPlayer.y = 0f;
            if (dirToPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToPlayer.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, facePlayerSpeed * Time.deltaTime);
            }
        }
    }

    private void UpdateReacting()
    {
        // Face the player during reaction too
        UpdatePaused();

        // Check if reaction animation has finished
        // (we transition back to Roaming via coroutine)
    }

    // ═══════════════════════════════════════════════════════════════
    //  PLAYER DETECTION
    // ═══════════════════════════════════════════════════════════════

    private void CheckPlayerProximity()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            return;
        }

        float dist = Vector3.Distance(playerTransform.position, transform.position);
        bool wasInRange = playerInRange;
        playerInRange = dist <= detectionRadius;

        // ── Player just entered range ──
        // Audio + outline ALWAYS fire (regardless of cooldown/state) so every ghost
        // responds when the player walks near it.
        if (playerInRange && !wasInRange)
        {
            // Start looping ambient audio
            StartAmbientAudio();

            // Enable outline highlight (like NPC targeting)
            SetOutlineVisible(true);

            // Transition to Paused only if Roaming and cooldown has elapsed
            if (currentState == GhostState.Roaming && reactionCooldownTimer <= 0f)
            {
                TransitionTo(GhostState.Paused);
                OnPlayerApproached?.Invoke();
            }

            // NOTE: Greet/scream clips do NOT auto-play.
            // Use PlayerGreeted(), TriggerScream(), or TriggerKarmaReaction()
            // from interaction triggers, dialogue actions, or cutscene scripts.
        }

        // ── Player just left range ──
        if (!playerInRange && wasInRange)
        {
            // Always stop audio + outline when player leaves
            StopAmbientAudio();
            SetOutlineVisible(false);

            if (currentState == GhostState.Paused || currentState == GhostState.Reacting)
            {
                OnPlayerLeft?.Invoke();
                TransitionTo(GhostState.Roaming);
                reactionCooldownTimer = reactionCooldown;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  REACTION SYSTEM
    // ═══════════════════════════════════════════════════════════════

    // NOTE: Auto-reaction removed. Greet/scream clips require explicit triggers:
    //   PlayerGreeted()       — greet reaction
    //   TriggerScream()       — scream reaction
    //   TriggerKarmaReaction() — karma-based (>= 0.5 greet, < 0.5 scream)
    //
    // Wire these to: trigger zones, dialogue actions, interaction system, or cutscene scripts.

    /// <summary>
    /// Play the greet or scream reaction.
    /// </summary>
    private IEnumerator DoReactionAfterDelay(bool isGreet)
    {
        TransitionTo(GhostState.Reacting);

        if (isGreet)
        {
            // Play greet animation
            if (animator != null)
                animator.SetTrigger(hashGreet);

            // Play random greet audio
            PlayRandomClip(greetClips);

            OnGreetReaction?.Invoke();
        }
        else
        {
            // Play scream animation
            if (animator != null)
                animator.SetTrigger(hashScream);

            // Play random scream audio
            PlayRandomClip(screamClips);

            OnScreamReaction?.Invoke();
        }

        // Wait for reaction to finish (approximate with clip length or fixed time)
        float reactionDuration = GetCurrentClipLength();
        yield return new WaitForSeconds(Mathf.Max(reactionDuration, 2f));

        // Return to roaming if player left, or stay paused if still in range
        if (playerInRange)
        {
            TransitionTo(GhostState.Paused);
        }
        else
        {
            TransitionTo(GhostState.Roaming);
            reactionCooldownTimer = reactionCooldown;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATE TRANSITIONS
    // ═══════════════════════════════════════════════════════════════

    private void TransitionTo(GhostState newState)
    {
        // Exit current state
        switch (currentState)
        {
            case GhostState.Roaming:
                break;
            case GhostState.Paused:
                break;
            case GhostState.Reacting:
                break;
        }

        currentState = newState;

        // Enter new state
        switch (newState)
        {
            case GhostState.Roaming:
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    PickRandomDestination();
                }
                playerGreeted = false;
                break;

            case GhostState.Paused:
                if (agent != null && agent.isOnNavMesh)
                    agent.isStopped = true;
                break;

            case GhostState.Reacting:
                if (agent != null && agent.isOnNavMesh)
                    agent.isStopped = true;
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  NAVIGATION HELPERS
    // ═══════════════════════════════════════════════════════════════

    private void PickRandomDestination()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        // Try up to 20 times to find a valid NavMesh point
        for (int i = 0; i < 20; i++)
        {
            Vector3 randomDir = UnityEngine.Random.insideUnitSphere * roamRadius;
            randomDir.y = 0f;
            Vector3 candidate = spawnPosition + randomDir;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                float dist = Vector3.Distance(transform.position, hit.position);
                if (dist >= minRoamDistance)
                {
                    agent.SetDestination(hit.position);
                    return;
                }
            }
        }

        // Fallback: try a closer point
        Vector3 fallback = spawnPosition + UnityEngine.Random.insideUnitSphere * (roamRadius * 0.5f);
        fallback.y = transform.position.y;
        if (NavMesh.SamplePosition(fallback, out NavMeshHit fallbackHit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(fallbackHit.position);
        }
    }

    private void SnapToNavMesh()
    {
        if (agent == null) return;

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 20f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                spawnPosition = hit.position;
                Debug.Log($"GhostNPC '{gameObject.name}': Snapped to NavMesh at {hit.position}");
            }
            else
            {
                Debug.LogWarning($"GhostNPC '{gameObject.name}': Could not find NavMesh within 20m! " +
                    "Make sure you've baked the NavMesh (Window > AI > Navigation > Bake)");
            }
        }
    }

    private void SmoothRotateToVelocity()
    {
        if (agent == null) return;

        Vector3 velocity = agent.desiredVelocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude > 0.05f)
        {
            Quaternion targetRot = Quaternion.LookRotation(velocity.normalized);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, facePlayerSpeed * Time.deltaTime);
        }
    }

    private void FindPlayer()
    {
        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject playerObj = GameObject.FindWithTag(playerTag);
            if (playerObj != null)
                playerTransform = playerObj.transform;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  AUDIO HELPERS
    // ═══════════════════════════════════════════════════════════════

    private void PlayRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || audioSource == null) return;

        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (clip != null)
        {
            audioSource.PlayOneShot(clip, maxVolume);
        }
    }

    /// <summary>Start looping ambient audio with fade-in.</summary>
    private void StartAmbientAudio()
    {
        if (ambientClip == null || audioSource == null) return;

        if (ambientFadeCoroutine != null)
            StopCoroutine(ambientFadeCoroutine);
        ambientFadeCoroutine = StartCoroutine(FadeAudioInLoop(ambientClip));
    }

    /// <summary>Fade out and stop any playing audio.</summary>
    private void StopAmbientAudio()
    {
        if (ambientFadeCoroutine != null)
            StopCoroutine(ambientFadeCoroutine);
        ambientFadeCoroutine = null;
        StartCoroutine(FadeAudioOut());
    }

    /// <summary>
    /// Fade in an audio clip and loop it continuously while the player is in range.
    /// Stops when StopAmbientAudio() / FadeAudioOut() is called.
    /// </summary>
    private IEnumerator FadeAudioInLoop(AudioClip clip)
    {
        if (audioSource == null) yield break;

        audioSource.clip = clip;
        audioSource.loop = true; // Loop while player is nearby
        audioSource.volume = 0f;
        audioSource.Play();

        // Fade in
        while (audioSource.volume < maxVolume)
        {
            audioSource.volume = Mathf.MoveTowards(
                audioSource.volume, maxVolume, audioFadeSpeed * Time.deltaTime);
            yield return null;
        }

        ambientFadeCoroutine = null; // Fade-in done — audio continues looping via AudioSource.loop
    }

    private IEnumerator FadeAudioOut()
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

    // ═══════════════════════════════════════════════════════════════
    //  OUTLINE HIGHLIGHT
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Enable or disable the QuickOutline on the ghost's child mesh.
    /// Mirrors the InteractableBase targeting pattern — ghost glows when player is near.
    /// </summary>
    private void SetOutlineVisible(bool visible)
    {
        // Lazy lookup fallback (if Awake didn't find it, e.g. outline added at runtime)
        if (!outlineLookedUp)
        {
            cachedOutline = GetComponentInChildren<QuickOutline>(true);
            outlineLookedUp = true;
        }

        if (cachedOutline != null)
            cachedOutline.enabled = visible;
    }

    // ═══════════════════════════════════════════════════════════════
    //  ANIMATION HELPERS
    // ═══════════════════════════════════════════════════════════════

    private void UpdateAnimatorSpeed()
    {
        if (animator == null) return;

        if (!hasSpeedParam) return;

        float speed = 0f;
        if (currentState == GhostState.Roaming && agent != null)
        {
            speed = agent.velocity.magnitude / Mathf.Max(roamSpeed, 0.01f);
        }

        animator.SetFloat(hashSpeed, speed);
    }

    private float GetCurrentClipLength()
    {
        if (animator == null) return 2f;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.length;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GIZMOS (Scene view debugging)
    // ═══════════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        // Roam radius (green circle)
        Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.3f);
        Vector3 center = Application.isPlaying ? spawnPosition : transform.position;
        Gizmos.DrawWireSphere(center, roamRadius);

        // Detection radius (yellow circle)
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Current destination (red line)
        if (Application.isPlaying && agent != null && agent.hasPath)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, agent.destination);
            Gizmos.DrawSphere(agent.destination, 0.3f);
        }

        // State label is visible via the enum in Inspector
    }
}
