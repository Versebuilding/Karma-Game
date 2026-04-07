using UnityEngine;
using System.Collections;

public class ChapterSelection : MonoBehaviour
{
    public RectTransform[] images;

    public float scaleSpeed = 5f;

    public float normalScale = 1f;
    public float selectedScale = 1.3f;

    int selectedIndex = 0;
    bool scaling = false;

    void Start()
    {
        for (int i = 0; i < images.Length; i++)
        {
            if (i == selectedIndex)
                images[i].localScale = Vector3.one * selectedScale;
            else
                images[i].localScale = Vector3.one * normalScale;
        }
    }

    public void SelectImage(int index)
    {
        if (scaling) return;
        if (index == selectedIndex) return;

        StartCoroutine(ChangeSelection(index));
    }

    IEnumerator ChangeSelection(int newIndex)
    {
        scaling = true;

        Vector3 oldStart = images[selectedIndex].localScale;
        Vector3 newStart = images[newIndex].localScale;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * scaleSpeed;

            images[selectedIndex].localScale =
                Vector3.Lerp(oldStart, Vector3.one * normalScale, t);

            images[newIndex].localScale =
                Vector3.Lerp(newStart, Vector3.one * selectedScale, t);

            yield return null;
        }

        images[selectedIndex].localScale = Vector3.one * normalScale;
        images[newIndex].localScale = Vector3.one * selectedScale;

        selectedIndex = newIndex;
        scaling = false;
    }
}
