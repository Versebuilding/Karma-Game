using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class RotateImagev2 : MonoBehaviour
{
    public List<RectTransform> images;
    public List<Image> imageComponents;
    public List<Sprite> activeSprites;

    public float radius = 300f;
    public float rotateSpeed = 300f;
    public float startingAngleOffset = -90f;

    public Vector2 activeSize = new Vector2(140f, 180f);

    public RotateImage other;
    public RectTransform spinningCircle;

    private float currentAngle = 0f;
    private bool isRotating = false;

    private int currentActiveIndex = -1;
    private List<Sprite> originalSprites = new List<Sprite>();
    private List<Vector2> originalSizes = new List<Vector2>();

    void Start()
    {
        currentAngle = 0f;

        for (int i = 0; i < imageComponents.Count; i++)
        {
            originalSprites.Add(imageComponents[i].sprite);
            originalSizes.Add(images[i].sizeDelta);
            AddHoverHandlers(i);
        }

        UpdatePositions();

        if (other != null)
        {
            RectTransform selectedFromOther = other.GetImageAtDotSlot();
            if (selectedFromOther != null)
            {
                for (int i = 0; i < images.Count; i++)
                {
                    if (images[i].name == selectedFromOther.name)
                    {
                        SetInitialSelectionNoSize(i);
                        break;
                    }
                }
            }
        }
    }

    void UpdatePositions()
    {
        float angleStep = 360f / images.Count;

        for (int i = 0; i < images.Count; i++)
        {
            float angle = currentAngle + startingAngleOffset + i * angleStep;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
            images[i].anchoredPosition = pos;
        }

        if (spinningCircle != null)
        {
            spinningCircle.localEulerAngles = new Vector3(0f, 0f, currentAngle);
        }
    }

    public void OnImageClicked(int index)
    {
        if (isRotating) return;

        SetActive(index, true);
        StartCoroutine(RotateToIndex(index));
    }

    void SetActive(int index, bool sync)
    {
        if (currentActiveIndex != -1)
        {
            imageComponents[currentActiveIndex].sprite = originalSprites[currentActiveIndex];
            images[currentActiveIndex].sizeDelta = originalSizes[currentActiveIndex];
        }

        imageComponents[index].sprite = activeSprites[index];
        images[index].sizeDelta = activeSize;

        currentActiveIndex = index;

        if (sync && other != null)
            other.SyncSelection(index);
    }

    public void SyncSelection(int index)
    {
        SetActive(index, false);
        StartCoroutine(RotateToIndex(index));
    }

    public void SetInitialSelection(int index)
    {
        SetActive(index, false);
        StartCoroutine(RotateToIndex(index));
    }

    public void SetInitialSelectionNoSize(int index)
    {
        if (currentActiveIndex != -1)
        {
            imageComponents[currentActiveIndex].sprite = originalSprites[currentActiveIndex];
            images[currentActiveIndex].sizeDelta = originalSizes[currentActiveIndex];
        }

        imageComponents[index].sprite = activeSprites[index];
        images[index].sizeDelta = activeSize;

        currentActiveIndex = index;

        StartCoroutine(RotateToIndex(index));
    }

    public void DeselectAllImages()
    {
        for (int i = 0; i < imageComponents.Count; i++)
        {
            imageComponents[i].sprite = originalSprites[i];
            images[i].sizeDelta = originalSizes[i];
        }

        currentActiveIndex = -1;
    }

    IEnumerator RotateToIndex(int index)
    {
        isRotating = true;

        float angleStep = 360f / images.Count;
        float targetAngle = -index * angleStep;

        while (Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle)) > 0.1f)
        {
            currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotateSpeed * Time.deltaTime);
            UpdatePositions();
            yield return null;
        }

        currentAngle = targetAngle;
        UpdatePositions();

        isRotating = false;
    }

    void AddHoverHandlers(int index)
    {
        EventTrigger trigger = images[index].gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = images[index].gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enter = new EventTrigger.Entry();
        enter.eventID = EventTriggerType.PointerEnter;
        enter.callback.AddListener((data) => { OnHoverEnter(index); });
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry();
        exit.eventID = EventTriggerType.PointerExit;
        exit.callback.AddListener((data) => { OnHoverExit(index); });
        trigger.triggers.Add(exit);
    }

    void OnHoverEnter(int index)
    {
        if (currentActiveIndex == index) return;
        imageComponents[index].sprite = activeSprites[index];
        images[index].sizeDelta = activeSize;
    }

    void OnHoverExit(int index)
    {
        if (currentActiveIndex == index) return;
        imageComponents[index].sprite = originalSprites[index];
        images[index].sizeDelta = originalSizes[index];
    }
}