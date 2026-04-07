using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class ButtonScript : MonoBehaviour
{
    public GameObject darkness;

    public RectTransform NGarrow;
    public RectTransform Carrow;
    public RectTransform LGarrow;
    public RectTransform Sarrow;
    public RectTransform Qarrow;

    public RectTransform NGbutton;
    public RectTransform Cbutton;
    public RectTransform LGbutton;
    public RectTransform Sbutton;
    public RectTransform Qbutton;

    public Vector2 expandedSize = new Vector2(12f, 12f);
    public Vector2 collapsedSize = new Vector2(0f, 0f);
    public float speed = 10f;

    private Vector2 NGtarget;
    private Vector2 Ctarget;
    private Vector2 LGtarget;
    private Vector2 Starget;
    private Vector2 Qtarget;

    public float fadeSpeed = 1f;
    private CanvasGroup darknessCanvasGroup;
    public GameObject fadeObject;

    void Start()
    {
        NGtarget = collapsedSize;
        Ctarget = collapsedSize;
        LGtarget = collapsedSize;
        Starget = collapsedSize;
        Qtarget = collapsedSize;

        NGarrow.sizeDelta = collapsedSize;
        Carrow.sizeDelta = collapsedSize;
        LGarrow.sizeDelta = collapsedSize;
        Sarrow.sizeDelta = collapsedSize;
        Qarrow.sizeDelta = collapsedSize;

        if (darkness != null)
        {
            darkness.SetActive(true);
            darknessCanvasGroup = darkness.GetComponent<CanvasGroup>();
            if (darknessCanvasGroup == null)
            {
                darknessCanvasGroup = darkness.AddComponent<CanvasGroup>();
            }
            darknessCanvasGroup.alpha = 0f;
        }
    }

    void Update()
    {
        NGarrow.sizeDelta = Vector2.Lerp(NGarrow.sizeDelta, NGtarget, Time.deltaTime * speed);
        Carrow.sizeDelta = Vector2.Lerp(Carrow.sizeDelta, Ctarget, Time.deltaTime * speed);
        LGarrow.sizeDelta = Vector2.Lerp(LGarrow.sizeDelta, LGtarget, Time.deltaTime * speed);
        Sarrow.sizeDelta = Vector2.Lerp(Sarrow.sizeDelta, Starget, Time.deltaTime * speed);
        Qarrow.sizeDelta = Vector2.Lerp(Qarrow.sizeDelta, Qtarget, Time.deltaTime * speed);
    }

    public void onNewGameClick()
    {
        if (darknessCanvasGroup != null)
        {
            StartCoroutine(FadeAndLoadScene());
        }
        else
        {
            SceneManager.LoadScene("SelectRealm");
        }
    }

    private IEnumerator FadeAndLoadScene()
    {
        CanvasGroup fadeObjectCanvasGroup = null;

        if (fadeObject != null)
        {
            fadeObjectCanvasGroup = fadeObject.GetComponent<CanvasGroup>();
            if (fadeObjectCanvasGroup == null)
            {
                fadeObjectCanvasGroup = fadeObject.AddComponent<CanvasGroup>();
            }
            fadeObjectCanvasGroup.alpha = 1f;
        }

        while ((darknessCanvasGroup != null && darknessCanvasGroup.alpha < 1f) ||
               (fadeObjectCanvasGroup != null && fadeObjectCanvasGroup.alpha > 0f))
        {
            if (darknessCanvasGroup != null && darknessCanvasGroup.alpha < 1f)
            {
                darknessCanvasGroup.alpha += fadeSpeed * Time.deltaTime;
                if (darknessCanvasGroup.alpha > 1f)
                    darknessCanvasGroup.alpha = 1f;
            }

            if (fadeObjectCanvasGroup != null && fadeObjectCanvasGroup.alpha > 0f)
            {
                fadeObjectCanvasGroup.alpha -= fadeSpeed * Time.deltaTime;
                if (fadeObjectCanvasGroup.alpha < 0f)
                    fadeObjectCanvasGroup.alpha = 0f;
            }

            yield return null;
        }

        SceneManager.LoadScene("SelectRealm");
    }

    public void OnNGEnter()
    {
        NGtarget = expandedSize;
    }

    public void OnNGExit()
    {
        NGtarget = collapsedSize;
    }

    public void OnCEnter()
    {
        Ctarget = expandedSize;
    }

    public void OnCExit()
    {
        Ctarget = collapsedSize;
    }

    public void OnLGEnter()
    {
        LGtarget = expandedSize;
    }

    public void OnLGExit()
    {
        LGtarget = collapsedSize;
    }

    public void OnSEnter()
    {
        Starget = expandedSize;
    }

    public void OnSExit()
    {
        Starget = collapsedSize;
    }

    public void OnQEnter()
    {
        Qtarget = expandedSize;
    }

    public void OnQExit()
    {
        Qtarget = collapsedSize;
    }

    public void OnNGPress()
    {
        NGbutton.localScale = NGbutton.localScale * 0.9f; NGarrow.localScale = NGarrow.localScale * 0.9f;
    }

    public void OnNGRelease()
    {
        NGbutton.localScale = Vector3.one; NGarrow.localScale = Vector3.one;
    }

    public void OnCPress()
    {
        Cbutton.localScale = Cbutton.localScale * 0.9f; Carrow.localScale = Carrow.localScale * 0.9f;
    }

    public void OnCRelease()
    {
        Cbutton.localScale = Vector3.one; Carrow.localScale = Vector3.one;
    }

    public void OnLGPress()
    {
        LGbutton.localScale = LGbutton.localScale * 0.9f; LGarrow.localScale = LGarrow.localScale * 0.9f;
    }

    public void OnLGRelease()
    {
        LGbutton.localScale = Vector3.one; LGarrow.localScale = Vector3.one;
    }

    public void OnSPress()
    {
        Sbutton.localScale = Sbutton.localScale * 0.9f; Sarrow.localScale = Sarrow.localScale * 0.9f;
    }

    public void OnSRelease()
    {
        Sbutton.localScale = Vector3.one; Sarrow.localScale = Vector3.one;
    }

    public void OnQPress()
    {
        Qbutton.localScale = Qbutton.localScale * 0.9f; Qarrow.localScale = Qarrow.localScale * 0.9f;
    }

    public void OnQRelease()
    {
        Qbutton.localScale = Vector3.one; Qarrow.localScale = Vector3.one;
    }
}
