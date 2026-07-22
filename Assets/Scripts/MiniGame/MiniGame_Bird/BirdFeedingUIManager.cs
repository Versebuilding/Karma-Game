using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BirdFeedingUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FeedingMiniGameManager miniGameManager;
    [SerializeField] private TMP_Text birdNumberLabel;
    [SerializeField] private TMP_Text countdownLabel;
    [SerializeField] private TMP_Text bestScoreLabel;
    [SerializeField] private TMP_Text patternDebugLabel;
    [SerializeField] private Button nextPatternButton;

    [Header("Formatting")]
    [SerializeField] private string bestScorePrefix = "Best: ";

    private bool nextPatternButtonBound;

    private void Awake()
    {
        ResolveReferences();
        RefreshUi();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindButton();
        RefreshUi();
    }

    private void OnDisable()
    {
        if (nextPatternButton != null && nextPatternButtonBound)
        {
            nextPatternButton.onClick.RemoveListener(HandleNextPatternClicked);
            nextPatternButtonBound = false;
        }
    }

    private void Update()
    {
        RefreshUi();
    }

    private void RefreshUi()
    {
        if (miniGameManager == null)
        {
            return;
        }

        if (birdNumberLabel != null)
        {
            birdNumberLabel.text = miniGameManager.SuccessfulFeeds + "/" + miniGameManager.CurrentTargetFeeds;
        }

        if (countdownLabel != null)
        {
            countdownLabel.text = FormatTime(miniGameManager.RemainingTime);
        }

        if (bestScoreLabel != null)
        {
            bestScoreLabel.text = miniGameManager.HasBestCompletionTime
                ? bestScorePrefix + FormatTime(miniGameManager.BestCompletionTimeSeconds)
                : bestScorePrefix + "--";
        }

        if (patternDebugLabel != null)
        {
            patternDebugLabel.text = miniGameManager.CurrentPatternName;
        }
    }

    private void HandleNextPatternClicked()
    {
        if (miniGameManager != null)
        {
            miniGameManager.NextPattern();
        }
    }

    private void ResolveReferences()
    {
        if (miniGameManager == null)
        {
            miniGameManager = FindFirstObjectByType<FeedingMiniGameManager>();
        }

        if (birdNumberLabel == null)
        {
            birdNumberLabel = FindTextByName("BirdNumber");
        }

        if (countdownLabel == null)
        {
            countdownLabel = FindTextByName("CountDown");
        }

        if (bestScoreLabel == null)
        {
            bestScoreLabel = FindTextByName("BestScore");
        }

        if (patternDebugLabel == null)
        {
            patternDebugLabel = FindTextByName("PatternDebugLog");
        }

        if (nextPatternButton == null)
        {
            nextPatternButton = FindButtonByName("NextPatternButton");
        }
    }

    private void BindButton()
    {
        if (nextPatternButton == null || nextPatternButtonBound)
        {
            return;
        }

        if (nextPatternButton.onClick.GetPersistentEventCount() == 0)
        {
            nextPatternButton.onClick.AddListener(HandleNextPatternClicked);
            nextPatternButtonBound = true;
        }
    }

    private TMP_Text FindTextByName(string objectName)
    {
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == objectName)
            {
                return texts[i];
            }
        }

        return null;
    }

    private Button FindButtonByName(string objectName)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == objectName)
            {
                return buttons[i];
            }
        }

        return null;
    }

    private string FormatTime(float totalSeconds)
    {
        int clampedSeconds = Mathf.Max(0, Mathf.CeilToInt(totalSeconds));
        int minutes = clampedSeconds / 60;
        int seconds = clampedSeconds % 60;
        return minutes + ":" + seconds.ToString("00");
    }

    private void OnValidate()
    {
        ResolveReferences();
    }
}
