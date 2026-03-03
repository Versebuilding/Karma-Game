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

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
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
        if (animator) animator.SetBool("isTalking", isTalking);
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
        if (animator) animator.SetInteger("Variant", currentVariant);
    }
}