using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor tool that builds the full UI Canvas hierarchy from code.
/// Menu: Karma > Build UI Canvases
///
/// Creates two canvases matching the Figma mockups:
///   HUDCanvas (Sort Order 5): Karma flower, coin counter, karma popup, interaction prompt
///   DialogueCanvas (Sort Order 10): Dialogue panel with orange border, speaker badge,
///                                    choice container, continue prompt
///
/// Also creates a ChoiceButton prefab at Assets/Prefab/UI/ChoiceButton.prefab
/// All [SerializeField] references and design values are auto-wired.
/// </summary>
public class UISetupTool
{
    // ─── Color Constants (from Figma mockups) ────────────────────
    private static readonly Color OrangeBorder = new Color(0.93f, 0.58f, 0.16f, 1f);     // #ED941F warm orange
    private static readonly Color CreamBg = new Color(0.98f, 0.96f, 0.91f, 1f);           // cream/off-white panel
    private static readonly Color DarkText = new Color(0.15f, 0.12f, 0.1f, 1f);           // near-black text
    private static readonly Color BrownBadge = new Color(0.4f, 0.28f, 0.16f, 1f);         // brown speaker badge
    private static readonly Color WhiteText = Color.white;
    private static readonly Color NeutralBtnBg = new Color(0.95f, 0.93f, 0.88f, 1f);      // neutral choice bg
    private static readonly Color EmpatheticBtnBg = new Color(0.93f, 0.58f, 0.16f, 1f);   // orange empathetic
    private static readonly Color SelfishBtnBg = new Color(0.35f, 0.35f, 0.4f, 1f);       // dark selfish
    private static readonly Color KarmaGold = new Color(1f, 0.85f, 0.25f, 1f);            // gold karma text
    private static readonly Color PromptBg = new Color(0f, 0f, 0f, 0.6f);                  // semi-transparent prompt bg
    private static readonly Color GainGreen = new Color(0.2f, 0.9f, 0.3f, 1f);            // green for +coins
    private static readonly Color LossRed = new Color(0.95f, 0.3f, 0.3f, 1f);             // red for -coins/-karma
    private static readonly Color UnlitGray = new Color(0.4f, 0.4f, 0.4f, 0.5f);          // unlit petal gray
    private static readonly Color LockedGray = new Color(0.5f, 0.5f, 0.5f, 0.6f);         // locked choice gray
    private static readonly Color FlashGold = new Color(1f, 1f, 0.5f, 0.8f);              // level-up flash

    // ─── Main Entry Points ──────────────────────────────────────

    [MenuItem("Karma/Build UI Canvases")]
    public static void BuildAllUI()
    {
        Debug.Log("=== Karma UI Setup Tool ===");

        // Build HUD Canvas
        var hudCanvas = BuildHUDCanvas();

        // Build Dialogue Canvas
        var dialogueCanvas = BuildDialogueCanvas();

        // Build ChoiceButton prefab
        BuildChoiceButtonPrefab();

        // Wire up HUDManager
        WireHUDManager(hudCanvas, dialogueCanvas);

        Debug.Log("");
        Debug.Log("=== UI Build Complete! ===");
        Debug.Log("  Auto-wired fields:");
        Debug.Log("    DialogueUI: dialoguePanel, speakerNameText, dialogueText, choiceContainer, continuePrompt, choiceButtonPrefab, uiAudioSource");
        Debug.Log("    KarmaFlowerUI: flowerImage, progressBarFill, progressBarBg, levelText, karmaPointsText, levelUpFlash, all colors");
        Debug.Log("    CoinCounterUI: coinIcon, coinText, deltaPopupText, all colors");
        Debug.Log("    KarmaPopupUI: popupText, flyTarget, timing, colors");
        Debug.Log("    CoinFlyUI: flyTarget, coinColor, timing");
        Debug.Log("    HUDManager: all 8 fields");
        Debug.Log("    ChoiceButtonUI: all references + all colors");
        Debug.Log("");
        Debug.Log("  Still need manual assignment (no assets exist yet):");
        Debug.Log("    - Flower sprite → KarmaFlowerUI.flowerImage.sprite");
        Debug.Log("    - Petal sprites → KarmaFlowerUI.petalImages[] (add petal Image children)");
        Debug.Log("    - Coin sprite → CoinCounterUI.coinIcon.sprite");
        Debug.Log("    - Audio clips → DialogueUI (advanceSound, choiceSound)");
        Debug.Log("    - Audio clips → KarmaFlowerUI (karmaGainSound, karmaLossSound, levelUpSound)");
        Debug.Log("    - Audio clips → CoinCounterUI (coinGainSound, coinSpendSound)");

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    [MenuItem("Karma/Build HUD Canvas Only")]
    public static void BuildHUDCanvasOnly()
    {
        var hudCanvas = BuildHUDCanvas();
        WireHUDManagerToHUD(hudCanvas);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    [MenuItem("Karma/Build Dialogue Canvas Only")]
    public static void BuildDialogueCanvasOnly()
    {
        BuildDialogueCanvas();
        BuildChoiceButtonPrefab();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    // ─── HUD Canvas Builder ─────────────────────────────────────

    private static GameObject BuildHUDCanvas()
    {
        // Delete existing if present (check both exact name and numbered variants
        // from prefab instances like "HUDCanvas (1)")
        var existing = GameObject.Find("HUDCanvas");
        if (existing == null) existing = GameObject.Find("HUDCanvas (1)");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Replace HUD Canvas?",
                "An existing HUDCanvas was found. Replace it?",
                "Replace", "Cancel"))
                return existing;
            Undo.DestroyObjectImmediate(existing);
        }

        // Create Canvas
        var canvasObj = CreateCanvas("HUDCanvas", 5);
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create HUD Canvas");

        // ─── Interaction Prompt (bottom center) ──────────────────
        var promptPanel = CreateInteractionPrompt(canvasObj.transform);

        // ─── Karma Flower (top-left) — must be created before KarmaPopup (needs flyTarget ref)
        var karmaFlower = CreateKarmaFlower(canvasObj.transform);

        // ─── Coin Counter (top-right) — must be created before CoinFlyUI (needs flyTarget ref)
        var coinCounter = CreateCoinCounter(canvasObj.transform);

        // ─── Karma Popup (center → flies to KarmaFlower) ─────────
        var karmaPopup = CreateKarmaPopup(canvasObj.transform, karmaFlower.GetComponent<RectTransform>());

        // ─── Coin Fly UI (center → coins fly to CoinCounter) ────
        var coinFlyUI = CreateCoinFlyUI(canvasObj.transform, coinCounter.GetComponent<RectTransform>());

        // ─── FPS Counter (top-right) ──────────────────────────────
        var fpsCounter = CreateFPSCounter(canvasObj.transform);

        // ─── Reset Button (bottom-right corner) ────────────────
        var resetButton = CreateResetButton(canvasObj.transform);

        Debug.Log("  HUDCanvas created with: InteractionPrompt, KarmaFlower, CoinCounter, KarmaPopup, CoinFlyUI, FPSCounter, ResetButton");
        return canvasObj;
    }

