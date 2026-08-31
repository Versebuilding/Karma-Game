using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for a single dialogue choice button.
/// Displays the choice text, input label (Z/X/C), and colors
/// based on the choice style (Empathetic, Selfish, Neutral).
///
/// Setup (on the ChoiceButton prefab):
///   1. Root: Button component + Image (background)
///   2. Child: TMP_Text for input label (circle with Z/X/C)
///   3. Child: TMP_Text for choice text
///   4. Optionally: Image for input label background circle
///   5. Attach this script to the root
/// </summary>
public class ChoiceButtonUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Background image of the choice button")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Text showing the input key (Z, X, C)")]
    [SerializeField] private TMP_Text inputLabelText;

    [Tooltip("Text showing the choice description")]
    [SerializeField] private TMP_Text choiceText;

    [Tooltip("Circle/badge behind the input label")]
    [SerializeField] private Image inputLabelBadge;

    [Tooltip("Optional: Button component for click support")]
    [SerializeField] private Button button;

    [Header("Style Colors")]
    [Tooltip("Background color for empathetic choices")]
    [SerializeField] private Color empatheticColor = new Color(1f, 0.65f, 0.2f, 1f);  // warm orange

    [Tooltip("Background color for selfish choices")]
    [SerializeField] private Color selfishColor = new Color(0.35f, 0.35f, 0.4f, 1f);  // dark gray

    [Tooltip("Background color for neutral choices")]
    [SerializeField] private Color neutralColor = new Color(0.95f, 0.95f, 0.9f, 1f);  // off-white

    [Tooltip("Text color for empathetic choices")]
    [SerializeField] private Color empatheticTextColor = Color.white;

    [Tooltip("Text color for selfish choices")]
    [SerializeField] private Color selfishTextColor = new Color(0.9f, 0.85f, 0.85f, 1f);

    [Tooltip("Text color for neutral choices")]
    [SerializeField] private Color neutralTextColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    [Tooltip("Color when the choice is locked (karma requirement not met)")]
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);

    [Tooltip("Alpha for locked choices")]
    [Range(0.2f, 0.8f)]
    [SerializeField] private float lockedAlpha = 0.4f;

    [Header("Text Sizing")]
    [Tooltip("Minimum font size for choice text")]
    [Range(12f, 24f)]
    [SerializeField] private float minChoiceFontSize = 18f;

    [Tooltip("Maximum font size for choice text")]
    [Range(16f, 30f)]
    [SerializeField] private float maxChoiceFontSize = 22f;

    [Tooltip("Vertical padding above and below text for background auto-size")]
    [SerializeField] private float verticalPadding = 24f;

    [Tooltip("Minimum button height")]
    [SerializeField] private float minButtonHeight = 50f;

    // ─── Events ───────────────────────────────────────────────

    /// <summary>Fired when the button is clicked. Arg: choice index.</summary>
    public event Action<int> OnClicked;

    // ─── Runtime ──────────────────────────────────────────────
    private int choiceIndex;
    private bool isAvailable;
    private ChoiceStyle currentStyle;
    private bool isSelected;

    // ─── Public API ───────────────────────────────────────────

    /// <summary>
    /// Configure this choice button with data from the dialogue system.
    /// </summary>
    public void Setup(DialogueChoice choice, string inputLabel, int index, bool available)
    {
        choiceIndex = index;
        isAvailable = available;

        // Set text
        if (inputLabelText != null)
            inputLabelText.text = inputLabel;

        if (choiceText != null)
        {
            choiceText.text = choice.choiceText;

            // Show lock reason if choice is unavailable
            if (!available)
            {
                string lockReason = GetLockReason(choice);
                if (!string.IsNullOrEmpty(lockReason))
                {
                    choiceText.text += $" <size=70%><color=#888>({lockReason})</color></size>";
                }
            }
        }

        // Apply style colors
        currentStyle = choice.choiceStyle;
        isSelected = false;
        ApplyStyle(choice.choiceStyle, available);

        // Auto-size the button height to fit the text content (fixed width from parent)
        AutoSizeHeight();

        // Wire up button click
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.interactable = available;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClicked?.Invoke(choiceIndex));
        }
    }

    /// <summary>
    /// Determine why a choice is locked. Checks both legacy fields and new conditions.
    /// </summary>
    private string GetLockReason(DialogueChoice choice)
    {
        // Legacy: check requiredKarmaLevel
        if (choice.requiredKarmaLevel > 0)
        {
            if (KarmaManager.Instance == null ||
                KarmaManager.Instance.CurrentLevel < choice.requiredKarmaLevel)
            {
                return $"Requires Karma Lv.{choice.requiredKarmaLevel}";
            }
        }

        // New: check extensible conditions
        if (choice.conditions != null)
        {
            foreach (var cond in choice.conditions)
            {
                if (cond != null && !cond.Evaluate())
                {
                    return $"Requires: {cond.Label}";
                }
            }
        }

        return "Locked";
    }

    // ─── Selection State ────────────────────────────────────

    /// <summary>
    /// Show this choice as selected (fills with style color, used for visual feedback).
    /// Matches Figma mockup: selected empathetic choice turns fully orange.
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (!isAvailable) return;

        if (selected)
        {
            // Selected = orange fill with white text; badge darkens for contrast.
            if (backgroundImage != null)
                backgroundImage.color = UnifiedBadge;

            if (choiceText != null)
                choiceText.color = Color.white;

            if (inputLabelBadge != null)
                inputLabelBadge.color = new Color(0.85f, 0.5f, 0.1f, 1f);
        }
        else
        {
            // Restore default style
            ApplyStyle(currentStyle, isAvailable);
        }
    }

    // ─── Auto-Size ──────────────────────────────────────────

    /// <summary>
    /// Auto-sizes the button height to fit the choice text content.
    /// Width stays fixed (controlled by the parent layout group).
    /// Applies the same min/max font sizing (24–28pt) as the dialogue UIs.
    /// </summary>
    private void AutoSizeHeight()
    {
        if (choiceText == null) return;

        // Enable word wrapping and auto-sizing with the shared font range
        choiceText.textWrappingMode = TextWrappingModes.Normal;
        choiceText.enableAutoSizing = true;
        choiceText.fontSizeMin = minChoiceFontSize;
        choiceText.fontSizeMax = maxChoiceFontSize;
        choiceText.ForceMeshUpdate();

        float textHeight = choiceText.preferredHeight;
        float targetHeight = Mathf.Max(textHeight + verticalPadding, minButtonHeight);

        // Use LayoutElement.preferredHeight so the parent layout group respects this
        var le = GetComponent<LayoutElement>();
        if (le == null)
            le = gameObject.AddComponent<LayoutElement>();

        le.preferredHeight = targetHeight;
    }

    // ─── Style Application ────────────────────────────────────

    // Unified visual language: every choice uses the same cream panel with an
    // orange key badge (matching the speech bubble), regardless of choice style.
    // Style still drives karma outcomes — just not the resting colors.
    private static readonly Color UnifiedBg = new Color(1f, 0.97f, 0.9f, 0.95f);      // cream
    private static readonly Color UnifiedText = new Color(0.15f, 0.1f, 0.05f, 1f);    // dark brown
    private static readonly Color UnifiedBadge = new Color(1f, 0.65f, 0.2f, 1f);      // orange

    private void ApplyStyle(ChoiceStyle style, bool available)
    {
        Color bgColor = UnifiedBg;
        Color textColor = UnifiedText;

        if (!available)
        {
            bgColor = lockedColor;
            textColor = new Color(textColor.r, textColor.g, textColor.b, lockedAlpha);
        }

        if (backgroundImage != null)
            backgroundImage.color = bgColor;

        if (choiceText != null)
            choiceText.color = textColor;

        if (inputLabelBadge != null)
            inputLabelBadge.color = UnifiedBadge;
    }
}
