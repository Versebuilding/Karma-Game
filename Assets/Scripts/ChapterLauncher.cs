using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Loads a game scene when a chapter button is clicked.
/// Attach to a button in the SelectRealm/chapter selection UI.
/// Supports fade-to-black transition before loading.
///
/// Setup:
///   1. Add this to a "Play" button in the chapter selection UI
///   2. Set sceneName to the target scene (e.g., "Chapter1- Serna")
///   3. Optionally assign a darkness CanvasGroup for fade transition
///   4. Wire the button's OnClick to LaunchChapter()
/// </summary>
public class ChapterLauncher : MonoBehaviour
{
    [Tooltip("Scene name to load (must be in Build Settings)")]
    public string sceneName = "Chapter1- Serna";

    [Tooltip("Optional: CanvasGroup to fade in before loading (darkness overlay)")]
    public CanvasGroup fadeOverlay;

    [Tooltip("Fade speed (higher = faster)")]
    public float fadeSpeed = 1.5f;

    /// <summary>Call this from a UI button OnClick event.</summary>
    public void LaunchChapter()
    {
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            StartCoroutine(FadeAndLoad());
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private IEnumerator FadeAndLoad()
    {
        fadeOverlay.alpha = 0f;

        while (fadeOverlay.alpha < 1f)
        {
            fadeOverlay.alpha += fadeSpeed * Time.deltaTime;
            yield return null;
        }

        fadeOverlay.alpha = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
