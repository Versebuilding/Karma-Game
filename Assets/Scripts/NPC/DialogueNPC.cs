using System.Collections;
using UnityEngine;

/// <summary>
/// NPC that the player can talk to. Starts a DialogueSO conversation on interact.
/// Extends InteractableBase so it integrates with the existing InteractionDetector
/// (player's trigger collider detects this, GroundedState calls Interact on E press).
///
/// Features:
///   - Plugs into DialogueManager for dialogue flow
///   - Outline pulsing when targeted (QuickOutline)
///   - Faces player during dialogue
///   - Drives SernaAnimCycler (or any animator with IsTalking bool)
///   - Audio fade in/out for ambient talk sounds
///   - Ends dialogue if player walks out of range
///
/// Setup:
///   1. Add this component to the NPC GameObject
///   2. Add a Collider (so InteractionDetector's trigger can detect it)
///   3. Assign a DialogueSO asset in the Inspector
///   4. Optionally assign QuickOutline, SernaAnimCycler, AudioSource
///
/// Replaces SernaInteraction.cs for Serna (and works for any talking NPC).
/// </summary>
public class DialogueNPC : InteractableBase
{
    // ─── Dialogue ─────────────────────────────────────────────
    [Header("Dialogue")]
    [Tooltip("The dialogue tree to play when the player interacts")]
    [SerializeField] private DialogueSO dialogue;

    [Tooltip("If true, NPC ends dialogue when the player walks out of range")]
    [SerializeField] private bool endDialogueOnExit = true;

    [Tooltip("Distance at which dialogue is force-ended (0 = use Collider exit)")]
    [Range(0f, 30f)]
    [SerializeField] private float maxDialogueDistance = 8f;

    // ─── Facing ───────────────────────────────────────────────
    [Header("Facing")]
    [Tooltip("NPC turns to face the player during dialogue")]
    [SerializeField] private bool facePlayerDuringDialogue = true;

    [Tooltip("How fast the NPC turns to face the player")]
    [Range(1f, 15f)]
    [SerializeField] private float facePlayerSpeed = 5f;

    // ─── Visual / Outline ─────────────────────────────────────
    [Header("Outline (QuickOutline)")]
    [Tooltip("QuickOutline component (auto-found if not assigned)")]
    [SerializeField] private QuickOutline outline;

    [Tooltip("Pulse the outline width and color when targeted")]
    [SerializeField] private bool pulseOutline = true;

    [Tooltip("Outline pulse speed")]
    [Range(0.5f, 5f)]
    [SerializeField] private float pulseSpeed = 2f;

    [Tooltip("Minimum outline width")]
    [Range(0f, 10f)]
    [SerializeField] private float minOutlineWidth = 2.5f;

    [Tooltip("Maximum outline width")]
    [Range(0f, 10f)]
    [SerializeField] private float maxOutlineWidth = 4.5f;

    [Tooltip("Outline color A (pulse start)")]
    [SerializeField] private Color outlineColorA = new Color(0.35f, 0.9f, 1f, 1f);   // soft cyan

    [Tooltip("Outline color B (pulse end)")]
    [SerializeField] private Color outlineColorB = new Color(1f, 0.85f, 0.25f, 1f);  // warm gold

    // ─── Animation ────────────────────────────────────────────
    [Header("Animation")]
    [Tooltip("SernaAnimCycler for idle/talk variant cycling (optional)")]
    [SerializeField] private SernaAnimCycler animCycler;

    [Tooltip("Animator (auto-found if not assigned). Used if no AnimCycler.")]
    [SerializeField] private Animator animator;

    [Tooltip("Animator bool parameter name for talking state")]
    [SerializeField] private string talkingBoolParam = "isTalking";

    [Header("Default Animations")]
    [Tooltip("Default idle animation clips (cycled when not in dialogue). Leave empty to use SernaAnimCycler.")]
    [SerializeField] private AnimationClip[] defaultIdleClips;

    [Tooltip("Default talking animation clips (cycled during dialogue when no per-node animation). Leave empty to use SernaAnimCycler.")]
    [SerializeField] private AnimationClip[] defaultTalkClips;

    [Tooltip("Seconds between default animation variant changes")]
    [Range(1f, 10f)]
    [SerializeField] private float defaultAnimChangeInterval = 3.5f;

    // ─── Audio ────────────────────────────────────────────────
    [Header("Audio")]
    [Tooltip("AudioSource for ambient talk sounds (auto-found)")]
    [SerializeField] private AudioSource voiceSource;

    [Tooltip("Ambient clip to play while talking (murmurs, hums)")]
    [SerializeField] private AudioClip talkAmbientClip;

