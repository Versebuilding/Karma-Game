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

    // ─── Events ───────────────────────────────────────────────

    /// <summary>Fired when the button is clicked. Arg: choice index.</summary>
    public event Action<int> OnClicked;

    // ─── Runtime ──────────────────────────────────────────────
    private int choiceIndex;
    private bool isAvailable;

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
        ApplyStyle(choice.choiceStyle, available);

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

    // ─── Style Application ────────────────────────────────────

    private void ApplyStyle(ChoiceStyle style, bool available)
    {
        Color bgColor;
        Color textColor;

        switch (style)
        {
            case ChoiceStyle.Empathetic:
                bgColor = empatheticColor;
                textColor = empatheticTextColor;
                break;
            case ChoiceStyle.Selfish:
                bgColor = selfishColor;
                textColor = selfishTextColor;
                break;
            default: // Neutral
                bgColor = neutralColor;
                textColor = neutralTextColor;
                break;
        }

        if (!available)
        {
            bgColor = lockedColor;
            textColor = new Color(textColor.r, textColor.g, textColor.b, lockedAlpha);
        }

        if (backgroundImage != null)
            backgroundImage.color = bgColor;

        if (choiceText != null)
            choiceText.color = textColor;
    }
}
