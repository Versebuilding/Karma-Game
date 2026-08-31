using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SelectRealmAnimationStarter : MonoBehaviour
{
    public RectTransform targetImageUp;
    public RectTransform targetImageDown;
    public GameObject fadeObject;
    public Button fadeButton;
    public float moveY = 100f;
    public float speed = 100f;
    public float fadeSpeed = 1f;

    [Tooltip("Scene loaded when the start button is clicked (must be in Build Settings)")]
    public string chapterSceneName = "Chapter1- Serna";

    private Vector2 targetPosUp;
    private Vector2 targetPosDown;
    private CanvasGroup fadeCanvasGroup;
    private CanvasGroup buttonCanvasGroup;
    private bool fadeStarted = false;

    void Start()
    {
        if (targetImageUp != null)
        {
            targetImageUp.gameObject.SetActive(true);
            targetPosUp = targetImageUp.anchoredPosition + new Vector2(0, moveY);
        }

        if (targetImageDown != null)
        {
            targetImageDown.gameObject.SetActive(true);
            targetPosDown = targetImageDown.anchoredPosition + new Vector2(0, -moveY);
        }

        if (fadeObject != null)
        {
            fadeObject.SetActive(true);
            fadeCanvasGroup = fadeObject.GetComponent<CanvasGroup>();
            if (fadeCanvasGroup == null)
            {
                fadeCanvasGroup = fadeObject.AddComponent<CanvasGroup>();
            }
            fadeCanvasGroup.alpha = 0f;
        }

        if (fadeButton != null)
        {
            fadeButton.gameObject.SetActive(true);
            buttonCanvasGroup = fadeButton.GetComponent<CanvasGroup>();
            if (buttonCanvasGroup == null)
            {
                buttonCanvasGroup = fadeButton.gameObject.AddComponent<CanvasGroup>();
            }
            buttonCanvasGroup.alpha = 0f;

            // The prefab's onClick wiring is broken (null target) — wire the
            // chapter launch here so the start button actually enters the realm.
            fadeButton.onClick.AddListener(LaunchChapter);
        }
    }

    private void LaunchChapter()
    {
        if (!string.IsNullOrEmpty(chapterSceneName))
            SceneManager.LoadScene(chapterSceneName);
    }

    void Update()
    {
        bool imagesMoving = false;

        if (targetImageUp != null)
        {
            if ((Vector2)targetImageUp.anchoredPosition != targetPosUp)
            {
                targetImageUp.anchoredPosition = Vector2.MoveTowards(
                    targetImageUp.anchoredPosition,
                    targetPosUp,
                    speed * Time.deltaTime
                );
                imagesMoving = true;
            }
        }

        if (targetImageDown != null)
        {
            if ((Vector2)targetImageDown.anchoredPosition != targetPosDown)
            {
                targetImageDown.anchoredPosition = Vector2.MoveTowards(
                    targetImageDown.anchoredPosition,
                    targetPosDown,
                    speed * Time.deltaTime
                );
                imagesMoving = true;
            }
        }

        if (!imagesMoving && !fadeStarted)
        {
            fadeStarted = true;
        }

        if (fadeStarted)
        {
            if (fadeCanvasGroup != null && fadeCanvasGroup.alpha < 1f)
            {
                fadeCanvasGroup.alpha += fadeSpeed * Time.deltaTime;
                if (fadeCanvasGroup.alpha > 1f)
                {
                    fadeCanvasGroup.alpha = 1f;
                }
            }

            if (buttonCanvasGroup != null && buttonCanvasGroup.alpha < 1f)
            {
                buttonCanvasGroup.alpha += fadeSpeed * Time.deltaTime;
                if (buttonCanvasGroup.alpha > 1f)
                {
                    buttonCanvasGroup.alpha = 1f;
                }
            }
        }
    }
}