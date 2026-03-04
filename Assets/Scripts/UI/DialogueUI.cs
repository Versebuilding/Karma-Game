using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the dialogue UI panel. Subscribes to DialogueManager events to show/hide
/// the dialogue panel, display NPC speech text, spawn choice buttons, and handle
/// player input (Z/X/C for choices, E/Space to advance).
///
/// Setup:
///   1. Create a Canvas (Screen Space - Overlay, sort order 10)
///   2. Add a panel at the bottom for the dialogue box (dialoguePanel)
///   3. Add TMP_Text for speaker name and dialogue text
///   4. Add an empty GameObject as choiceContainer (Vertical/Horizontal Layout)
///   5. Create a ChoiceButton prefab with ChoiceButtonUI component
///   6. Add a "Press E to continue" indicator (continuePrompt)
///   7. Attach this script and drag references
/// </summary>
public class DialogueUI : MonoBehaviour
{
    // ─── References ───────────────────────────────────────────
    [Header("Dialogue Panel")]
    [Tooltip("The root dialogue panel (enabled/disabled)")]
    [SerializeField] private GameObject dialoguePanel;

    [Tooltip("Speaker name text")]
    [SerializeField] private TMP_Text speakerNameText;

    [Tooltip("Dialogue body text")]
    [SerializeField] private TMP_Text dialogueText;

    [Header("Choices")]
    [Tooltip("Parent transform for spawned choice buttons")]
    [SerializeField] private Transform choiceContainer;

    [Tooltip("Prefab with ChoiceButtonUI component")]
    [SerializeField] private GameObject choiceButtonPrefab;

    [Header("Continue Prompt")]
    [Tooltip("'Press E to continue' indicator (shown for non-choice nodes)")]
    [SerializeField] private GameObject continuePrompt;

    [Header("Typewriter Effect")]
    [Tooltip("Enable typewriter text reveal effect")]
    [SerializeField] private bool useTypewriter = true;

    [Tooltip("Characters per second for typewriter effect")]
    [Range(10f, 120f)]
    [SerializeField] private float typewriterSpeed = 40f;

    [Header("Audio")]
    [Tooltip("AudioSource for UI sounds (click, advance)")]
    [SerializeField] private AudioSource uiAudioSource;

    [Tooltip("Sound played when advancing dialogue")]
    [SerializeField] private AudioClip advanceSound;

    [Tooltip("Sound played when selecting a choice")]
    [SerializeField] private AudioClip choiceSound;

    // ─── Runtime State ────────────────────────────────────────
    private bool isShowingChoices;
    private bool isTypewriting;
    private Coroutine typewriterCoroutine;
    private string fullDialogueText;
    private int currentChoiceCount;

    // ─── Input Keys ───────────────────────────────────────────
    private readonly KeyCode[] choiceKeys = { KeyCode.Z, KeyCode.X, KeyCode.C };
    private readonly KeyCode advanceKey1 = KeyCode.E;
    private readonly KeyCode advanceKey2 = KeyCode.Space;

    // ─── Unity Lifecycle ──────────────────────────────────────