    [Tooltip("Audio fade duration (seconds)")]
    [Range(0.1f, 2f)]
    [SerializeField] private float audioFadeDuration = 0.3f;

    [Tooltip("Maximum voice volume")]
    [Range(0f, 1f)]
    [SerializeField] private float maxVoiceVolume = 1f;

    // ─── Runtime State ────────────────────────────────────────
    private bool isInDialogue;
    private bool isTargeted;
    private Transform playerTransform;
    private Coroutine voiceFadeCoroutine;
    private Coroutine defaultAnimCoroutine;
    private float dialogueEndTime;
    private bool isPlayingNodeAnimation; // true when per-node override is active

    /// <summary>
    /// Grace period (seconds) after dialogue ends before re-interaction is allowed.
    /// Prevents the Enter key that advanced the last dialogue line from
    /// immediately re-triggering the conversation.
    /// </summary>
    private const float INTERACTION_COOLDOWN = 0.5f;

    // ─── Properties ───────────────────────────────────────────

    /// <summary>Whether this NPC is currently in dialogue.</summary>
    public bool IsInDialogue => isInDialogue;

    /// <summary>The dialogue asset assigned to this NPC.</summary>
    public DialogueSO Dialogue => dialogue;

    // ─── Unity Lifecycle ──────────────────────────────────────

    void Awake()
    {
        // Set default prompt
        if (string.IsNullOrEmpty(prompt))
            prompt = "Talk";

        // Auto-find components
        if (outline == null)
            outline = GetComponentInChildren<QuickOutline>(true);

        if (animCycler == null)
            animCycler = GetComponentInChildren<SernaAnimCycler>(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        // Start with outline off
        if (outline != null)
            outline.enabled = false;

        // Start in idle (not talking)
        if (animCycler != null)
            animCycler.SetTalking(false);

        // Fallback subscription if DialogueManager wasn't ready in OnEnable
        TrySubscribeToDialogueManager();
    }

    private bool isSubscribedToDialogueManager;

    void OnEnable()
    {
        TrySubscribeToDialogueManager();
    }

    void OnDisable()
    {
        UnsubscribeFromDialogueManager();
    }

    private void TrySubscribeToDialogueManager()
    {
        if (isSubscribedToDialogueManager) return;

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnNodeChanged += HandleNodeChanged;
            DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;
            isSubscribedToDialogueManager = true;
        }
    }

