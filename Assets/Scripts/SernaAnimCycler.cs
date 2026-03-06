using UnityEngine;

public class SernaAnimCycler : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;

    [Header("Variant Cycling")]
    public int variantCount = 3;          // 3 idles / 3 talks
    public float idleChangeEvery = 4f;    // seconds
    public float talkChangeEvery = 3f;    // seconds

    private bool isTalking;
    private int currentVariant;
    private float timer;

    // Cached parameter existence flags (prevents "Parameter does not exist" errors)
    private bool hasTalkingParam;
    private bool hasVariantParam;

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);

        // Cache which parameters actually exist in the Animator Controller
        if (animator != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == "isTalking" && param.type == AnimatorControllerParameterType.Bool)
                    hasTalkingParam = true;
                if (param.name == "Variant" && param.type == AnimatorControllerParameterType.Int)
                    hasVariantParam = true;
            }

#if UNITY_EDITOR
            if (!hasTalkingParam)
                Debug.LogWarning($"SernaAnimCycler: Animator on '{gameObject.name}' is missing 'isTalking' (Bool) parameter.");
            if (!hasVariantParam)
                Debug.LogWarning($"SernaAnimCycler: Animator on '{gameObject.name}' is missing 'Variant' (Int) parameter.");
#endif
        }
    }

    void Start()
    {
        // start in idle variant 0
        SetVariant(0);
        SetTalking(false);
    }

    void Update()
    {
        timer += Time.deltaTime;

        float interval = isTalking ? talkChangeEvery : idleChangeEvery;
        if (interval <= 0.1f) interval = 0.1f;

        if (timer >= interval)
        {
            timer = 0f;
            NextVariant();
        }
    }

    public void SetTalking(bool talking)
    {
        isTalking = talking;
        timer = 0f; // restart cycling when mode changes
        if (animator && hasTalkingParam) animator.SetBool("isTalking", isTalking);
        // optional: reset variant when switching mode
        SetVariant(0);
    }

    private void NextVariant()
    {
        currentVariant = (currentVariant + 1) % variantCount;
        SetVariant(currentVariant);
    }

    private void SetVariant(int v)
    {
        currentVariant = Mathf.Clamp(v, 0, variantCount - 1);
        if (animator && hasVariantParam) animator.SetInteger("Variant", currentVariant);
    }
}