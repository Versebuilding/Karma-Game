using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD element displaying the player's coin count with animated delta popups.
/// Shows "+100" in green or "-50" in red when coins change, then fades out.
///
/// Subscribes to WalletManager.OnCoinsChanged event.
///
/// Setup:
///   1. On the HUD Canvas, add a coin display area (top-right area)
///   2. Add coin icon (Image), coin count text (TMP_Text)
///   3. Add delta popup text (TMP_Text) — starts hidden, floats up on change
///   4. Attach this script and drag references
/// </summary>
public class CoinCounterUI : MonoBehaviour
{
    // ─── References ───────────────────────────────────────────
    [Header("Display")]
    [Tooltip("Coin icon image")]
    [SerializeField] private Image coinIcon;

    [Tooltip("Text showing current coin count")]
    [SerializeField] private TMP_Text coinText;

    [Header("Delta Popup")]
    [Tooltip("Text for showing +/- coin changes (will float and fade)")]
    [SerializeField] private TMP_Text deltaPopupText;

    [Tooltip("How long the delta popup is visible")]
    [Range(0.5f, 3f)]
    [SerializeField] private float popupDuration = 1.5f;

    [Tooltip("How far the popup floats upward (pixels)")]
    [Range(10f, 100f)]
    [SerializeField] private float popupFloatDistance = 40f;

    [Tooltip("Color for positive coin changes")]
    [SerializeField] private Color gainColor = new Color(0.2f, 0.9f, 0.3f, 1f); // green

    [Tooltip("Color for negative coin changes")]
    [SerializeField] private Color lossColor = new Color(0.95f, 0.3f, 0.3f, 1f); // red

    [Header("Animation")]
    [Tooltip("Punch scale effect on coin change")]
    [SerializeField] private bool punchScale = true;

    [Tooltip("Punch scale amount")]
    [Range(1.05f, 1.5f)]
    [SerializeField] private float punchScaleAmount = 1.2f;

    [Tooltip("Punch scale duration")]
    [Range(0.1f, 0.5f)]
    [SerializeField] private float punchScaleDuration = 0.2f;

    [Header("Audio")]
    [Tooltip("AudioSource for coin sounds")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Sound for gaining coins")]
    [SerializeField] private AudioClip coinGainSound;

    [Tooltip("Sound for spending coins")]
    [SerializeField] private AudioClip coinSpendSound;

    // ─── Runtime ──────────────────────────────────────────────
    private Coroutine popupCoroutine;
    private Coroutine punchCoroutine;
    private Vector3 originalScale = Vector3.one;
    private RectTransform deltaPopupRect;

    // ─── Unity Lifecycle ──────────────────────────────────────

    void Awake()
    {
        originalScale = transform.localScale;

        if (deltaPopupText != null)
        {
            deltaPopupRect = deltaPopupText.GetComponent<RectTransform>();
            deltaPopupText.gameObject.SetActive(false);
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    void OnEnable()
    {
        if (WalletManager.Instance != null)
        {
            WalletManager.Instance.OnCoinsChanged += HandleCoinsChanged;

            // Initialize display
            UpdateCoinDisplay(WalletManager.Instance.Coins);
        }
    }

    void OnDisable()
    {
        if (WalletManager.Instance != null)
        {
            WalletManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
        }
    }

    // ─── Event Handler ────────────────────────────────────────

    private void HandleCoinsChanged(int newTotal, int delta)
    {
        UpdateCoinDisplay(newTotal);
        ShowDeltaPopup(delta);

        // Punch scale effect
        if (punchScale)
        {
            if (punchCoroutine != null) StopCoroutine(punchCoroutine);
            punchCoroutine = StartCoroutine(PunchScaleCoroutine());
        }

        // Play sound
        if (audioSource != null)
        {
            AudioClip clip = delta > 0 ? coinGainSound : coinSpendSound;
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }
    }

    // ─── Display ──────────────────────────────────────────────

    private void UpdateCoinDisplay(int coins)
    {
        if (coinText != null)
            coinText.text = FormatNumber(coins);
    }

    private string FormatNumber(int number)
    {
        // Format with commas: 1500 → "1,500"
        return number.ToString("N0");
    }

    // ─── Delta Popup ──────────────────────────────────────────

    private void ShowDeltaPopup(int delta)
    {
        if (deltaPopupText == null) return;

        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(DeltaPopupCoroutine(delta));
    }

    private IEnumerator DeltaPopupCoroutine(int delta)
    {
        // Setup text
        string prefix = delta > 0 ? "+" : "";
        deltaPopupText.text = $"{prefix}{delta}";
        deltaPopupText.color = delta > 0 ? gainColor : lossColor;
        deltaPopupText.gameObject.SetActive(true);

        // Store original position
        Vector2 startPos = deltaPopupRect != null
            ? deltaPopupRect.anchoredPosition
            : Vector2.zero;

        float elapsed = 0f;
        while (elapsed < popupDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popupDuration;

            // Float upward
            if (deltaPopupRect != null)
            {
                deltaPopupRect.anchoredPosition = startPos + Vector2.up * (popupFloatDistance * t);
            }

            // Fade out in the last half
            if (t > 0.5f)
            {
                float fadeT = (t - 0.5f) / 0.5f;
                Color c = deltaPopupText.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                deltaPopupText.color = c;
            }

            yield return null;
        }

        deltaPopupText.gameObject.SetActive(false);

        // Reset position
        if (deltaPopupRect != null)
            deltaPopupRect.anchoredPosition = startPos;
    }

    // ─── Punch Scale ──────────────────────────────────────────

    private IEnumerator PunchScaleCoroutine()
    {
        float elapsed = 0f;
        float halfDuration = punchScaleDuration * 0.5f;

        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float scale = Mathf.Lerp(1f, punchScaleAmount, t);
            transform.localScale = originalScale * scale;
            yield return null;
        }

        // Scale back down
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            float scale = Mathf.Lerp(punchScaleAmount, 1f, t);
            transform.localScale = originalScale * scale;
            yield return null;
        }

        transform.localScale = originalScale;
    }
}
