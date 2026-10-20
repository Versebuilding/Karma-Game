
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Backpack_Inventory : MonoBehaviour
{
    public Sprite pinkLabel;
    public Sprite blueLabel;
    public RectTransform bag;

    public float selectedScale = 1.08f;
    public float animationSpeed = 0.15f;
    public float deselectAnimationSpeed = 0.06f;
    public float logoMoveAmount = 400f;

    private Image image;
    private Vector3 normalScale;
    private Vector2 bagNormalPosition;

    private void Awake()
    {
        image = GetComponent<Image>();
        normalScale = transform.localScale;

        if (bag != null)
            bagNormalPosition = bag.anchoredPosition;
    }

    public void Select()
    {
        image.sprite = blueLabel;
        StopAllCoroutines();
        StartCoroutine(ScaleLabel(true));
    }

    public void Deselect()
    {
        image.sprite = pinkLabel;
        StopAllCoroutines();
        StartCoroutine(ScaleLabel(false));
    }

    private IEnumerator ScaleLabel(bool selected)
    {
        float duration = selected ? animationSpeed : deselectAnimationSpeed;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = normalScale * (selected ? selectedScale : 1f);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Smooth start and finish
            float smoothT = t * t * (3f - 2f * t);

            transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            UpdateBagPosition();

            yield return null;
        }

        transform.localScale = targetScale;
        UpdateBagPosition();
    }

    private void UpdateBagPosition()
    {
        if (bag == null) return;

        float scaleRatio = transform.localScale.x / normalScale.x;

        bag.anchoredPosition = new Vector2(
            bagNormalPosition.x + logoMoveAmount * (scaleRatio - 1f),
            bagNormalPosition.y
        );
    }
}