    // ─── Dialogue Canvas Builder ────────────────────────────────

    private static GameObject BuildDialogueCanvas()
    {
        var existing = GameObject.Find("DialogueCanvas");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Replace Dialogue Canvas?",
                "An existing DialogueCanvas was found. Replace it?",
                "Replace", "Cancel"))
                return existing;
            Undo.DestroyObjectImmediate(existing);
        }

        var canvasObj = CreateCanvas("DialogueCanvas", 10);
        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Dialogue Canvas");

        // ─── Dialogue Panel (root, starts inactive) ─────────────
        var dialoguePanel = CreateDialoguePanel(canvasObj.transform);

        // ─── Choice Container (right side of screen, direct child of Canvas) ──
        var choiceContainerObj = CreateChoiceContainer(canvasObj.transform);

        // ─── Add DialogueUI component ───────────────────────────
        var dialogueUI = canvasObj.AddComponent<DialogueUI>();
        var audioSrc = canvasObj.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;

        // Wire ALL references using SerializedObject
        var so = new SerializedObject(dialogueUI);

        // dialoguePanel
        SetProp(so, "dialoguePanel", dialoguePanel);

        // speakerNameText — SpeakerBadge is direct child of DialoguePanel
        var speakerBadge = dialoguePanel.transform.Find("SpeakerBadge");
        if (speakerBadge != null)
        {
            var speakerText = speakerBadge.Find("SpeakerNameText");
            if (speakerText != null)
                SetProp(so, "speakerNameText", speakerText.GetComponent<TMP_Text>());
        }

        // dialogueText — NESTED under InnerPanel, must use path
        var textArea = dialoguePanel.transform.Find("InnerPanel/DialogueTextArea");
        if (textArea != null)
        {
            var dTextObj = textArea.Find("DialogueText");
            if (dTextObj != null)
                SetProp(so, "dialogueText", dTextObj.GetComponent<TMP_Text>());
        }

        // choiceContainer — direct child of Canvas (right side of screen)
        if (choiceContainerObj != null)
            SetProp(so, "choiceContainer", choiceContainerObj.transform);

        // continuePrompt — NESTED under InnerPanel, must use path
        var continuePrompt = dialoguePanel.transform.Find("InnerPanel/ContinuePrompt");
        if (continuePrompt != null)
            SetProp(so, "continuePrompt", continuePrompt.gameObject);

        // Audio + typewriter settings
        SetProp(so, "uiAudioSource", audioSrc);
        SetBool(so, "useTypewriter", true);
        SetFloat(so, "typewriterSpeed", 40f);
        SetFloat(so, "selectionDelay", 0.4f);

        so.ApplyModifiedProperties();

        Debug.Log("  DialogueCanvas created. DialogueUI auto-wired (all fields).");
        return canvasObj;
    }

    // ─── Dialogue Panel Construction ────────────────────────────

    private static GameObject CreateDialoguePanel(Transform parent)
    {
        // Root panel (bottom of screen, stretches horizontally)
        var panelObj = new GameObject("DialoguePanel");
        panelObj.transform.SetParent(parent, false);

        var panelRect = panelObj.AddComponent<RectTransform>();
        // Fixed-width centered at bottom (not percentage-stretch)
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0, 30);
        panelRect.sizeDelta = new Vector2(1000, 160); // auto-sized height at runtime by DialogueUI

        // ─── Orange border (outer container) ─────────────────────
        var borderImage = panelObj.AddComponent<Image>();
        borderImage.color = OrangeBorder;

        // ─── Inner cream panel ───────────────────────────────────
        var innerObj = new GameObject("InnerPanel");
        innerObj.transform.SetParent(panelObj.transform, false);

        var innerRect = innerObj.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(3, 3);
        innerRect.offsetMax = new Vector2(-3, -3);

        var innerImage = innerObj.AddComponent<Image>();
        innerImage.color = CreamBg;

        // ─── Speaker Name Badge (top-left, overlapping border) ──
        var badgeObj = new GameObject("SpeakerBadge");
        badgeObj.transform.SetParent(panelObj.transform, false);

        var badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(0f, 1f);
        badgeRect.pivot = new Vector2(0f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(20, 0);
        badgeRect.sizeDelta = new Vector2(120, 32);

        var badgeImage = badgeObj.AddComponent<Image>();
        badgeImage.color = BrownBadge;

        var badgeLayout = badgeObj.AddComponent<ContentSizeFitter>();
        badgeLayout.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var badgeHLayout = badgeObj.AddComponent<HorizontalLayoutGroup>();
        badgeHLayout.padding = new RectOffset(16, 16, 4, 4);
        badgeHLayout.childAlignment = TextAnchor.MiddleCenter;

        // Speaker Name Text
        var speakerTextObj = new GameObject("SpeakerNameText");
        speakerTextObj.transform.SetParent(badgeObj.transform, false);

        var speakerText = speakerTextObj.AddComponent<TextMeshProUGUI>();
        speakerText.text = "Serna";
        speakerText.fontSize = 16;
        speakerText.fontStyle = FontStyles.Bold;
        speakerText.color = WhiteText;
        speakerText.alignment = TextAlignmentOptions.Center;
        speakerText.enableAutoSizing = false;

        // ─── Dialogue Text Area (under InnerPanel) ──────────────
        var textAreaObj = new GameObject("DialogueTextArea");
        textAreaObj.transform.SetParent(innerObj.transform, false);

        var textAreaRect = textAreaObj.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(20, 15);
        textAreaRect.offsetMax = new Vector2(-20, -20);

        var dialogueTextObj = new GameObject("DialogueText");
        dialogueTextObj.transform.SetParent(textAreaObj.transform, false);

        var dialogueRect = dialogueTextObj.AddComponent<RectTransform>();
        dialogueRect.anchorMin = Vector2.zero;
        dialogueRect.anchorMax = Vector2.one;
        dialogueRect.offsetMin = Vector2.zero;
        dialogueRect.offsetMax = Vector2.zero;

        var dialogueText = dialogueTextObj.AddComponent<TextMeshProUGUI>();
        dialogueText.text = "Oh... you can see me? Most people walk right past without noticing.";
        dialogueText.fontSize = 20;
        dialogueText.color = DarkText;
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.overflowMode = TextOverflowModes.Ellipsis;

        // NOTE: ChoiceContainer is now created as a direct child of DialogueCanvas
        // (not DialoguePanel) so it can stay visible when the panel is hidden.
        // See CreateChoiceContainer() and BuildDialogueCanvas().

        // ─── Continue Prompt (under InnerPanel) ─────────────────
        var continueObj = new GameObject("ContinuePrompt");
        continueObj.transform.SetParent(innerObj.transform, false);

        var continueRect = continueObj.AddComponent<RectTransform>();
        continueRect.anchorMin = new Vector2(1f, 0f);
        continueRect.anchorMax = new Vector2(1f, 0f);
        continueRect.pivot = new Vector2(1f, 0f);
        continueRect.anchoredPosition = new Vector2(-15, 10);
        continueRect.sizeDelta = new Vector2(200, 24);

        var continueText = continueObj.AddComponent<TextMeshProUGUI>();
        continueText.text = "Press Enter to continue >>";
        continueText.fontSize = 13;
        continueText.color = new Color(0.5f, 0.45f, 0.4f, 0.8f);
        continueText.alignment = TextAlignmentOptions.BottomRight;
        continueText.fontStyle = FontStyles.Italic;

        continueObj.SetActive(false);

        panelObj.SetActive(false);

        return panelObj;
    }

    // ─── Interaction Prompt (bottom center) ──────────────────────

    private static GameObject CreateInteractionPrompt(Transform parent)
    {
        var promptObj = new GameObject("InteractionPrompt");
        promptObj.transform.SetParent(parent, false);

        var promptRect = promptObj.AddComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0f);
        promptRect.anchorMax = new Vector2(0.5f, 0f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.anchoredPosition = new Vector2(0, 60);
        promptRect.sizeDelta = new Vector2(280, 44);

        var bgImage = promptObj.AddComponent<Image>();
        bgImage.color = PromptBg;

        var hlg = promptObj.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 20, 8, 8);
        hlg.childAlignment = TextAnchor.MiddleCenter;

        var textObj = new GameObject("PromptText");
        textObj.transform.SetParent(promptObj.transform, false);

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Press E to pick up";
        text.fontSize = 16;
        text.color = WhiteText;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = false;

        var fitter = promptObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        promptObj.SetActive(false);

        return promptObj;
    }

    // ─── Karma Popup (center screen → flies to KarmaFlower) ─────

    private static GameObject CreateKarmaPopup(Transform parent, RectTransform karmaFlowerRect)
    {
        var popupObj = new GameObject("KarmaPopup");
        popupObj.transform.SetParent(parent, false);

        var popupRect = popupObj.AddComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 0.55f);
        popupRect.anchorMax = new Vector2(0.5f, 0.55f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.anchoredPosition = Vector2.zero;
        popupRect.sizeDelta = new Vector2(300, 50);

        var textObj = new GameObject("KarmaPopupText");
        textObj.transform.SetParent(popupObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "+50";
        text.fontSize = 36;
        text.color = GainGreen;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = false;

        textObj.SetActive(false);

        // Add KarmaPopupUI and wire ALL fields
        var popupUI = popupObj.AddComponent<KarmaPopupUI>();
        var popupSO = new SerializedObject(popupUI);

        SetProp(popupSO, "popupText", text);
        SetProp(popupSO, "flyTarget", karmaFlowerRect);
        SetFloat(popupSO, "fontSize", 36f);
        SetColor(popupSO, "positiveColor", GainGreen);
        SetColor(popupSO, "negativeColor", LossRed);
        SetFloat(popupSO, "popInDuration", 0.2f);
        SetFloat(popupSO, "pauseDuration", 0.5f);
        SetFloat(popupSO, "flyDuration", 0.6f);

        popupSO.ApplyModifiedProperties();

        return popupObj;
    }

    // ─── Karma Flower (top-left) ────────────────────────────────
    // Layout matches HUDCanvas.prefab exactly

    private static GameObject CreateKarmaFlower(Transform parent)
    {
        var flowerObj = new GameObject("KarmaFlower");
        flowerObj.transform.SetParent(parent, false);

        var flowerRect = flowerObj.AddComponent<RectTransform>();
        flowerRect.anchorMin = new Vector2(0f, 1f);
        flowerRect.anchorMax = new Vector2(0f, 1f);
        flowerRect.pivot = new Vector2(0f, 1f);
        flowerRect.anchoredPosition = new Vector2(15, -15);
        flowerRect.sizeDelta = new Vector2(273.9f, 80);

        // Flower icon — anchored center-left of the KarmaFlower container
        var iconObj = new GameObject("FlowerIcon");
        iconObj.transform.SetParent(flowerObj.transform, false);

        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(36.1f, -14.1f);
        iconRect.sizeDelta = new Vector2(70, 50);

        var iconImage = iconObj.AddComponent<Image>();
        iconImage.color = new Color(1f, 0.85f, 0.3f, 0.5f); // gold placeholder
        // Try to load the actual flower sprite
        var flowerSprite = LoadSpriteByGUID("7b247038d7fb54083933f2b2e96bf8a9");
        if (flowerSprite != null) iconImage.sprite = flowerSprite;

        // Progress bar background — bottom-anchored, taller bar matching prefab
        var barBgObj = new GameObject("ProgressBarBg");
        barBgObj.transform.SetParent(flowerObj.transform, false);

        var barBgRect = barBgObj.AddComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0f, 0f);
        barBgRect.anchorMax = new Vector2(1f, 0f);
        barBgRect.pivot = new Vector2(0.5f, 1f);
        barBgRect.anchoredPosition = Vector2.zero;
        barBgRect.sizeDelta = new Vector2(0, 27.1f);

        var barBgImage = barBgObj.AddComponent<Image>();
        barBgImage.color = Color.white;
        // Try to load the actual bar bg sprite
        var barBgSprite = LoadSpriteByGUID("dc77fe1781b17498daca4dac69c3cc59");
        if (barBgSprite != null) barBgImage.sprite = barBgSprite;
        barBgImage.type = Image.Type.Filled;
        barBgImage.fillAmount = 1f;

        // Progress bar fill — inset by 6px on each side to match prefab
        var barFillObj = new GameObject("ProgressBarFill");
        barFillObj.transform.SetParent(barBgObj.transform, false);

        var barFillRect = barFillObj.AddComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = Vector2.one;
        barFillRect.offsetMin = new Vector2(6, 6);   // -12 total → sizeDelta (-12, -12)
        barFillRect.offsetMax = new Vector2(-6, -6);

        var barFillImage = barFillObj.AddComponent<Image>();
        barFillImage.color = Color.white;
        // Try to load the actual fill sprite
        var fillSprite = LoadSpriteByGUID("c3a99fa5b6576423fabe0b923855695f");
        if (fillSprite != null) barFillImage.sprite = fillSprite;
        barFillImage.type = Image.Type.Filled;
        barFillImage.fillMethod = Image.FillMethod.Horizontal;
        barFillImage.fillAmount = 0.3f;

        // Level text — positioned left of center to match prefab
        var levelObj = new GameObject("LevelText");
        levelObj.transform.SetParent(flowerObj.transform, false);

        var levelRect = levelObj.AddComponent<RectTransform>();
        levelRect.anchorMin = new Vector2(0.5f, 0.5f);
        levelRect.anchorMax = new Vector2(0.5f, 0.5f);
        levelRect.pivot = new Vector2(0.5f, 0.5f);
        levelRect.anchoredPosition = new Vector2(-99.6f, -32f);
        levelRect.sizeDelta = new Vector2(60, 30);

        var levelText = levelObj.AddComponent<TextMeshProUGUI>();
        levelText.text = "Lv.0";
        levelText.fontSize = 14;
        levelText.color = WhiteText;
        levelText.alignment = TextAlignmentOptions.Center;
        levelText.fontStyle = FontStyles.Bold;

        // Karma score text — below center to match prefab
        var karmaScoreObj = new GameObject("KarmaScoreText");
        karmaScoreObj.transform.SetParent(flowerObj.transform, false);

        var kpRect = karmaScoreObj.AddComponent<RectTransform>();
        kpRect.anchorMin = new Vector2(0.5f, 0.5f);
        kpRect.anchorMax = new Vector2(0.5f, 0.5f);
        kpRect.pivot = new Vector2(0.5f, 0.5f);
        kpRect.anchoredPosition = new Vector2(0, -52.6f);
        kpRect.sizeDelta = new Vector2(60, 30);

        var karmaScoreText = karmaScoreObj.AddComponent<TextMeshProUGUI>();
        karmaScoreText.text = "167";
        karmaScoreText.fontSize = 14;
        karmaScoreText.color = WhiteText;
        karmaScoreText.alignment = TextAlignmentOptions.Center;
        karmaScoreText.fontStyle = FontStyles.Bold;

        // Level-up flash overlay (fullscreen flash on level-up, starts hidden)
        var flashObj = new GameObject("LevelUpFlash");
        flashObj.transform.SetParent(flowerObj.transform, false);

        var flashRect = flashObj.AddComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;

        var flashImage = flashObj.AddComponent<Image>();
        flashImage.color = new Color(1f, 1f, 0.5f, 0f); // transparent gold
        flashImage.raycastTarget = false;
        flashObj.SetActive(false);

        // Add KarmaFlowerUI and wire ALL fields
        var karmaFlowerUI = flowerObj.AddComponent<KarmaFlowerUI>();
        var kfSO = new SerializedObject(karmaFlowerUI);

        // Object references
        SetProp(kfSO, "flowerImage", iconImage);
        SetProp(kfSO, "progressBarFill", barFillImage);
        SetProp(kfSO, "progressBarBg", barBgImage);
        SetProp(kfSO, "levelText", levelText);
        SetProp(kfSO, "karmaPointsText", karmaScoreText);
        SetProp(kfSO, "levelUpFlash", flashImage);

        // Color values
        SetColor(kfSO, "litPetalColor", KarmaGold);
        SetColor(kfSO, "unlitPetalColor", UnlitGray);
        SetColor(kfSO, "barFillColor", KarmaGold);
        SetColor(kfSO, "barGainColor", GainGreen);
        SetColor(kfSO, "flashColor", FlashGold);
        SetFloat(kfSO, "flashDuration", 0.5f);

        // Try to wire audio clips
        var gainClip = LoadAssetByGUID<AudioClip>("dd27a083f822347559297747f1d762d6");
        var lossClip = LoadAssetByGUID<AudioClip>("3722611c441bb4126aafd9e0107776c4");
        var lvlUpClip = LoadAssetByGUID<AudioClip>("946a4317505304143af4d39ee6b184eb");
        if (gainClip != null) SetProp(kfSO, "karmaGainSound", gainClip);
        if (lossClip != null) SetProp(kfSO, "karmaLossSound", lossClip);
        if (lvlUpClip != null) SetProp(kfSO, "levelUpSound", lvlUpClip);

        kfSO.ApplyModifiedProperties();

        return flowerObj;
    }

    // ─── Coin Counter (top-left, below KarmaFlower) ──────────────
    // Layout matches HUDCanvas.prefab exactly

    private static GameObject CreateCoinCounter(Transform parent)
    {
        var coinObj = new GameObject("CoinCounter");
        coinObj.transform.SetParent(parent, false);

        // Positioned top-left, below the KarmaFlower (matching prefab)
        var coinRect = coinObj.AddComponent<RectTransform>();
        coinRect.anchorMin = new Vector2(0f, 1f);
        coinRect.anchorMax = new Vector2(0f, 1f);
        coinRect.pivot = new Vector2(1f, 1f);
        coinRect.anchoredPosition = new Vector2(155, -141.8f);
        coinRect.sizeDelta = new Vector2(140, 40);

        var hlg = coinObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.padding = new RectOffset(10, 10, 5, 5);
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // Coin icon
        var iconObj = new GameObject("CoinIcon");
        iconObj.transform.SetParent(coinObj.transform, false);

        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(28, 28);

        var iconImage = iconObj.AddComponent<Image>();
        iconImage.color = new Color(1f, 0.85f, 0.2f, 1f); // gold
        // Try to load the actual coin sprite
        var coinSprite = LoadSpriteByGUID("2dbaa885d66d445a58ddf8c9f8436cec");
        if (coinSprite != null) iconImage.sprite = coinSprite;

        // Coin text
        var textObj = new GameObject("CoinText");
        textObj.transform.SetParent(coinObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(80, 30);

        var coinText = textObj.AddComponent<TextMeshProUGUI>();
        coinText.text = "0";
        coinText.fontSize = 20;
        coinText.color = WhiteText;
        coinText.alignment = TextAlignmentOptions.Left;
        coinText.fontStyle = FontStyles.Bold;

        // Delta popup text (hidden by default)
        var deltaObj = new GameObject("DeltaPopup");
        deltaObj.transform.SetParent(coinObj.transform, false);

        var deltaRect = deltaObj.AddComponent<RectTransform>();
        deltaRect.anchorMin = new Vector2(0.5f, 1f);
        deltaRect.anchorMax = new Vector2(0.5f, 1f);
        deltaRect.pivot = new Vector2(0.5f, 0f);
        deltaRect.anchoredPosition = new Vector2(0, 5);
        deltaRect.sizeDelta = new Vector2(80, 24);

        var deltaText = deltaObj.AddComponent<TextMeshProUGUI>();
        deltaText.text = "+100";
        deltaText.fontSize = 16;
        deltaText.color = GainGreen;
        deltaText.alignment = TextAlignmentOptions.Center;
        deltaText.fontStyle = FontStyles.Bold;
        deltaObj.SetActive(false);

        // Audio source for coin sounds
        var coinAudio = coinObj.AddComponent<AudioSource>();
        coinAudio.playOnAwake = false;

        // Add CoinCounterUI and wire ALL fields
        var coinCounterUI = coinObj.AddComponent<CoinCounterUI>();
        var ccSO = new SerializedObject(coinCounterUI);

        // Object references
        SetProp(ccSO, "coinIcon", iconImage);
        SetProp(ccSO, "coinText", coinText);
        SetProp(ccSO, "deltaPopupText", deltaText);
        SetProp(ccSO, "audioSource", coinAudio);

        // Colors
        SetColor(ccSO, "gainColor", GainGreen);
        SetColor(ccSO, "lossColor", LossRed);

        // Animation settings
        SetBool(ccSO, "punchScale", true);
        SetFloat(ccSO, "punchScaleAmount", 1.2f);
        SetFloat(ccSO, "punchScaleDuration", 0.2f);

        // Popup settings
        SetFloat(ccSO, "popupDuration", 1.5f);
        SetFloat(ccSO, "popupFloatDistance", 40f);

        ccSO.ApplyModifiedProperties();

        return coinObj;
    }

    // ─── Coin Fly UI (center → flies to CoinCounter) ───────────

    private static GameObject CreateCoinFlyUI(Transform parent, RectTransform coinCounterRect)
    {
        var coinFlyObj = new GameObject("CoinFlyUI");
        coinFlyObj.transform.SetParent(parent, false);

        // Container only — no visible UI, just holds the CoinFlyUI component
        var coinFlyRect = coinFlyObj.AddComponent<RectTransform>();
        coinFlyRect.anchorMin = Vector2.zero;
        coinFlyRect.anchorMax = Vector2.zero;
        coinFlyRect.sizeDelta = Vector2.zero;

        var coinFlyUI = coinFlyObj.AddComponent<CoinFlyUI>();
        var so = new SerializedObject(coinFlyUI);

        SetProp(so, "flyTarget", coinCounterRect);
        SetFloat(so, "coinSize", 24f);
        SetColor(so, "coinColor", new Color(1f, 0.85f, 0.2f, 1f)); // gold
        SetFloat(so, "popInDuration", 0.15f);
        SetFloat(so, "pauseDuration", 0.4f);
        SetFloat(so, "flyDuration", 0.5f);
        SetFloat(so, "staggerDelay", 0.12f);

        so.ApplyModifiedProperties();

        return coinFlyObj;
    }

    // ─── Reset Button (bottom-right) ─────────────────────────────

    private static GameObject CreateResetButton(Transform parent)
    {
        var btnObj = new GameObject("ResetButton");
        btnObj.transform.SetParent(parent, false);

        var btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0f);
        btnRect.anchorMax = new Vector2(1f, 0f);
        btnRect.pivot = new Vector2(1f, 0f);
        btnRect.anchoredPosition = new Vector2(-15, 15); // bottom-right with padding
        btnRect.sizeDelta = new Vector2(120, 32);

        var btnImage = btnObj.AddComponent<UnityEngine.UI.Image>();
        btnImage.color = new Color(0.3f, 0.3f, 0.3f, 0.7f); // dark semi-transparent bg

        var button = btnObj.AddComponent<UnityEngine.UI.Button>();
        var colors = button.colors;
        colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);
        colors.highlightedColor = new Color(0.5f, 0.3f, 0.3f, 0.9f);
        colors.pressedColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        button.colors = colors;

        // Label text
        var textObj = new GameObject("ResetLabel");
        textObj.transform.SetParent(btnObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Reset Game";
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;

        // Add ResetGameButton component and wire onClick
        var resetComp = btnObj.AddComponent<ResetGameButton>();
        button.onClick.AddListener(resetComp.ResetGame);

        return btnObj;
    }

    // ─── ChoiceButton Prefab Builder ────────────────────────────

    private static void BuildChoiceButtonPrefab()
    {
        // Ensure folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
            AssetDatabase.CreateFolder("Assets", "Prefab");
        if (!AssetDatabase.IsValidFolder("Assets/Prefab/UI"))
            AssetDatabase.CreateFolder("Assets/Prefab", "UI");

        string prefabPath = "Assets/Prefab/UI/ChoiceButton.prefab";

        var btnRoot = new GameObject("ChoiceButton");

        var rootRect = btnRoot.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(0, 64);

        // Background Image
        var bgImage = btnRoot.AddComponent<Image>();
        bgImage.color = NeutralBtnBg;

        // Button component
        var button = btnRoot.AddComponent<Button>();
        button.targetGraphic = bgImage;

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.85f, 1f);
        colors.pressedColor = new Color(0.9f, 0.85f, 0.75f, 1f);
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        button.colors = colors;

        // Horizontal Layout
        var hlg = btnRoot.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.padding = new RectOffset(16, 20, 10, 10);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Input Badge (circle with Z/X/C)
        var badgeObj = new GameObject("InputBadge");
        badgeObj.transform.SetParent(btnRoot.transform, false);

        var badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.sizeDelta = new Vector2(42, 42);

        var badgeImage = badgeObj.AddComponent<Image>();
        badgeImage.color = OrangeBorder;

        var badgeTextObj = new GameObject("BadgeText");
        badgeTextObj.transform.SetParent(badgeObj.transform, false);

        var badgeTextRect = badgeTextObj.AddComponent<RectTransform>();
        badgeTextRect.anchorMin = Vector2.zero;
        badgeTextRect.anchorMax = Vector2.one;
        badgeTextRect.offsetMin = Vector2.zero;
        badgeTextRect.offsetMax = Vector2.zero;

        var badgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
        badgeText.text = "Z";
        badgeText.fontSize = 20;
        badgeText.color = WhiteText;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.fontStyle = FontStyles.Bold;

        // Choice Text
        var choiceTextObj = new GameObject("ChoiceText");
        choiceTextObj.transform.SetParent(btnRoot.transform, false);

        var choiceTextRect = choiceTextObj.AddComponent<RectTransform>();
        choiceTextRect.sizeDelta = new Vector2(400, 36);

        var choiceText = choiceTextObj.AddComponent<TextMeshProUGUI>();
        choiceText.text = "Choice text here";
        choiceText.fontSize = 18;
        choiceText.color = DarkText;
        choiceText.alignment = TextAlignmentOptions.MidlineLeft;
        choiceText.textWrappingMode = TextWrappingModes.Normal;

        var choiceLE = choiceTextObj.AddComponent<LayoutElement>();
        choiceLE.flexibleWidth = 1;
        choiceLE.preferredHeight = 42;

        // Add ChoiceButtonUI and wire ALL fields
        var choiceUI = btnRoot.AddComponent<ChoiceButtonUI>();
        var cuiSO = new SerializedObject(choiceUI);

        // Object references
        SetProp(cuiSO, "backgroundImage", bgImage);
        SetProp(cuiSO, "inputLabelText", badgeText);
        SetProp(cuiSO, "choiceText", choiceText);
        SetProp(cuiSO, "inputLabelBadge", badgeImage);
        SetProp(cuiSO, "button", button);

        // Background colors
        SetColor(cuiSO, "empatheticColor", EmpatheticBtnBg);
        SetColor(cuiSO, "selfishColor", SelfishBtnBg);
        SetColor(cuiSO, "neutralColor", NeutralBtnBg);

        // Text colors
        SetColor(cuiSO, "empatheticTextColor", WhiteText);
        SetColor(cuiSO, "selfishTextColor", new Color(0.9f, 0.85f, 0.85f, 1f));
        SetColor(cuiSO, "neutralTextColor", DarkText);

        // Locked state
        SetColor(cuiSO, "lockedColor", LockedGray);
        SetFloat(cuiSO, "lockedAlpha", 0.4f);

        cuiSO.ApplyModifiedProperties();

        // Save as prefab
        var prefab = PrefabUtility.SaveAsPrefabAsset(btnRoot, prefabPath);
        Object.DestroyImmediate(btnRoot);

        Debug.Log($"  ChoiceButton prefab saved to: {prefabPath}");

        // Auto-assign to DialogueUI if present
        AutoAssignChoicePrefab(prefab);
    }

    private static void AutoAssignChoicePrefab(GameObject prefab)
    {
        var dialogueUI = Object.FindFirstObjectByType<DialogueUI>();
        if (dialogueUI != null && prefab != null)
        {
            var so = new SerializedObject(dialogueUI);
            var prefabProp = so.FindProperty("choiceButtonPrefab");
            if (prefabProp != null)
            {
                prefabProp.objectReferenceValue = prefab;
                so.ApplyModifiedProperties();
                Debug.Log("  Auto-assigned ChoiceButton prefab to DialogueUI");
            }
        }
    }

    // ─── Wire HUDManager ────────────────────────────────────────

    private static void WireHUDManager(GameObject hudCanvas, GameObject dialogueCanvas)
    {
        var managers = GameObject.Find("GameManagers");
        if (managers == null)
        {
            Debug.LogWarning("  GameManagers not found. Run 'Karma > Setup Game Systems' first.");
            return;
        }

        var hudManager = managers.GetComponent<HUDManager>();
        if (hudManager == null)
        {
            hudManager = Undo.AddComponent<HUDManager>(managers);
        }

        var hmSO = new SerializedObject(hudManager);

        SetProp(hmSO, "hudCanvas", hudCanvas);
        SetProp(hmSO, "dialogueCanvas", dialogueCanvas);

        var karmaFlowerUI = hudCanvas.GetComponentInChildren<KarmaFlowerUI>();
        if (karmaFlowerUI != null) SetProp(hmSO, "karmaFlowerUI", karmaFlowerUI);

        var coinUI = hudCanvas.GetComponentInChildren<CoinCounterUI>();
        if (coinUI != null) SetProp(hmSO, "coinCounterUI", coinUI);

        var karmaPopup = hudCanvas.GetComponentInChildren<KarmaPopupUI>();
        if (karmaPopup != null) SetProp(hmSO, "karmaPopupUI", karmaPopup);

        var dialogueUI = dialogueCanvas.GetComponentInChildren<DialogueUI>();
        if (dialogueUI != null) SetProp(hmSO, "dialogueUI", dialogueUI);

        var promptPanel = hudCanvas.transform.Find("InteractionPrompt");
        if (promptPanel != null)
        {
            SetProp(hmSO, "interactionPromptPanel", promptPanel.gameObject);

            var promptText = promptPanel.GetComponentInChildren<TMP_Text>();
            if (promptText != null)
                SetProp(hmSO, "interactionPromptText", promptText);
        }

        hmSO.ApplyModifiedProperties();
        Debug.Log("  HUDManager wired to all UI components.");
    }

    // ─── Wire HUDManager to HUD Canvas Only ──────────────────────
    // (Used when rebuilding just the HUD canvas without the dialogue canvas)

    private static void WireHUDManagerToHUD(GameObject hudCanvas)
    {
        var managers = GameObject.Find("GameManagers");
        if (managers == null)
        {
            Debug.LogWarning("  GameManagers not found. HUDManager not re-wired.");
            return;
        }

        var hudManager = managers.GetComponent<HUDManager>();
        if (hudManager == null)
        {
            Debug.LogWarning("  HUDManager not found on GameManagers. Run 'Karma > Build UI Canvases' for full setup.");
            return;
        }

        var hmSO = new SerializedObject(hudManager);

        // Wire HUD canvas reference
        SetProp(hmSO, "hudCanvas", hudCanvas);

        // Wire component references from HUD canvas
        var karmaFlowerUI = hudCanvas.GetComponentInChildren<KarmaFlowerUI>();
        if (karmaFlowerUI != null) SetProp(hmSO, "karmaFlowerUI", karmaFlowerUI);

        var coinUI = hudCanvas.GetComponentInChildren<CoinCounterUI>();
        if (coinUI != null) SetProp(hmSO, "coinCounterUI", coinUI);

        var karmaPopup = hudCanvas.GetComponentInChildren<KarmaPopupUI>();
        if (karmaPopup != null) SetProp(hmSO, "karmaPopupUI", karmaPopup);

        // Wire interaction prompt panel + text
        var promptPanel = hudCanvas.transform.Find("InteractionPrompt");
        if (promptPanel != null)
        {
            SetProp(hmSO, "interactionPromptPanel", promptPanel.gameObject);

            var promptText = promptPanel.GetComponentInChildren<TMP_Text>();
            if (promptText != null)
                SetProp(hmSO, "interactionPromptText", promptText);
        }

        hmSO.ApplyModifiedProperties();
        Debug.Log("  HUDManager re-wired to rebuilt HUD canvas (hudCanvas, karmaFlowerUI, coinCounterUI, karmaPopupUI, interactionPrompt).");
    }

    // ─── Choice Container (right side of screen) ─────────────────

    private static GameObject CreateChoiceContainer(Transform parent)
    {
        var choiceContainerObj = new GameObject("ChoiceContainer");
        choiceContainerObj.transform.SetParent(parent, false);

        var ccRect = choiceContainerObj.AddComponent<RectTransform>();
        // Right side of screen, vertically centered (slightly above middle)
        ccRect.anchorMin = new Vector2(1f, 0.25f);
        ccRect.anchorMax = new Vector2(1f, 0.75f);
        ccRect.pivot = new Vector2(1f, 0.5f);
        ccRect.anchoredPosition = new Vector2(-40, 30); // 40px from right edge, slightly above center
        ccRect.sizeDelta = new Vector2(420, 0);          // Fixed width, height from content

        var vlg = choiceContainerObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12;
        vlg.childAlignment = TextAnchor.MiddleRight;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        var ccFitter = choiceContainerObj.AddComponent<ContentSizeFitter>();
        ccFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        choiceContainerObj.SetActive(false);
        return choiceContainerObj;
    }

    // ─── FPS Counter (top-right) ────────────────────────────────

    private static GameObject CreateFPSCounter(Transform parent)
    {
        var fpsObj = new GameObject("FPSCounter");
        fpsObj.transform.SetParent(parent, false);

        var fpsRect = fpsObj.AddComponent<RectTransform>();
        fpsRect.anchorMin = new Vector2(1f, 1f);
        fpsRect.anchorMax = new Vector2(1f, 1f);
        fpsRect.pivot = new Vector2(1f, 1f);
        fpsRect.anchoredPosition = new Vector2(-15, -15); // Top-right corner
        fpsRect.sizeDelta = new Vector2(120, 30);

        var fpsText = fpsObj.AddComponent<TextMeshProUGUI>();
        fpsText.text = "FPS: --";
        fpsText.fontSize = 14;
        fpsText.color = Color.green;
        fpsText.alignment = TextAlignmentOptions.TopRight;
        fpsText.fontStyle = FontStyles.Bold;

        var fpsCounter = fpsObj.AddComponent<FPSCounter>();
        var so = new SerializedObject(fpsCounter);
        SetProp(so, "fpsText", fpsText);
        so.ApplyModifiedProperties();

        return fpsObj;
    }

    // ─── Helper: Create a Canvas ────────────────────────────────

    private static GameObject CreateCanvas(string name, int sortOrder)
    {
        var canvasObj = new GameObject(name);

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        return canvasObj;
    }

    // ─── Asset Loading Helpers (GUID-based) ────────────────────

    private static Sprite LoadSpriteByGUID(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static T LoadAssetByGUID<T>(string guid) where T : Object
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    // ─── SerializedProperty Helpers ─────────────────────────────

    private static void SetProp(SerializedObject so, string name, Object value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.objectReferenceValue = value;
    }

    private static void SetColor(SerializedObject so, string name, Color value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.colorValue = value;
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.floatValue = value;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.boolValue = value;
    }

    private static void SetString(SerializedObject so, string name, string value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.stringValue = value;
    }
}
