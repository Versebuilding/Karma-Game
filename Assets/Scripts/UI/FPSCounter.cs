using UnityEngine;
using TMPro;

/// <summary>
/// Color-coded FPS counter for development/debugging.
/// Updates every 0.5s to avoid jitter. Displays in top-right corner.
///
/// Color coding:
///   Green  = 50+ FPS (good)
///   Yellow = 30-50 FPS (acceptable)
///   Red    = below 30 FPS (needs optimization)
///
/// Uses Time.unscaledDeltaTime so it works even when Time.timeScale is 0.
/// </summary>
public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text fpsText;

    [Tooltip("How often to update the FPS display (seconds)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float updateInterval = 0.5f;

    private float elapsed;
    private int frameCount;

    void Update()
    {
        frameCount++;
        elapsed += Time.unscaledDeltaTime;

        if (elapsed >= updateInterval)
        {
            float fps = frameCount / elapsed;

            if (fpsText != null)
            {
                fpsText.text = $"FPS: {fps:F0}";

                // Color-code based on performance
                if (fps >= 50f)
                    fpsText.color = Color.green;
                else if (fps >= 30f)
                    fpsText.color = Color.yellow;
                else
                    fpsText.color = Color.red;
            }

            frameCount = 0;
            elapsed = 0f;
        }
    }
}
