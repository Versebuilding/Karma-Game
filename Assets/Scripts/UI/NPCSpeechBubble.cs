using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// NPC speech bubble — single shared Screen Space Overlay panel positioned above
/// the speaking NPC via WorldToScreenPoint on the NPC's renderer-bounds top.
///
/// Why this design (industry-standard for floating labels):
///  - ONE screen-space canvas, built in code — no per-NPC world-space canvases,
///    no prefab scale/pivot fragility, text is always crisp.
///  - The anchor is computed from combined Renderer bounds each frame, so any
///    NPC height, import scale, or offset pivot works with zero tuning.
///  - Clamped to screen edges; hidden when the NPC is behind the camera.
///
/// One component instance still lives on each NPC (as a child), preserving the
/// existing wiring: DialogueUI finds it via GetComponentInChildren, and the
/// static registry maps NPC transform -> bubble. All rendering goes through the
/// shared panel; only the bubble whose NPC is actively speaking owns it.
/// </summary>
public class NPCSpeechBubble : MonoBehaviour
{
    // ─── Static Registry ────────────────────────────────────────
    private static readonly System.Collections.Generic.Dictionary<Transform, NPCSpeechBubble>
        registry = new System.Collections.Generic.Dictionary<Transform, NPCSpeechBubble>();

    /// <summary>Find the speech bubble associated with a given NPC transform.</summary>
    public static NPCSpeechBubble GetBubbleForNPC(Transform npc)
    {
        if (npc != null && registry.TryGetValue(npc, out var bubble))
            return bubble;
        return null;
    }

    // ─── Settings ────────────────────────────────────────────────
    [Header("Positioning")]
    [Tooltip("The NPC transform this bubble belongs to (auto-found from parent if empty)")]
    [SerializeField] private Transform targetNPC;

    [Tooltip("Extra world-space clearance above the NPC's renderer-bounds top")]
    [SerializeField] private float headClearance = 0.35f;

    [Header("Typewriter")]
    [SerializeField] private bool useTypewriter = true;
    [Tooltip("Characters per second")]
    [SerializeField] private float typewriterSpeed = 40f;

    [Header("Continue Prompt")]
    [SerializeField] private string continuePromptText = "Press Enter >>";

    // ─── Shared UI (one panel for the whole game, built lazily) ──
    private static Canvas sharedCanvas;
    private static RectTransform panelRT;
    private static CanvasGroup panelGroup;
    private static TMP_Text nameText;
    private static TMP_Text bodyText;
    private static TMP_Text promptText;
    private static NPCSpeechBubble activeOwner;

    private const float PanelWidth = 540f;
    private const float PanelMinHeight = 140f;
    private const float ScreenMargin = 12f;

    // ─── Runtime ────────────────────────────────────────────────
    private Camera mainCamera;
    private Renderer[] targetRenderers;
    private Coroutine typewriterCoroutine;
    private bool isTypewriting;
    private bool isSubscribed;

    /// <summary>Whether the bubble is currently revealing text character by character.</summary>
    public bool IsTypewriting => isTypewriting;

    // ─── Unity Lifecycle ────────────────────────────────────────

    void Awake()
    {
        mainCamera = Camera.main;

        // Auto-find the NPC this bubble belongs to.
        if (targetNPC == null)
        {
            var npc = GetComponentInParent<NPCBase>();
            targetNPC = npc != null ? npc.transform : transform.parent;
        }

        if (targetNPC != null)
        {
            registry[targetNPC] = this;
            targetRenderers = targetNPC.GetComponentsInChildren<Renderer>();
        }

        // Legacy cleanup: older prefabs carried a world-space Canvas child under
        // this object. Disable it so it can't render a ghost bubble.
        var legacyCanvas = GetComponentInChildren<Canvas>(true);
        if (legacyCanvas != null)
            legacyCanvas.gameObject.SetActive(false);
    }

    void OnEnable() { TrySubscribe(); }
    void Start() { TrySubscribe(); }

    void OnDisable()
    {
        Unsubscribe();
        if (activeOwner == this)
            HidePanel();
    }

    void OnDestroy()
    {
        if (targetNPC != null && registry.TryGetValue(targetNPC, out var b) && b == this)
            registry.Remove(targetNPC);
    }

