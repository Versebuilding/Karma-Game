using UnityEngine;
using UnityEngine.UI;

public class OrbFillUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [Range(0f, 1f)] public float fillAmount = 1f;

    private Material fillMat;

    void Awake()
    {
        fillMat = Instantiate(fillImage.material);
        fillImage.material = fillMat;
    }

    void Update()
    {
        fillMat.SetFloat("_Fill", fillAmount);
    }

    public void SetFill(float value)
    {
        fillAmount = Mathf.Clamp01(value);
        fillMat.SetFloat("_Fill", fillAmount);
    }
}