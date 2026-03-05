using UnityEngine;

/// <summary>
/// Objects that can be pushed and pulled by the player.
/// Requires Rigidbody + Collider on the GameObject.
///
/// Features:
///   - friction: scales push speed (higher = slower push)
///   - canPull: whether backward movement is allowed (checked by PushPullState)
///   - Optional push loop audio (3D spatial, loops while pushing)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PushableObject : InteractableBase
{
    [Header("Push/Pull Settings")]
    [Tooltip("Movement friction (0 = no friction, 1 = half speed, 2 = third speed)")]
    public float friction = 0.5f;

    [Tooltip("Whether the player can pull this object backward")]
    public bool canPull = true;

    [Header("Audio (optional)")]
    [Tooltip("Looping sound while being pushed/pulled (scraping, grinding, etc.)")]
    [SerializeField] private AudioClip pushLoopClip;

    [Tooltip("Volume of push loop sound")]
    [Range(0f, 1f)]
    [SerializeField] private float pushLoopVolume = 0.5f;

    private Rigidbody rb;
    private AudioSource audioSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Dynamic prompt based on whether pulling is allowed
        prompt = canPull ? "Push / Pull" : "Push";

        // Constrain to prevent tipping over
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Only add AudioSource if we have a push loop clip
        if (pushLoopClip != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = pushLoopClip;
            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.volume = pushLoopVolume;
            audioSource.spatialBlend = 1f; // 3D sound
        }
    }

    public override void Interact(PlayerController player)
    {
        StartInteraction(player);
    }

    public void StartInteraction(PlayerController player)
    {
        player.interactionTarget = gameObject;
        player.stateMachine.SetState<PushPullState>();
    }

    public void ApplyMovement(Vector3 delta)
    {
        rb.MovePosition(rb.position + delta);
    }

    public void StopInteraction()
    {
        StopPushLoop();
    }

    // ─── Push Loop Audio ────────────────────────────────────────

    /// <summary>Start the push loop audio if not already playing.</summary>
    public void PlayPushLoop()
    {
        if (audioSource != null && !audioSource.isPlaying)
            audioSource.Play();
    }

    /// <summary>Stop the push loop audio.</summary>
    public void StopPushLoop()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }
}
