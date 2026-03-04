using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Floating karma change text that appears above the player or NPC when karma changes.
/// Shows "+50 Karma" in gold or "-20 Karma" in red, floats upward, and fades out.
///
/// Can be used as a world-space popup (above the NPC) or as a screen-space overlay.
///
/// Setup (World-Space):
///   1. Create a World Space Canvas as child of the player or a manager object
///   2. Add a TMP_Text child
///   3. Attach this script to the canvas or a manager
///   4. It will auto-create popup instances when karma changes
///
/// Setup (Screen-Space, simpler):
///   1. On the HUD Canvas, add a TMP_Text for karma popup
///   2. Attach this script and assign the text reference
///   3. It shows the popup at a fixed screen position
/// </summary>
public class KarmaPopupUI : MonoBehaviour
{
    [Header("Popup Text")]
    [Tooltip("TMP_Text used for the popup display")]
    [SerializeField] private TMP_Text popupText;

    [Header("Appearance")]
    [Tooltip("Duration the popup is visible")]
    [Range(0.5f, 3f)]
    [SerializeField] private float popupDuration = 1.5f;

    [Tooltip("How far the popup floats upward")]
    [Range(10f, 150f)]
    [SerializeField] private float floatDistance = 60f;

    [Tooltip("Font size for the popup")]
    [Range(16f, 72f)]
    [SerializeField] private float fontSize = 36f;

    [Tooltip("Color for positive karma changes")]
    [SerializeField] private Color positiveColor = new Color(1f, 0.85f, 0.25f, 1f); // gold

    [Tooltip("Color for negative karma changes")]
    [SerializeField] private Color negativeColor = new Color(0.9f, 0.25f, 0.25f, 1f); // red

    [Tooltip("Suffix text after the number")]
    [SerializeField] private string suffix = " Karma";

    // ─── Runtime ──────────────────────────────────────────────
    private Coroutine popupCoroutine;
    private RectTransform popupRect;
    private Vector2 startPosition;

    // ─── Unity Lifecycle ──────────────────────────────────────

    void Awake()
    {
        if (popupText != null)
        {
            popupRect = popupText.GetComponent<RectTransform>();
            if (popupRect != null)
                startPosition = popupRect.anchoredPosition;

            popupText.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (KarmaManager.Instance != null)
        {
            KarmaManager.Instance.OnKarmaChanged += HandleKarmaChanged;
        }
    }

    void OnDisable()
    {
        if (KarmaManager.Instance != null)
        {
            KarmaManager.Instance.OnKarmaChanged -= HandleKarmaChanged;
        }
    }

    // ─── Event Handler ────────────────────────────────────────

    private void HandleKarmaChanged(int newTotal, int delta)
    {
        if (delta == 0) return;
        ShowPopup(delta);
    }

    // ─── Popup Display ────────────────────────────────────────

    /// <summary>Show a karma change popup.</summary>
    public void ShowPopup(int delta)
    {
        if (popupText == null) return;

        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(PopupCoroutine(delta));
    }

    private IEnumerator PopupCoroutine(int delta)
    {
        // Setup text
        string prefix = delta > 0 ? "+" : "";
        popupText.text = $"{prefix}{delta}{suffix}";
        popupText.color = delta > 0 ? positiveColor : negativeColor;
        popupText.fontSize = fontSize;
        popupText.gameObject.SetActive(true);

        // Reset position
        if (popupRect != null)
            popupRect.anchoredPosition = startPosition;

        float elapsed = 0f;
        while (elapsed < popupDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / popupDuration;

            // Float upward
            if (popupRect != null)
            {
                popupRect.anchoredPosition = startPosition + Vector2.up * (floatDistance * t);
            }

            // Scale: start big, settle to normal
            float scaleCurve = 1f + 0.3f * Mathf.Max(0f, 1f - t * 3f);
            popupText.transform.localScale = Vector3.one * scaleCurve;

            // Fade out in the last third
            if (t > 0.65f)
            {
                float fadeT = (t - 0.65f) / 0.35f;
                Color c = popupText.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                popupText.color = c;
            }

            yield return null;
        }

        popupText.gameObject.SetActive(false);

        // Reset position and scale
        if (popupRect != null)
            popupRect.anchoredPosition = startPosition;

        popupText.transform.localScale = Vector3.one;
    }
}
