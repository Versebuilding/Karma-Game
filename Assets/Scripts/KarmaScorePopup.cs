using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using System;
using UnityEngine.EventSystems;

public class KarmaScorePopup : MonoBehaviour
{

    public GameObject karmascore;
    public Image karmabar2;

    public GameObject petal1;
    public GameObject petal2;
    public GameObject petal3;
    public GameObject petal4;
    public GameObject petal5;
    public GameObject petal6;
    public GameObject petal7;

    public SpriteRenderer karmabar;

    private float moveDistance = 0.01f;
    public Transform cameraTransform;
    private float distanceInFront = 5.0f;
    private int count = 1;

    private bool stop = false;
    private int currentPetal = 0;

    public TMP_Text scoretext;
    private int score=0;

    private float targetFill = 0f;

    // Wire to the real karma system: the bar/petals/popup respond to
    // KarmaManager instead of (only) the editor test keys.
    void OnEnable()
    {
        if (KarmaManager.Instance != null)
        {
            KarmaManager.Instance.OnKarmaChanged += HandleKarmaChanged;
        }
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;
        }
    }

    void OnDisable()
    {
        if (KarmaManager.Instance != null)
        {
            KarmaManager.Instance.OnKarmaChanged -= HandleKarmaChanged;
        }
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;
        }
    }

    private void HandleKarmaChanged(int total, int delta)
    {
        score = total;
        targetFill = KarmaManager.Instance.CurrentLevelProgress;
    }

    void Start()
    {
        karmascore.SetActive(false);
        karmabar2.fillAmount = 0f;

        if (KarmaManager.Instance != null)
        {
            // Late subscribe in case the manager awoke after OnEnable.
            KarmaManager.Instance.OnKarmaChanged -= HandleKarmaChanged;
            KarmaManager.Instance.OnKarmaChanged += HandleKarmaChanged;
            HandleKarmaChanged(KarmaManager.Instance.CurrentKarma, 0);
            karmabar2.fillAmount = targetFill;

            // Don't fire milestone popups for karma the player already had.
            float startSize = targetFill * 5.1f;
            if (startSize >= 4.900035f) currentPetal = 5;
            else if (startSize >= 3.600016f) currentPetal = 4;
            else if (startSize >= 2.41f) currentPetal = 3;
            else if (startSize >= 1.28f) currentPetal = 2;
            else if (startSize >= 0.17f) currentPetal = 1;
        }

        if (DialogueManager.Instance != null)
        {
            // Late subscribe in case the manager awoke after OnEnable.
            DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;
            DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;
        }

        score = KarmaManager.Instance != null ? KarmaManager.Instance.CurrentKarma : 0;

        //Automatically uses main camera if camera not assigned to camera transform

        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        AutoFitToScreen();
        HookKarmaFlowerClick();
    }

    [Tooltip("Fraction of the screen height the popup should occupy")]
    [Range(0.2f, 0.95f)] public float screenHeightFraction = 0.55f;

    /// <summary>
    /// The popup rig is world-space sprites held in front of the camera, so
    /// its on-screen size depends on camera FOV and authoring scale. Measure
    /// its renderer bounds and scale it so it fills screenHeightFraction of
    /// the view at the follow distance.
    /// </summary>
    private void AutoFitToScreen()
    {
        if (karmascore == null) return;
        bool wasActive = karmascore.activeSelf;
        karmascore.SetActive(true);

        var renderers = karmascore.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            var cam = Camera.main;
            float frustumHeight = cam != null
                ? 2f * distanceInFront * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)
                : 5f;
            float targetHeight = frustumHeight * screenHeightFraction;

            if (b.size.y > 0.0001f)
            {
                float factor = targetHeight / b.size.y;
                karmascore.transform.localScale *= factor;
            }
        }

        karmascore.SetActive(wasActive);
    }

    /// <summary>
    /// Tapping the karma flower HUD (top-left score cluster) toggles the
    /// popup. Adds an invisible stretched Button over the KarmaFlowerUI so
    /// no scene wiring is needed.
    /// </summary>
    private void HookKarmaFlowerClick()
    {
        var flowerUI = FindFirstObjectByType<KarmaFlowerUI>();
        if (flowerUI == null) return;

        var btnGo = new GameObject("KarmaPopupTapTarget", typeof(RectTransform));
        btnGo.transform.SetParent(flowerUI.transform, false);
        var rt = (RectTransform)btnGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f); // invisible, still clickable
        var btn = btnGo.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(ImageClicked);
    }


    void Update()
    {
        scoretext.text = score.ToString();

        //Progress bar from HUD increaes

        if (!stop)
        {
#if UNITY_EDITOR
            // Editor-only test keys (Christler's original debug controls).
            if (Input.GetKey(KeyCode.Z))
            {
                targetFill = Mathf.Min(targetFill + moveDistance / 5.1f, 1f);
                score = Mathf.RoundToInt(targetFill * 100f);
            }
            else if (Input.GetKey(KeyCode.C))
            {
                targetFill = Mathf.Max(targetFill - moveDistance / 5.1f, 0f);
                score = Mathf.RoundToInt(targetFill * 100f);
            }
#endif
            // Ease the bar toward the real karma level progress.
            karmabar2.fillAmount = Mathf.MoveTowards(karmabar2.fillAmount, targetFill, moveDistance);
        }

        UpdateKarmaBarColor();
        UpdatePetalColor();

    }

    private bool popupOpen = false;

    /// <summary>
    /// Open the popup: place it once in front of the camera (fixed — it no
    /// longer follows), then pause the whole game (dialogue, food rain,
    /// movement) and the music until it closes.
    /// </summary>
    private void ShowPopup()
    {
        if (popupOpen) return;
        popupOpen = true;

        // Fixed placement at open time
        transform.position = cameraTransform.position + cameraTransform.forward * distanceInFront;
        transform.rotation = cameraTransform.rotation;

        karmascore.SetActive(true);
        SetOverlayVisible(true);
    }

    private void HidePopup()
    {
        if (!popupOpen) return;
        popupOpen = false;
        karmascore.SetActive(false);
        SetOverlayVisible(false);
    }

    private SpriteRenderer darkOverlay;

    /// <summary>
    /// 80% black sheet between the popup and the world: a world-space quad
    /// half a unit behind the popup, scaled past the camera frustum, drawn
    /// under the popup sprites.
    /// </summary>
    private void SetOverlayVisible(bool visible)
    {
        if (darkOverlay == null)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            var go = new GameObject("PopupDarkOverlay");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.5f); // just behind the popup
            darkOverlay = go.AddComponent<SpriteRenderer>();
            darkOverlay.sprite = sprite;
            darkOverlay.color = new Color(0f, 0f, 0f, 0.8f);
            darkOverlay.sortingOrder = -10; // under the popup sprites

            var cam = Camera.main;
            float h = cam != null
                ? 2f * (distanceInFront + 0.5f) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)
                : 10f;
            // Oversize well past the frustum so edges never show.
            go.transform.localScale = new Vector3(h * cam.aspect * 1.5f / transform.localScale.x,
                                                  h * 1.5f / transform.localScale.y, 1f);
        }
        darkOverlay.gameObject.SetActive(visible);
    }

    //Timer on karma score popup (realtime — the game clock is paused)
    IEnumerator PopUpTimer()
    {
        stop = true;
        ShowPopup();
        yield return new WaitForSeconds(3f);
        HidePopup();
        stop = false;
    }

    //When progress bar image is clicked, the karma popup score will show up and disappear when clicked again
    public void ImageClicked()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (!popupOpen)
        {
            ShowPopup();
        }
        else if (!stop) // milestone auto-popups close on their own timer
        {
            HidePopup();
        }
    }

    //Petals fade an change into their colors based on position of progress bar

    public void UpdatePetalColor()
    {

        if (karmabar.size.x > 0f && karmabar.size.x <= 0.17f)
        {
            float fade = Mathf.InverseLerp(0f, 0.17f, karmabar.size.x);

            petal1.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);

            petal2.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);

            petal7.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);
        }
        else if (karmabar.size.x >= 0.17f)
        {
            petal1.GetComponent<SpriteRenderer>().color = Color.white;

            petal2.GetComponent<SpriteRenderer>().color = Color.white;

            petal7.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else
        {
            petal1.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            petal2.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            petal7.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        if (karmabar.size.x > 0.17f && karmabar.size.x <= 1.28f)
        {
            float fade = Mathf.InverseLerp(0.17f, 1.28f, karmabar.size.x);

            petal6.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);
        }
        else if (karmabar.size.x >= 1.28f)
        {
            petal6.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else
        {
            petal6.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        if (karmabar.size.x > 1.28f && karmabar.size.x <= 2.41f)
        {
            float fade = Mathf.InverseLerp(1.28f, 2.41f, karmabar.size.x);

            petal3.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);
        }
        else if (karmabar.size.x >= 2.41f)
        {
            petal3.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else
        {
            petal3.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        if (karmabar.size.x > 2.41f && karmabar.size.x <= 3.600016f)
        {
            float fade = Mathf.InverseLerp(2.41f, 3.600016f, karmabar.size.x);

            petal4.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);
        }
        else if (karmabar.size.x >= 3.600016f)
        {
            petal4.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else
        {
            petal4.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        if (karmabar.size.x > 3.600016f && karmabar.size.x <= 4.900035f)
        {
            float fade = Mathf.InverseLerp(3.600016f, 4.900035f, karmabar.size.x);

            petal5.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);
        }
        else if (karmabar.size.x >= 4.900035f)
        {
            petal5.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else
        {
            petal5.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

    }

    //Updating karmabar color
    
    public void UpdateKarmaBarColor()
    {
        karmabar.size = new Vector2(karmabar2.fillAmount * 5.1f, karmabar.size.y);

        if (karmabar.size.x >= 0.17f && currentPetal < 1 && !stop)
        {
            currentPetal = 1;
            QueueMilestonePopup();
        }
        else if (karmabar.size.x >= 1.28f && currentPetal < 2 && !stop)
        {
            currentPetal = 2;
            QueueMilestonePopup();
        }
        else if (karmabar.size.x >= 2.41f && currentPetal < 3 && !stop)
        {
            currentPetal = 3;
            QueueMilestonePopup();
        }
        else if (karmabar.size.x >= 3.600016f && currentPetal < 4 && !stop)
        {
            currentPetal = 4;
            QueueMilestonePopup();
        }
        else if (karmabar.size.x >= 4.900035f && currentPetal < 5 && !stop)
        {
            currentPetal = 5;
            QueueMilestonePopup();
        }
    }

    private bool milestonePending = false;

    /// <summary>
    /// Milestone popups never interrupt a conversation: if dialogue is
    /// running, remember the milestone and show the popup once the player
    /// leaves interaction mode (dialogue ends).
    /// </summary>
    private void QueueMilestonePopup()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            milestonePending = true;
            return;
        }
        StartCoroutine(PopUpTimer());
    }

    private void HandleDialogueEnded()
    {
        if (milestonePending)
        {
            milestonePending = false;
            StartCoroutine(ShowPendingAfterDelay());
        }
    }

    private IEnumerator ShowPendingAfterDelay()
    {
        yield return new WaitForSeconds(0.6f); // let the dialogue UI fade out
        if (!popupOpen) StartCoroutine(PopUpTimer());
    }

    //KarmaScore follows camera

    // Position is set once in ShowPopup() — the popup stays fixed on
    // screen while open instead of following the camera.
    void LateUpdate() { }
}