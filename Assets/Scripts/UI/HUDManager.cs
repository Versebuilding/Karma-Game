using UnityEngine;

/// <summary>
/// Top-level HUD manager. Controls visibility of all HUD elements and
/// coordinates between dialogue mode and gameplay mode.
///
/// When dialogue is active, HUD elements (karma, coins) can stay visible
/// but the dialogue panel takes focus. When inventory/map opens, HUD hides.
///
/// Setup:
///   1. Create a "HUDCanvas" (Screen Space - Overlay, sort order 5)
///   2. Add KarmaFlowerUI, CoinCounterUI, KarmaPopupUI as children
///   3. Create a "DialogueCanvas" (Screen Space - Overlay, sort order 10)
///   4. Add DialogueUI as child
///   5. Attach this script to a GameManagers object (or the HUD Canvas)
///   6. Drag references to the relevant GameObjects
/// </summary>
public class HUDManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────
    public static HUDManager Instance { get; private set; }

    // ─── References ───────────────────────────────────────────
    [Header("HUD Elements")]
    [Tooltip("The HUD Canvas root (karma flower, coins, compass, etc.)")]
    [SerializeField] private GameObject hudCanvas;

    [Tooltip("The Dialogue Canvas root (dialogue panel, choices)")]
    [SerializeField] private GameObject dialogueCanvas;

    [Tooltip("Karma flower UI component")]
    [SerializeField] private KarmaFlowerUI karmaFlowerUI;

    [Tooltip("Coin counter UI component")]
    [SerializeField] private CoinCounterUI coinCounterUI;

    [Tooltip("Karma popup UI component")]
    [SerializeField] private KarmaPopupUI karmaPopupUI;

    [Tooltip("Dialogue UI component")]
    [SerializeField] private DialogueUI dialogueUI;

    [Header("Interaction Prompt")]
    [Tooltip("Interaction prompt panel (shows 'Press E to Talk', etc.)")]
    [SerializeField] private GameObject interactionPromptPanel;

    [Tooltip("Interaction prompt text")]
    [SerializeField] private TMPro.TMP_Text interactionPromptText;

    // ─── Runtime State ────────────────────────────────────────
    private bool isHudVisible = true;

    // ─── Properties ───────────────────────────────────────────

    /// <summary>Whether the HUD is currently visible.</summary>
    public bool IsHUDVisible => isHudVisible;

    // ─── Unity Lifecycle ──────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("HUDManager: Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        // Subscribe to dialogue events for auto-hiding interaction prompt
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStarted += HandleDialogueStarted;
            DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;
        }

        // Subscribe to interaction detector for prompt display
        SubscribeToInteractionDetector();
    }

    void OnDisable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStarted -= HandleDialogueStarted;
            DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;
        }
    }

    // ─── Public API ───────────────────────────────────────────

    /// <summary>Show the gameplay HUD (karma, coins, compass).</summary>
    public void ShowHUD()
    {
        isHudVisible = true;
        if (hudCanvas != null) hudCanvas.SetActive(true);
    }

    /// <summary>Hide the gameplay HUD (for inventory, map, cutscenes).</summary>
    public void HideHUD()
    {
        isHudVisible = false;
        if (hudCanvas != null) hudCanvas.SetActive(false);
    }

    /// <summary>Show the interaction prompt (e.g., "Press E to Talk").</summary>
    public void ShowInteractionPrompt(string promptText)
    {
        if (interactionPromptPanel != null)
            interactionPromptPanel.SetActive(true);

        if (interactionPromptText != null)
            interactionPromptText.text = $"Press E to {promptText}";
    }

    /// <summary>Hide the interaction prompt.</summary>
    public void HideInteractionPrompt()
    {
        if (interactionPromptPanel != null)
            interactionPromptPanel.SetActive(false);
    }

    // ─── Event Handlers ───────────────────────────────────────

    private void HandleDialogueStarted(DialogueSO dialogue)
    {
        // Hide interaction prompt during dialogue
        HideInteractionPrompt();
    }

    private void HandleDialogueEnded()
    {
        // Could restore interaction prompt if player is still near NPC
    }

    private void HandlePromptChanged(string promptText)
    {
        ShowInteractionPrompt(promptText);
    }

    private void HandlePromptHidden()
    {
        HideInteractionPrompt();
    }

    // ─── Interaction Detector Wiring ──────────────────────────

    private void SubscribeToInteractionDetector()
    {
        // Find the player's InteractionDetector and subscribe to its events
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null && player.interactionDetector != null)
        {
            player.interactionDetector.OnPromptChanged += HandlePromptChanged;
            player.interactionDetector.OnPromptHidden += HandlePromptHidden;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // Unsubscribe from interaction detector
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null && player.interactionDetector != null)
        {
            player.interactionDetector.OnPromptChanged -= HandlePromptChanged;
            player.interactionDetector.OnPromptHidden -= HandlePromptHidden;
        }
    }
}