    private void UnsubscribeFromDialogueManager()
    {
        if (!isSubscribedToDialogueManager) return;

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnNodeChanged -= HandleNodeChanged;
            DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;
        }
        isSubscribedToDialogueManager = false;
    }

    void Update()
    {
        // Both NPC and player face each other during dialogue
        if (isInDialogue && facePlayerDuringDialogue && playerTransform != null)
        {
            FaceTarget(playerTransform);          // NPC faces player
            FacePlayerTowardNPC(playerTransform);  // Player faces NPC
        }

        // Pulse outline when targeted (not in dialogue)
        if (isTargeted && !isInDialogue && outline != null && outline.enabled && pulseOutline)
        {
            float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
            outline.OutlineWidth = Mathf.Lerp(minOutlineWidth, maxOutlineWidth, t);
            outline.OutlineColor = Color.Lerp(outlineColorA, outlineColorB, t);
        }

        // End dialogue if player walks too far away
        if (isInDialogue && endDialogueOnExit && playerTransform != null
            && maxDialogueDistance > 0f)
        {
            float dist = Vector3.Distance(playerTransform.position, transform.position);
            if (dist > maxDialogueDistance)
            {
                ForceEndDialogue();
            }
        }
    }

    // ─── IInteractable / InteractableBase ─────────────────────

    /// <summary>Can interact only if a dialogue is assigned, no dialogue is active, and cooldown has passed.</summary>
    public override bool CanInteract(PlayerController player)
    {
        if (dialogue == null) return false;
        if (DialogueManager.Instance == null) return false;
        if (DialogueManager.Instance.IsDialogueActive) return false;

        // Cooldown after dialogue ends — prevents the Enter key that advanced
        // the last dialogue line from immediately re-triggering interaction
        if (Time.time - dialogueEndTime < INTERACTION_COOLDOWN) return false;

        return true;
    }

    /// <summary>Start the dialogue conversation.</summary>
    public override void Interact(PlayerController player)
    {
        if (dialogue == null || DialogueManager.Instance == null) return;

        isInDialogue = true;
        playerTransform = player.transform;

        // Set active NPC transform so camera can frame the dialogue
        DialogueManager.Instance.ActiveNPCTransform = transform;

        // Hide outline during dialogue
        if (outline != null)
            outline.enabled = false;

        // Start talking animation
        StartTalkingAnimation();

        // Fade in ambient voice
        FadeInVoice();

        // Set the NPC speaker name so UI can route NPC lines vs player lines
        DialogueNode startNode = dialogue.GetStartNode();
        DialogueManager.Instance.ActiveNPCSpeakerName =
            startNode != null ? startNode.speakerName : gameObject.name;

        // Start the dialogue tree
        DialogueManager.Instance.StartDialogue(dialogue);

        Debug.Log($"DialogueNPC: Started dialogue '{dialogue.dialogueId}' with {gameObject.name}");
    }

    /// <summary>Called by InteractionDetector when this becomes the player's target.</summary>
    public override void OnTargeted()
    {
        isTargeted = true;

        if (!isInDialogue && outline != null)
        {
            outline.enabled = true;
            outline.OutlineWidth = minOutlineWidth;
            outline.OutlineColor = outlineColorA;
        }
    }

    /// <summary>Called by InteractionDetector when this is no longer the player's target.</summary>
    public override void OnUntargeted()
    {
        isTargeted = false;

        if (outline != null)
            outline.enabled = false;
    }

    // ─── Dialogue Event Handlers ──────────────────────────────

    private void HandleNodeChanged(DialogueNode node)
    {
        if (!isInDialogue || node == null) return;

        // Only react if this NPC is the active speaker
        if (DialogueManager.Instance == null) return;
        string activeName = DialogueManager.Instance.ActiveNPCSpeakerName;
        bool isThisNPC = !string.IsNullOrEmpty(activeName)
            && string.Equals(activeName, node.speakerName, System.StringComparison.OrdinalIgnoreCase);

        if (!isThisNPC) return;

        // Play per-node voice clip (stop previous to avoid overlap)
        if (voiceSource != null)
        {
            voiceSource.Stop();

            if (node.voiceClip != null)
            {
                voiceSource.loop = false;
                voiceSource.clip = node.voiceClip;
                voiceSource.volume = maxVoiceVolume;
                voiceSource.Play();
            }
            else if (talkAmbientClip != null)
            {
                // Fallback: resume ambient murmur if no per-node clip
                voiceSource.clip = talkAmbientClip;
                voiceSource.loop = true;
                voiceSource.volume = maxVoiceVolume;
                voiceSource.Play();
            }
        }

        // Per-node animation override (if set)
        if (node.nodeAnimation != null)
        {
            // Pause default cycling so it doesn't fight with the override
            StopDefaultAnimCycling();
            if (animCycler != null)
                animCycler.enabled = false;

            // Play the specific animation clip by name
            if (animator != null)
                TryCrossFade(node.nodeAnimation.name, 0.25f);

            isPlayingNodeAnimation = true;
        }
        else if (isPlayingNodeAnimation)
        {
            // Previous node had an override, this one doesn't — resume defaults
            isPlayingNodeAnimation = false;
            ResumeDefaultAnimations(true);
        }
    }

    private void HandleDialogueEnded()
    {
        if (!isInDialogue) return;

        isInDialogue = false;
        isPlayingNodeAnimation = false;
        dialogueEndTime = Time.time;  // Start cooldown to prevent instant re-trigger

        // Stop talking animation + any per-node animation cycling
        StopTalkingAnimation();

        // Re-enable variant cycling if it was paused for per-node animation
        if (animCycler != null)
            animCycler.enabled = true;

        // Fade out ambient voice
        FadeOutVoice();

        // Restore outline if still targeted
        if (isTargeted && outline != null)
        {
            outline.enabled = true;
        }

        Debug.Log($"DialogueNPC: Dialogue ended for {gameObject.name}");
    }

    private void ForceEndDialogue()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            Debug.Log($"DialogueNPC: Player walked out of range, ending dialogue.");
            DialogueManager.Instance.EndDialogue();
        }
    }

    // ─── Animation Helpers ────────────────────────────────────

    private void StartTalkingAnimation()
    {
        if (defaultTalkClips != null && defaultTalkClips.Length > 0)
        {
            // Use explicit clip arrays (overrides SernaAnimCycler)
            if (animCycler != null) animCycler.enabled = false;
            StartDefaultAnimCycling(true);
        }
        else if (animCycler != null)
        {
            // Use the anim cycler (handles variant cycling for idles/talks)
            animCycler.SetTalking(true);
        }
        else if (animator != null)
        {
            // Fallback: just set a bool on the animator directly
            animator.SetBool(talkingBoolParam, true);
        }
    }

    private void StopTalkingAnimation()
    {
        StopDefaultAnimCycling();

        if (defaultIdleClips != null && defaultIdleClips.Length > 0)
        {
            // Return to explicit idle clips
            StartDefaultAnimCycling(false);
        }
        else if (animCycler != null)
        {
            animCycler.enabled = true;
            animCycler.SetTalking(false);
        }
        else if (animator != null)
        {
            animator.SetBool(talkingBoolParam, false);
        }
    }

    /// <summary>
    /// Resume default animations after a per-node override ends.
    /// </summary>
    private void ResumeDefaultAnimations(bool talking)
    {
        if (talking && defaultTalkClips != null && defaultTalkClips.Length > 0)
        {
            StartDefaultAnimCycling(true);
        }
        else if (animCycler != null)
        {
            animCycler.enabled = true;
            if (talking) animCycler.SetTalking(true);
        }
    }

    // ─── Default Animation Cycling ───────────────────────────

    private void StartDefaultAnimCycling(bool talking)
    {
        StopDefaultAnimCycling();

        AnimationClip[] clips = talking ? defaultTalkClips : defaultIdleClips;
        if (clips == null || clips.Length == 0 || animator == null) return;

        defaultAnimCoroutine = StartCoroutine(DefaultAnimCycleCoroutine(clips));
    }

    private void StopDefaultAnimCycling()
    {
        if (defaultAnimCoroutine != null)
        {
            StopCoroutine(defaultAnimCoroutine);
            defaultAnimCoroutine = null;
        }
    }

    private IEnumerator DefaultAnimCycleCoroutine(AnimationClip[] clips)
    {
        int index = 0;

        while (true)
        {
            if (clips[index] != null && animator != null)
                TryCrossFade(clips[index].name, 0.25f);

            yield return new WaitForSeconds(defaultAnimChangeInterval);

            index = (index + 1) % clips.Length;
        }
    }

    // ─── Animator Helper ──────────────────────────────────

    /// <summary>
    /// Safely cross-fade to an animation state. Checks that the state exists
    /// in the Animator Controller first to avoid "State could not be found" errors.
    /// </summary>
    private bool TryCrossFade(string stateName, float transitionDuration = 0.25f, int layer = 0)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return false;

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(layer, stateHash))
        {
            #if UNITY_EDITOR
            Debug.LogWarning($"DialogueNPC: Animator state '{stateName}' not found on '{gameObject.name}'. " +
                             $"Add it as a state in the Animator Controller.");
            #endif
            return false;
        }

        animator.CrossFade(stateHash, transitionDuration, layer);
        return true;
    }

    // ─── Facing Helper ────────────────────────────────────────

    private void FaceTarget(Transform target)
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRot, facePlayerSpeed * Time.deltaTime);
    }

    /// <summary>Smoothly rotate the player toward this NPC during dialogue.</summary>
    private void FacePlayerTowardNPC(Transform player)
    {
        Vector3 dir = transform.position - player.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        player.rotation = Quaternion.Slerp(
            player.rotation, targetRot, facePlayerSpeed * Time.deltaTime);
    }

    // ─── Audio Helpers ────────────────────────────────────────

    private void FadeInVoice()
    {
        if (voiceSource == null || talkAmbientClip == null) return;

        if (voiceFadeCoroutine != null) StopCoroutine(voiceFadeCoroutine);
        voiceFadeCoroutine = StartCoroutine(FadeVoiceCoroutine(maxVoiceVolume, true));
    }

    private void FadeOutVoice()
    {
        if (voiceSource == null) return;

        if (voiceFadeCoroutine != null) StopCoroutine(voiceFadeCoroutine);
        voiceFadeCoroutine = StartCoroutine(FadeVoiceCoroutine(0f, false));
    }

    private IEnumerator FadeVoiceCoroutine(float targetVolume, bool startPlaying)
    {
        float startVol = voiceSource.volume;

        if (startPlaying && !voiceSource.isPlaying)
        {
            voiceSource.clip = talkAmbientClip;
            voiceSource.loop = true;
            voiceSource.Play();
        }

        float elapsed = 0f;
        while (elapsed < audioFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / audioFadeDuration);
            voiceSource.volume = Mathf.Lerp(startVol, targetVolume, t);
            yield return null;
        }

        voiceSource.volume = targetVolume;

        if (Mathf.Approximately(targetVolume, 0f))
        {
            voiceSource.Stop();
            voiceSource.loop = false;
        }
    }

    // ─── Public API ───────────────────────────────────────────

    /// <summary>
    /// Change the dialogue asset at runtime (e.g., after a quest is completed).
    /// </summary>
    public void SetDialogue(DialogueSO newDialogue)
    {
        dialogue = newDialogue;
    }

    /// <summary>
    /// Change the interaction prompt at runtime.
    /// </summary>
    public void SetPrompt(string newPrompt)
    {
        prompt = newPrompt;
    }

    // ─── Gizmos ───────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (maxDialogueDistance > 0f)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, maxDialogueDistance);
        }
    }
}
