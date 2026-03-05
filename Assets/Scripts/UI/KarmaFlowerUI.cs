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

    [Tooltip("Color used during karma gain animation (green pulse)")]
    [SerializeField] private Color barGainColor = new Color(0.2f, 0.9f, 0.3f, 1f);

    [Tooltip("Duration of the fill animation in seconds")]
    [Range(0.2f, 1.5f)]
    [SerializeField] private float fillAnimDuration = 0.5f;

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
    private Coroutine fillCoroutine;
    private int displayedLevel = -1;
    private float currentDisplayedFill;
    private bool isSubscribed;

    // ─── Unity Lifecycle ──────────────────────────────────────

    void OnEnable()
    {
        // Runtime safety: force the progress bar Image to Filled mode.
        // If the scene was serialized with Image.Type = Simple, fillAmount
        // changes have NO visual effect. This guarantees correct rendering
        // regardless of serialized Inspector values.
        if (progressBarFill != null)
        {
            progressBarFill.type = Image.Type.Filled;
            progressBarFill.fillMethod = Image.FillMethod.Horizontal;
            progressBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        TrySubscribe();

        // Hide flash
        if (levelUpFlash != null)
            levelUpFlash.gameObject.SetActive(false);
    }

    void Start()
    {
        // Fallback: if KarmaManager.Instance was null during OnEnable
        // (common when HUDCanvas initializes before GameManagers),
        // subscribe now — all Awakes have run by Start time.
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (isSubscribed) return;
        if (KarmaManager.Instance == null) return;

        KarmaManager.Instance.OnKarmaChanged += HandleKarmaChanged;
        KarmaManager.Instance.OnKarmaLevelUp += HandleLevelUp;
        isSubscribed = true;

        // Initialize display (no animation on startup)
        float progress = KarmaManager.Instance.CurrentLevelProgress;
        currentDisplayedFill = progress;
        UpdateDisplay(
            KarmaManager.Instance.CurrentKarma,
            KarmaManager.Instance.CurrentLevel,
            progress
        );

        Debug.Log($"KarmaFlowerUI: Subscribed. Karma={KarmaManager.Instance.CurrentKarma}, " +
            $"Level={KarmaManager.Instance.CurrentLevel}, Progress={progress:F3}, " +
            $"BarRef={progressBarFill != null}, Active={gameObject.activeInHierarchy}");
    }

    void OnDisable()
    {
        if (KarmaManager.Instance != null)
        {
            KarmaManager.Instance.OnKarmaChanged -= HandleKarmaChanged;
            KarmaManager.Instance.OnKarmaLevelUp -= HandleLevelUp;
        }
        isSubscribed = false;
    }

    // ─── Event Handlers ───────────────────────────────────────

    private void HandleKarmaChanged(int newTotal, int delta)
    {
        if (KarmaManager.Instance == null) return;

        float progress = KarmaManager.Instance.CurrentLevelProgress;
        Debug.Log($"KarmaFlowerUI: Karma changed! Total={newTotal}, Delta={delta}, " +
            $"Level={KarmaManager.Instance.CurrentLevel}, Progress={progress:F3}, " +
            $"CurrentFill={currentDisplayedFill:F3}, BarRef={progressBarFill != null}");

        UpdateDisplay(
            newTotal,
            KarmaManager.Instance.CurrentLevel,
            progress,
            delta
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

        // After level-up, the bar resets — set displayed fill to 0
        // so the next UpdateDisplay animates from zero
        currentDisplayedFill = 0f;

        Debug.Log($"KarmaFlowerUI: Level up! New level: {newLevel}");
    }

    // ─── Display Update ───────────────────────────────────────

    private void UpdateDisplay(int karma, int level, float progress, int delta = 0)
    {
        // Update progress bar (animated if delta != 0)
        if (progressBarFill != null)
        {
            if (delta != 0)
            {
                if (gameObject.activeInHierarchy)
                {
                    // Animate from current displayed fill to target
                    if (fillCoroutine != null) StopCoroutine(fillCoroutine);
                    fillCoroutine = StartCoroutine(AnimateFillCoroutine(
                        currentDisplayedFill, progress, delta > 0));
                }
                else
                {
                    // Gameobject not active — can't run coroutine, set instantly
                    Debug.LogWarning("KarmaFlowerUI: GameObject not active, setting fill instantly.");
                    progressBarFill.fillAmount = progress;
                    progressBarFill.color = barFillColor;
                    currentDisplayedFill = progress;
                }
            }
            else
            {
                // Instant set (initialization, no delta)
                progressBarFill.fillAmount = progress;
                progressBarFill.color = barFillColor;
                currentDisplayedFill = progress;
            }
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

    // ─── Fill Animation ──────────────────────────────────────

    private IEnumerator AnimateFillCoroutine(float from, float to, bool isGain)
    {
        Debug.Log($"KarmaFlowerUI: Animating fill {from:F3} → {to:F3} (gain={isGain})");

        float elapsed = 0f;
        Color animColor = isGain ? barGainColor : barFillColor;

        // Green pulse during gain animation
        progressBarFill.color = animColor;

        while (elapsed < fillAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fillAnimDuration;
            // Ease-out curve: fast start, slow finish
            float eased = 1f - (1f - t) * (1f - t);
            float fill = Mathf.Lerp(from, to, eased);
            progressBarFill.fillAmount = fill;
            yield return null;
        }

        progressBarFill.fillAmount = to;
        currentDisplayedFill = to;

        // Return to normal bar color
        progressBarFill.color = barFillColor;
        fillCoroutine = null;

        Debug.Log($"KarmaFlowerUI: Fill animation complete. fillAmount={to:F3}");
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
