using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD element displaying the karma flower and progress bar.
/// The karma flower has petals that light up as the player levels up.
/// The progress bar shows how far the player is toward the next level.
///
/// Subscribes to KarmaManager events:
///   - OnKarmaChanged → update progress bar fill
///   - OnKarmaLevelUp → light up a new petal, play flash effect
///
/// Setup:
///   1. Create a HUD Canvas (Screen Space - Overlay)
///   2. Top-left area: Add flower icon (Image) + progress bar (Image, Filled)
///   3. Add petal Images as children (one per max level)
///   4. Optionally add level text (TMP_Text) and flash overlay (Image)
///   5. Attach this script and drag references
/// </summary>
public class KarmaFlowerUI : MonoBehaviour
{
    // ─── References ───────────────────────────────────────────
    [Header("Flower Display")]
    [Tooltip("The main karma flower icon")]
    [SerializeField] private Image flowerImage;

    [Tooltip("Individual petal images (one per karma level). Order: petal 0 = level 1, etc.")]
    [SerializeField] private Image[] petalImages;

    [Tooltip("Color for lit (earned) petals")]
    [SerializeField] private Color litPetalColor = new Color(1f, 0.85f, 0.25f, 1f); // warm gold

    [Tooltip("Color for unlit (unearned) petals")]
    [SerializeField] private Color unlitPetalColor = new Color(0.4f, 0.4f, 0.4f, 0.5f); // gray

    [Header("Progress Bar")]
    [Tooltip("Progress bar fill image (Image.type = Filled)")]
    [SerializeField] private Image progressBarFill;

    [Tooltip("Progress bar background image")]
    [SerializeField] private Image progressBarBg;

    [Tooltip("Bar fill color")]
    [SerializeField] private Color barFillColor = new Color(1f, 0.75f, 0.2f, 1f); // orange-gold

    [Header("Level Text")]
    [Tooltip("Optional text showing current karma level")]
    [SerializeField] private TMP_Text levelText;

    [Tooltip("Optional text showing karma points")]
    [SerializeField] private TMP_Text karmaPointsText;

    [Header("Level Up Effect")]
    [Tooltip("Flash overlay image for level-up effect")]
    [SerializeField] private Image levelUpFlash;

    [Tooltip("Duration of the level-up flash")]
    [Range(0.2f, 2f)]
    [SerializeField] private float flashDuration = 0.5f;

    [Tooltip("Flash color")]
    [SerializeField] private Color flashColor = new Color(1f, 1f, 0.5f, 0.8f);

    [Header("Audio")]
    [Tooltip("AudioSource for karma UI sounds")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Sound for karma gain")]
    [SerializeField] private AudioClip karmaGainSound;

    [Tooltip("Sound for karma loss")]
    [SerializeField] private AudioClip karmaLossSound;

    [Tooltip("Sound for level up")]
    [SerializeField] private AudioClip levelUpSound;

    // ─── Runtime ──────────────────────────────────────────────
    private Coroutine flashCoroutine;
    private int displayedLevel = -1;

    // ─── Unity Lifecycle ──────────────────────────────────────

    void OnEnable()
    {
        if (KarmaManager.Instance != null)
        {
            KarmaManager.Instance.OnKarmaChanged += HandleKarmaChanged;
            KarmaManager.Instance.OnKarmaLevelUp += HandleLevelUp;

            // Initialize display
            UpdateDisplay(
                KarmaManager.Instance.CurrentKarma,
                KarmaManager.Instance.CurrentLevel,
                KarmaManager.Instance.CurrentLevelProgress
            );
        }

        // Hide flash
        if (levelUpFlash != null)
            levelUpFlash.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (KarmaManager.Instance != null)
        {
            KarmaManager.Instance.OnKarmaChanged -= HandleKarmaChanged;
            KarmaManager.Instance.OnKarmaLevelUp -= HandleLevelUp;
        }
    }

    // ─── Event Handlers ───────────────────────────────────────

    private void HandleKarmaChanged(int newTotal, int delta)
    {
        if (KarmaManager.Instance == null) return;

        UpdateDisplay(
            newTotal,
            KarmaManager.Instance.CurrentLevel,
            KarmaManager.Instance.CurrentLevelProgress
        );

        // Play gain/loss sound
        if (audioSource != null)
        {
            AudioClip clip = delta > 0 ? karmaGainSound : karmaLossSound;
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }
    }

    private void HandleLevelUp(int newLevel)
    {
        // Light up the new petal
        UpdatePetals(newLevel);

        // Flash effect
        if (levelUpFlash != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(LevelUpFlashCoroutine());
        }

        // Play level up sound
        if (audioSource != null && levelUpSound != null)
            audioSource.PlayOneShot(levelUpSound);

        Debug.Log($"KarmaFlowerUI: Level up! New level: {newLevel}");
    }

    // ─── Display Update ───────────────────────────────────────

    private void UpdateDisplay(int karma, int level, float progress)
    {
        // Update progress bar
        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = progress;
            progressBarFill.color = barFillColor;
        }

        // Update level text
        if (levelText != null)
            levelText.text = $"Lv.{level}";

        // Update karma points text
        if (karmaPointsText != null)
            karmaPointsText.text = karma.ToString();

        // Update petals if level changed
        if (level != displayedLevel)
        {
            UpdatePetals(level);
            displayedLevel = level;
        }
    }

    private void UpdatePetals(int currentLevel)
    {
        if (petalImages == null) return;

        for (int i = 0; i < petalImages.Length; i++)
        {
            if (petalImages[i] == null) continue;

            bool isLit = i < currentLevel;
            petalImages[i].color = isLit ? litPetalColor : unlitPetalColor;
        }
    }

    // ─── Level Up Flash ───────────────────────────────────────

    private IEnumerator LevelUpFlashCoroutine()
    {
        levelUpFlash.gameObject.SetActive(true);
        levelUpFlash.color = flashColor;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flashDuration;
            float alpha = Mathf.Lerp(flashColor.a, 0f, t);
            levelUpFlash.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
            yield return null;
        }

        levelUpFlash.gameObject.SetActive(false);
    }

    // ─── Public API ───────────────────────────────────────────

    /// <summary>Manually set flower sprite (e.g., from KarmaConfig).</summary>
    public void SetFlowerSprite(Sprite sprite)
    {
        if (flowerImage != null && sprite != null)
            flowerImage.sprite = sprite;
    }

    /// <summary>Manually set petal sprites from config.</summary>
    public void SetPetalSprites(Sprite[] sprites)
    {
        if (petalImages == null || sprites == null) return;

        for (int i = 0; i < petalImages.Length && i < sprites.Length; i++)
        {
            if (petalImages[i] != null && sprites[i] != null)
                petalImages[i].sprite = sprites[i];
        }
    }
}
