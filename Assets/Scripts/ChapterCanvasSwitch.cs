using UnityEngine;
using System.Collections;

public class ChapterCanvasSwitch : MonoBehaviour
{
    public RectTransform currentUI;
    public RectTransform nextUI;
    public float slideSpeed = 1f;
    public float screenWidth = 800f;
    public RotateImage reference;
    public RotateImagev2 targetCarousel;

    public void ContinueButton()
    {
        if (reference != null && targetCarousel != null)
        {
            RectTransform selectedFromReference = reference.GetImageAtDotSlot();
            if (selectedFromReference != null)
            {
                targetCarousel.DeselectAllImages();

                for (int i = 0; i < targetCarousel.images.Count; i++)
                {
                    if (targetCarousel.images[i].name == selectedFromReference.name)
                    {
                        targetCarousel.SetInitialSelectionNoSize(i);
                        break;
                    }
                }
            }
        }

        StartCoroutine(SlideLeft());
    }

    public void BackButton()
    {
        StartCoroutine(SlideRight());
    }

    IEnumerator SlideLeft()
    {
        Vector2 currentStart = currentUI.anchoredPosition;
        Vector2 currentEnd = currentStart + new Vector2(-screenWidth, 0);
        Vector2 nextStart = currentStart + new Vector2(screenWidth, 0);
        Vector2 nextEnd = currentStart;
        nextUI.anchoredPosition = nextStart;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * slideSpeed;
            currentUI.anchoredPosition = Vector2.Lerp(currentStart, currentEnd, t);
            nextUI.anchoredPosition = Vector2.Lerp(nextStart, nextEnd, t);
            yield return null;
        }

        currentUI.anchoredPosition = currentEnd;
        nextUI.anchoredPosition = nextEnd;

        RectTransform temp = currentUI;
        currentUI = nextUI;
        nextUI = temp;
    }

    IEnumerator SlideRight()
    {
        Vector2 currentStart = currentUI.anchoredPosition;
        Vector2 currentEnd = currentStart + new Vector2(screenWidth, 0);
        Vector2 nextStart = currentStart + new Vector2(-screenWidth, 0);
        Vector2 nextEnd = currentStart;
        nextUI.anchoredPosition = nextStart;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * slideSpeed;
            currentUI.anchoredPosition = Vector2.Lerp(currentStart, currentEnd, t);
            nextUI.anchoredPosition = Vector2.Lerp(nextStart, nextEnd, t);
            yield return null;
        }

        currentUI.anchoredPosition = currentEnd;
        nextUI.anchoredPosition = nextEnd;

        RectTransform temp = currentUI;
        currentUI = nextUI;
        nextUI = temp;
    }
}
