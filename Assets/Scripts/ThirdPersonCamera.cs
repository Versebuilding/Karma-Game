using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Third-person camera with two modes:
///   - Follow mode (default): matches player rotation, follows position
///   - Dialogue mode: over-the-shoulder shot with FOV zoom + depth-of-field blur
///
/// This script sits on the CameraRig parent. The actual Camera is a child with
/// a local offset (creating the third-person view). During dialogue the rig
/// rotates toward the NPC and shifts slightly right for over-the-shoulder
/// framing. The child camera's local offset does the rest.
///
/// Dialogue enhancements:
///   - Right shoulder offset so NPC isn't blocked by player
///   - Narrower FOV for cinematic zoom / focus on NPC
///   - URP Depth of Field (Bokeh) for background blur
///   - All transitions are smoothly animated
///
/// Subscribes to DialogueManager events to switch between modes.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    // ─── Follow Mode ──────────────────────────────────────────
    [Header("Follow Mode")]
    public Transform player;
    public float smoothSpeed = 8f;

    // ─── Dialogue Framing ────────────────────────────────────
    [Header("Dialogue Framing")]
    [Tooltip("Horizontal offset to the right for over-the-shoulder framing")]
    [Range(0f, 3f)]
    [SerializeField] private float shoulderOffset = 1.8f;

    [Tooltip("Forward offset toward the NPC (positive = camera closer to conversation midpoint)")]
    [Range(0f, 5f)]
    [SerializeField] private float dialogueForwardOffset = 1.5f;

    [Tooltip("Vertical offset during dialogue (negative = camera lower, slight upward angle)")]
    [Range(-2f, 1f)]
    [SerializeField] private float dialogueVerticalOffset = 0.2f;

    [Tooltip("Transition speed into/out of dialogue camera")]
    [SerializeField] private float dialogueTransitionSpeed = 5f;

    // ─── Dialogue Zoom ───────────────────────────────────────
    [Header("Dialogue Zoom")]
    [Tooltip("FOV during dialogue (narrower = more zoomed in on NPC)")]
    [Range(20f, 70f)]
    [SerializeField] private float dialogueFOV = 50f;

    [Tooltip("Speed of FOV and DoF transitions")]
    [SerializeField] private float effectTransitionSpeed = 4f;

    // ─── Dialogue Depth of Field ─────────────────────────────
    [Header("Dialogue Depth of Field")]
    [Tooltip("Enable background blur (bokeh) during dialogue")]
    [SerializeField] private bool useDepthOfField = true;

    [Tooltip("Bokeh focal length (higher = stronger background blur)")]
    [Range(20f, 200f)]
    [SerializeField] private float dofFocalLength = 80f;

    // ─── Runtime State ────────────────────────────────────────
    private bool isInDialogue;
    private Transform npcTarget;
    private bool isSubscribed;
    private Camera childCamera;
    private float normalFOV;
    private Volume dialogueVolume;
    private DepthOfField dofOverride;

    // ─── Unity Lifecycle ──────────────────────────────────────

    void Awake()
    {
        // Find the child camera (Main Camera is a child of this CameraRig)
        childCamera = GetComponentInChildren<Camera>();
        if (childCamera != null)
            normalFOV = childCamera.fieldOfView;
        else
            normalFOV = 60f;

        SetupDepthOfField();
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        // Fallback if DialogueManager wasn't ready during OnEnable
        TrySubscribe();
    }

    void OnDisable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStarted -= HandleDialogueStarted;
            DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;
        }
        isSubscribed = false;
    }

    void OnDestroy()
    {
        // Clean up the runtime-created VolumeProfile to avoid memory leak
        if (dialogueVolume != null && dialogueVolume.profile != null)
            Destroy(dialogueVolume.profile);
    }

    private void TrySubscribe()
    {
        if (isSubscribed) return;
        if (DialogueManager.Instance == null) return;

        DialogueManager.Instance.OnDialogueStarted += HandleDialogueStarted;
        DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;
        isSubscribed = true;
    }

    // ─── Depth of Field Setup ────────────────────────────────

    /// <summary>
    /// Creates a runtime URP Volume with Bokeh DoF override on the child camera.
    /// Starts at weight 0 (invisible); weight is animated to 1 during dialogue.
    /// </summary>
    private void SetupDepthOfField()
    {
        if (!useDepthOfField || childCamera == null) return;

        // Ensure post-processing is enabled on the camera (required for URP DoF)
        var cameraData = childCamera.GetUniversalAdditionalCameraData();
        if (cameraData != null)
            cameraData.renderPostProcessing = true;

        // Create a high-priority global Volume parented to the camera
        var volumeObj = new GameObject("DialogueDoFVolume");
        volumeObj.transform.SetParent(childCamera.transform, false);
        volumeObj.hideFlags = HideFlags.HideAndDontSave;

        dialogueVolume = volumeObj.AddComponent<Volume>();
        dialogueVolume.isGlobal = true;
        dialogueVolume.priority = 100; // Override any scene volumes
        dialogueVolume.weight = 0f;    // Start invisible

        // Create an in-memory VolumeProfile with quality-adaptive DoF
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        dofOverride = profile.Add<DepthOfField>();

        // Quality-adaptive: Bokeh on PC (prettier), Gaussian on mobile (cheaper)
        bool useBokeh = !IsLowQuality();
        dofOverride.mode.Override(useBokeh ? DepthOfFieldMode.Bokeh : DepthOfFieldMode.Gaussian);
        dofOverride.focusDistance.Override(5f);       // Updated dynamically each frame

        if (useBokeh)
        {
            dofOverride.focalLength.Override(dofFocalLength);
        }
        else
        {
            // Gaussian settings — start/end range for blur (cheaper on mobile)
            dofOverride.gaussianStart.Override(3f);
            dofOverride.gaussianEnd.Override(15f);
            dofOverride.gaussianMaxRadius.Override(1f);
        }

        dialogueVolume.profile = profile;
    }

    // ─── Quality Detection ────────────────────────────────────

    /// <summary>
    /// Returns true if running on a low-quality tier (mobile URP asset or low VRAM).
    /// Used to select cheaper DoF mode (Gaussian instead of Bokeh).
    /// </summary>
    private bool IsLowQuality()
    {
        var rpAsset = GraphicsSettings.currentRenderPipeline;
        if (rpAsset != null && rpAsset.name.Contains("Mobile"))
            return true;
        return SystemInfo.graphicsMemorySize < 2048;
    }

    // ─── Event Handlers ───────────────────────────────────────

    private void HandleDialogueStarted(DialogueSO dialogue)
    {
        isInDialogue = true;
        npcTarget = DialogueManager.Instance.ActiveNPCTransform;
    }

    private void HandleDialogueEnded()
    {
        isInDialogue = false;
        npcTarget = null;
    }

    // ─── Camera Update ────────────────────────────────────────

    void LateUpdate()
    {
        if (!player) return;

        if (isInDialogue && npcTarget != null)
            UpdateDialogueCamera();
        else
            UpdateFollowCamera();
    }

    /// <summary>Default follow camera: match player rotation and position.</summary>
    private void UpdateFollowCamera()
    {
        Quaternion targetRotation = Quaternion.Euler(
            0,
            player.eulerAngles.y,
            0
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            smoothSpeed * Time.deltaTime
        );

        transform.position = player.position;

        // ── Smoothly restore normal FOV when exiting dialogue ──
        if (childCamera != null && Mathf.Abs(childCamera.fieldOfView - normalFOV) > 0.1f)
        {
            childCamera.fieldOfView = Mathf.Lerp(
                childCamera.fieldOfView, normalFOV,
                effectTransitionSpeed * Time.deltaTime);
        }

        // ── Fade out DoF blur ──
        if (dialogueVolume != null && dialogueVolume.weight > 0.01f)
        {
            dialogueVolume.weight = Mathf.Lerp(
                dialogueVolume.weight, 0f,
                effectTransitionSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Dialogue camera: over-the-shoulder behind the player, looking at the NPC.
    ///
    /// This script is on the CameraRig parent — the actual Camera is a child
    /// with a local offset. The rig stays near the player and rotates toward
    /// the NPC; the child offset naturally creates the third-person framing.
    ///
    /// Enhancements over follow mode:
    ///   - Shoulder offset shifts rig right so NPC isn't blocked by player body
    ///   - FOV narrows for cinematic zoom onto the NPC + dialogue bubble
    ///   - URP Bokeh DoF blurs the background, keeping NPC in sharp focus
    /// </summary>
    private void UpdateDialogueCamera()
    {
        // Direction from player to NPC (horizontal only)
        Vector3 dirToNPC = npcTarget.position - player.position;
        dirToNPC.y = 0f;

        if (dirToNPC.sqrMagnitude < 0.01f) return;
        dirToNPC.Normalize();

        // ── Shoulder + forward offset ──
        // Cross(up, dirToNPC) gives the right-hand perpendicular direction.
        // Moving the rig to the right shifts the camera right, pushing
        // the player silhouette left of frame and revealing the NPC.
        // Forward offset pushes the camera closer to the conversation midpoint.
        Vector3 right = Vector3.Cross(Vector3.up, dirToNPC).normalized;
        Vector3 targetPos = player.position
            + right * shoulderOffset
            + dirToNPC * dialogueForwardOffset
            + Vector3.up * dialogueVerticalOffset;

        float t = dialogueTransitionSpeed * Time.deltaTime;
        transform.position = Vector3.Lerp(transform.position, targetPos, t);

        // ── Rotate rig to face the NPC ──
        Quaternion targetRot = Quaternion.LookRotation(dirToNPC);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);

        // ── FOV zoom ──
        if (childCamera != null)
        {
            childCamera.fieldOfView = Mathf.Lerp(
                childCamera.fieldOfView, dialogueFOV,
                effectTransitionSpeed * Time.deltaTime);
        }

        // ── Depth of Field — focus on NPC, blur surroundings ──
        if (dialogueVolume != null)
        {
            // Fade in the DoF effect
            dialogueVolume.weight = Mathf.Lerp(
                dialogueVolume.weight, 1f,
                effectTransitionSpeed * Time.deltaTime);

            // Dynamic focus distance — keep NPC body in sharp focus
            if (dofOverride != null && childCamera != null)
            {
                Vector3 npcCenter = npcTarget.position + Vector3.up * 1.5f;
                float focusDist = Vector3.Distance(
                    childCamera.transform.position, npcCenter);
                dofOverride.focusDistance.Override(focusDist);
            }
        }
    }
}