    private void TrySubscribe()
    {
        if (isSubscribed || DialogueManager.Instance == null) return;
        DialogueManager.Instance.OnNodeChanged += HandleNodeChanged;
        DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || DialogueManager.Instance == null) return;
        DialogueManager.Instance.OnNodeChanged -= HandleNodeChanged;
        DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;
        isSubscribed = false;
    }

    // ─── Dialogue Events ────────────────────────────────────────

    private void HandleNodeChanged(DialogueNode node)
    {
        if (node == null) return;

        // Only the bubble belonging to the actively speaking NPC responds.
        var active = DialogueManager.Instance != null ? DialogueManager.Instance.ActiveNPCTransform : null;
        if (active != targetNPC)
        {
            if (activeOwner == this) HidePanel();
            return;
        }

        string speaker = !string.IsNullOrEmpty(node.speakerName)
            ? node.speakerName
            : DialogueManager.Instance.ActiveNPCSpeakerName;
        SetText(string.IsNullOrEmpty(speaker) ? targetNPC.name : speaker, node.dialogueText);
        Show();
    }

    private void HandleDialogueEnded()
    {
        if (activeOwner == this)
            Hide();
    }

    // ─── Public API (kept compatible with DialogueUI) ───────────

    public void SetTarget(Transform npcTransform)
    {
        if (targetNPC != null && registry.TryGetValue(targetNPC, out var b) && b == this)
            registry.Remove(targetNPC);
        targetNPC = npcTransform;
        if (targetNPC != null)
        {
            registry[targetNPC] = this;
            targetRenderers = targetNPC.GetComponentsInChildren<Renderer>();
        }
    }

    public void Show()
    {
        EnsureSharedUI();
        activeOwner = this;
        panelRT.gameObject.SetActive(true);
        panelGroup.alpha = 1f;
        PositionPanel();
    }

    public void Hide()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTypewriting = false;
        if (activeOwner == this)
            HidePanel();
    }

    public void HideContinuePrompt()
    {
        if (promptText != null)
            promptText.gameObject.SetActive(false);
    }

    public void SetText(string speaker, string text)
    {
        EnsureSharedUI();
        activeOwner = this;

        nameText.text = speaker;
        bodyText.text = text;
        promptText.gameObject.SetActive(false);

        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        if (useTypewriter && gameObject.activeInHierarchy)
        {
            typewriterCoroutine = StartCoroutine(TypewriterRoutine(text.Length));
        }
        else
        {
            bodyText.maxVisibleCharacters = int.MaxValue;
            isTypewriting = false;
            promptText.gameObject.SetActive(true);
            promptText.text = continuePromptText;
        }
    }

    public void SkipTypewriter()
    {
        if (!isTypewriting) return;
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTypewriting = false;
        bodyText.maxVisibleCharacters = int.MaxValue;
        promptText.gameObject.SetActive(true);
        promptText.text = continuePromptText;
    }

    private IEnumerator TypewriterRoutine(int totalChars)
    {
        isTypewriting = true;
        bodyText.maxVisibleCharacters = 0;
        float shown = 0f;
        while (shown < totalChars)
        {
            shown += typewriterSpeed * Time.deltaTime;
            bodyText.maxVisibleCharacters = Mathf.Min(totalChars, Mathf.FloorToInt(shown));
            yield return null;
        }
        bodyText.maxVisibleCharacters = int.MaxValue;
        isTypewriting = false;
        typewriterCoroutine = null;
        promptText.gameObject.SetActive(true);
        promptText.text = continuePromptText;
    }

    // ─── Positioning ────────────────────────────────────────────

    void LateUpdate()
    {
        if (activeOwner != this || panelRT == null || !panelRT.gameObject.activeSelf)
            return;
        PositionPanel();
    }

    private void PositionPanel()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null || targetNPC == null) return;

        Vector3 anchor = GetHeadAnchor();
        Vector3 screen = mainCamera.WorldToScreenPoint(anchor);

        // NPC behind the camera — hide until visible again.
        if (screen.z <= 0f)
        {
            panelGroup.alpha = 0f;
            return;
        }
        panelGroup.alpha = 1f;

        // Panel pivot is bottom-center: it grows upward from the anchor.
        // Clamp fully on-screen accounting for the canvas scale factor.
        float scale = sharedCanvas.scaleFactor;
        float halfW = PanelWidth * 0.5f * scale;
        float panelH = panelRT.rect.height * scale;

        float x = Mathf.Clamp(screen.x, halfW + ScreenMargin, Screen.width - halfW - ScreenMargin);
        float y = Mathf.Clamp(screen.y, ScreenMargin, Screen.height - panelH - ScreenMargin);

        panelRT.position = new Vector3(x, y, 0f);
    }

    /// <summary>
    /// World point just above the NPC's head: top of the combined renderer
    /// bounds. Works for any model height, scale, or pivot offset.
    /// </summary>
    private Vector3 GetHeadAnchor()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = targetNPC.GetComponentsInChildren<Renderer>();

        bool has = false;
        Bounds combined = default;
        foreach (var r in targetRenderers)
        {
            if (r == null || !r.enabled) continue;
            if (!has) { combined = r.bounds; has = true; }
            else combined.Encapsulate(r.bounds);
        }
        if (!has)
            return targetNPC.position + Vector3.up * 2f;

        return new Vector3(combined.center.x, combined.max.y + headClearance, combined.center.z);
    }

    private void HidePanel()
    {
        activeOwner = null;
        if (panelRT != null)
            panelRT.gameObject.SetActive(false);
    }

    // ─── Shared UI Construction (code-built, no prefab) ─────────

    private static void EnsureSharedUI()
    {
        if (sharedCanvas != null) return;

        var canvasGO = new GameObject("SpeechBubbleCanvas");
        sharedCanvas = canvasGO.AddComponent<Canvas>();
        sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        sharedCanvas.sortingOrder = 8; // above HUD (5), below DialogueCanvas (10)

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Panel — cream rounded look, bottom-center pivot so it sits above the anchor.
        var panelGO = new GameObject("BubblePanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(PanelWidth, PanelMinHeight);
        panelRT.pivot = new Vector2(0.5f, 0f);
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);

        // Art assets (loaded from Assets/UI/Resources — same sprites the old
        // hand-built prefab used). Fallback to flat cream if missing.
        var panelSprite = Resources.Load<Sprite>("Dialogue_opponentBG");
        var tagSprite = Resources.Load<Sprite>("Dialogue_nametage");

        var bg = panelGO.AddComponent<Image>();
        if (panelSprite != null)
        {
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
        }
        else
        {
            bg.color = new Color(1f, 0.97f, 0.9f, 0.95f);
        }

        panelGroup = panelGO.AddComponent<CanvasGroup>();
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;

        // Height grows with the text.
        var fitter = panelGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var layout = panelGO.AddComponent<VerticalLayoutGroup>();
        // Extra top padding leaves room for the name tag overlapping the top edge.
        layout.padding = new RectOffset(26, 22, 30, 30);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        // Speaker name tag — orange badge straddling the panel's top-left edge,
        // excluded from the vertical layout (matches the original bubble design).
        var tagGO = new GameObject("SpeakerTag");
        tagGO.transform.SetParent(panelGO.transform, false);
        var tagLE = tagGO.AddComponent<LayoutElement>();
        tagLE.ignoreLayout = true;
        var tagRT = tagGO.GetComponent<RectTransform>();
        tagRT.anchorMin = tagRT.anchorMax = new Vector2(0f, 1f); // panel top-left
        tagRT.pivot = new Vector2(0f, 0.5f);
        tagRT.sizeDelta = new Vector2(140f, 40f);
        tagRT.anchoredPosition = new Vector2(14f, -6f); // straddles the top edge

        var tagBg = tagGO.AddComponent<Image>();
        if (tagSprite != null)
        {
            tagBg.sprite = tagSprite;
            tagBg.type = Image.Type.Sliced;
            tagBg.color = Color.white;
        }
        else
        {
            tagBg.color = new Color(0.85f, 0.5f, 0.1f, 1f);
        }

        var nameGO = new GameObject("SpeakerName");
        nameGO.transform.SetParent(tagGO.transform, false);
        var nameRT = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin = Vector2.zero;
        nameRT.anchorMax = Vector2.one;
        nameRT.offsetMin = new Vector2(12f, 2f);
        nameRT.offsetMax = new Vector2(-12f, -2f);
        nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 18;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Midline;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 12;
        nameText.fontSizeMax = 18;

        // Speech body.
        var bodyGO = new GameObject("SpeechText");
        bodyGO.transform.SetParent(panelGO.transform, false);
        bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
        bodyText.fontSize = 23;
        bodyText.color = new Color(0.15f, 0.1f, 0.05f);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        // Continue prompt — hangs BELOW the panel, outside the box, bottom-right.
        // Excluded from the vertical layout; follows the panel automatically.
        var promptGO = new GameObject("ContinuePrompt");
        promptGO.transform.SetParent(panelGO.transform, false);
        var promptLE = promptGO.AddComponent<LayoutElement>();
        promptLE.ignoreLayout = true;
        var promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.anchorMin = promptRT.anchorMax = new Vector2(1f, 0f); // panel bottom-right
        promptRT.pivot = new Vector2(1f, 1f);
        promptRT.sizeDelta = new Vector2(220f, 26f);
        promptRT.anchoredPosition = new Vector2(-10f, -6f); // just under the box
        promptText = promptGO.AddComponent<TextMeshProUGUI>();
        promptText.fontSize = 16;
        promptText.fontStyle = FontStyles.Italic;
        promptText.color = new Color(1f, 0.97f, 0.9f, 0.9f); // cream, readable over the world
        promptText.alignment = TextAlignmentOptions.TopRight;
        promptGO.SetActive(false);

        panelGO.SetActive(false);
    }
}
