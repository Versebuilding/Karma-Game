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
    }

    void OnEnable()
    {
        // Subscribe to dialogue events
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;
        }
    }

    void OnDisable()
    {
        // Unsubscribe
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;
        }
    }

    void Update()
    {
        // Face player during dialogue
        if (isInDialogue && facePlayerDuringDialogue && playerTransform != null)
        {
            FaceTarget(playerTransform);
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

    /// <summary>Can interact only if a dialogue is assigned and no dialogue is active.</summary>
    public override bool CanInteract(PlayerController player)
    {
        if (dialogue == null) return false;
        if (DialogueManager.Instance == null) return false;
        if (DialogueManager.Instance.IsDialogueActive) return false;
        return true;
    }

    /// <summary>Start the dialogue conversation.</summary>
    public override void Interact(PlayerController player)
    {
        if (dialogue == null || DialogueManager.Instance == null) return;

        isInDialogue = true;
        playerTransform = player.transform;

        // Hide outline during dialogue
        if (outline != null)
            outline.enabled = false;

        // Start talking animation
        StartTalkingAnimation();

        // Fade in ambient voice
        FadeInVoice();

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

    private void HandleDialogueEnded()
    {
        if (!isInDialogue) return;

        isInDialogue = false;

        // Stop talking animation
        StopTalkingAnimation();

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
        if (animCycler != null)
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
        if (animCycler != null)
        {
            animCycler.SetTalking(false);
        }
        else if (animator != null)
        {
            animator.SetBool(talkingBoolParam, false);
        }
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