    void Awake()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (uiAudioSource == null)
            uiAudioSource = GetComponent<AudioSource>();
    }

    void OnEnable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStarted += HandleDialogueStarted;
            DialogueManager.Instance.OnNodeChanged += HandleNodeChanged;
            DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;
        }
    }

    void OnDisable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStarted -= HandleDialogueStarted;
            DialogueManager.Instance.OnNodeChanged -= HandleNodeChanged;
            DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;
        }
    }

    void Update()
    {
        if (DialogueManager.Instance == null || !DialogueManager.Instance.IsDialogueActive)
            return;

        // If typewriter is still running, pressing E/Space skips to full text
        if (isTypewriting)
        {
            if (Input.GetKeyDown(advanceKey1) || Input.GetKeyDown(advanceKey2))
            {
                SkipTypewriter();
            }
            return;
        }

        // Handle choice input (Z/X/C or 1/2/3)
        if (isShowingChoices)
        {
            for (int i = 0; i < currentChoiceCount && i < choiceKeys.Length; i++)
            {
                if (Input.GetKeyDown(choiceKeys[i]) || Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    OnChoiceSelected(i);
                    return;
                }
            }
        }
        else
        {
            // Non-choice node: E or Space to advance
            if (Input.GetKeyDown(advanceKey1) || Input.GetKeyDown(advanceKey2))
            {
                OnAdvance();
            }
        }
    }

    // ─── Event Handlers ───────────────────────────────────────

    private void HandleDialogueStarted(DialogueSO dialogue)
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        Debug.Log("DialogueUI: Panel shown.");
    }

    private void HandleNodeChanged(DialogueNode node)
    {
        if (node == null) return;

        // Update speaker name
        if (speakerNameText != null)
        {
            speakerNameText.text = node.speakerName ?? "";
        }

        // Update dialogue text (with optional typewriter effect)
        fullDialogueText = node.dialogueText ?? "";
        if (useTypewriter && fullDialogueText.Length > 0)
        {
            StartTypewriter(fullDialogueText);
        }
        else
        {
            if (dialogueText != null)
                dialogueText.text = fullDialogueText;
        }

        // Choices or continue?
        if (node.HasChoices)
        {
            ShowChoices(node.choices);
        }
        else
        {
            HideChoices();
            ShowContinuePrompt(true);
        }
    }

    private void HandleDialogueEnded()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        isTypewriting = false;
        isShowingChoices = false;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        Debug.Log("DialogueUI: Panel hidden.");
    }

    // ─── Choice Display ───────────────────────────────────────

    private void ShowChoices(DialogueChoice[] choices)
    {
        ShowContinuePrompt(false);

        // Clear old choice buttons
        ClearChoices();

        if (choices == null || choices.Length == 0) return;

        currentChoiceCount = Mathf.Min(choices.Length, choiceKeys.Length);
        isShowingChoices = true;

        for (int i = 0; i < currentChoiceCount; i++)
        {
            DialogueChoice choice = choices[i];
            string inputLabel = choiceKeys[i].ToString(); // "Z", "X", "C"

            // Check if this choice is available (karma gating)
            bool available = true;
            if (DialogueManager.Instance != null)
                available = DialogueManager.Instance.IsChoiceAvailable(choice);

            SpawnChoiceButton(choice, inputLabel, i, available);
        }

        // Show choice container
        if (choiceContainer != null)
            choiceContainer.gameObject.SetActive(true);
    }

    private void HideChoices()
    {
        ClearChoices();
        isShowingChoices = false;
        currentChoiceCount = 0;

        if (choiceContainer != null)
            choiceContainer.gameObject.SetActive(false);
    }

    private void ClearChoices()
    {
        if (choiceContainer == null) return;

        for (int i = choiceContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(choiceContainer.GetChild(i).gameObject);
        }
    }

    private void SpawnChoiceButton(DialogueChoice choice, string inputLabel, int index, bool available)
    {
        if (choiceButtonPrefab == null || choiceContainer == null) return;

        GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer);
        ChoiceButtonUI btnUI = btnObj.GetComponent<ChoiceButtonUI>();

        if (btnUI != null)
        {
            btnUI.Setup(choice, inputLabel, index, available);
            // Wire up click callback (for mouse users)
            btnUI.OnClicked += OnChoiceSelected;
        }
        else
        {
            Debug.LogWarning("DialogueUI: ChoiceButton prefab missing ChoiceButtonUI component!");
        }
    }

    // ─── Continue Prompt ──────────────────────────────────────

    private void ShowContinuePrompt(bool show)
    {
        if (continuePrompt != null)
            continuePrompt.SetActive(show);
    }

    // ─── Input Actions ────────────────────────────────────────

    private void OnChoiceSelected(int choiceIndex)
    {
        if (DialogueManager.Instance == null) return;

        PlayUISound(choiceSound);
        DialogueManager.Instance.SelectChoice(choiceIndex);

        isShowingChoices = false;
    }

    private void OnAdvance()
    {
        if (DialogueManager.Instance == null) return;

        PlayUISound(advanceSound);
        DialogueManager.Instance.AdvanceDialogue();
    }

    // ─── Typewriter Effect ────────────────────────────────────

    private void StartTypewriter(string text)
    {
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        typewriterCoroutine = StartCoroutine(TypewriterCoroutine(text));
    }

    private void SkipTypewriter()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        isTypewriting = false;

        if (dialogueText != null)
            dialogueText.text = fullDialogueText;
    }

    private IEnumerator TypewriterCoroutine(string text)
    {
        isTypewriting = true;

        if (dialogueText != null)
            dialogueText.text = "";

        float delay = 1f / typewriterSpeed;

        for (int i = 0; i <= text.Length; i++)
        {
            if (dialogueText != null)
                dialogueText.text = text.Substring(0, i);

            yield return new WaitForSeconds(delay);
        }

        isTypewriting = false;
        typewriterCoroutine = null;
    }

    // ─── Audio ────────────────────────────────────────────────

    private void PlayUISound(AudioClip clip)
    {
        if (clip != null && uiAudioSource != null)
            uiAudioSource.PlayOneShot(clip);
    }
}
