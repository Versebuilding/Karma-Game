using UnityEngine;

public abstract class BirdBase : MonoBehaviour
{
    [Header("Common Movement")]
    [SerializeField] private bool useLocalSpace = true;
    [SerializeField] private bool rotateWithMovement = true;
    [SerializeField] [Min(0f)] private float rotationLerpSpeed = 10f;
    [SerializeField] private FeedingMiniGameManager miniGameManager;

    protected Vector3 anchorLocalPosition;
    protected Vector3 anchorWorldPosition;
    protected Vector3 lastWorldPosition;
    protected bool hasCachedAnchorPosition;

    public bool IsFeedWindowOpen { get; protected set; }
    public FeedingMiniGameManager MiniGameManager => miniGameManager;

    protected virtual void Awake()
    {
        ResolveMiniGameManager();
        CacheAnchorPosition();
        lastWorldPosition = transform.position;
    }

    protected virtual void OnEnable()
    {
        ResolveMiniGameManager();
    }

    public virtual void ResetBirdState()
    {
        if (!hasCachedAnchorPosition || !Application.isPlaying)
        {
            CacheAnchorPosition();
        }

        IsFeedWindowOpen = false;
        lastWorldPosition = transform.position;
    }

    public virtual void NotifyFed(Component feedSource)
    {
    }

    public virtual void NotifyMissed(Component feedSource, FeedFailureReason failureReason)
    {
    }

    public virtual void TriggerPanic(Vector3 threatPosition, float duration)
    {
    }

    protected void ApplyOffset(Vector3 offset)
    {
        if (useLocalSpace)
        {
            transform.localPosition = anchorLocalPosition + offset;
            return;
        }

        transform.position = anchorWorldPosition + offset;
    }

    protected void ApplyWorldPosition(Vector3 worldPosition)
    {
        if (useLocalSpace && transform.parent != null)
        {
            transform.localPosition = transform.parent.InverseTransformPoint(worldPosition);
            return;
        }

        transform.position = worldPosition;
    }

    protected Vector3 GetWorldPositionForOffset(Vector3 offset)
    {
        if (useLocalSpace && transform.parent != null)
        {
            return transform.parent.TransformPoint(anchorLocalPosition + offset);
        }

        return anchorWorldPosition + offset;
    }

    protected void UpdateRotation()
    {
        if (!rotateWithMovement)
        {
            lastWorldPosition = transform.position;
            return;
        }

        Vector3 movement = transform.position - lastWorldPosition;
        if (movement.sqrMagnitude > 0.0001f)
        {
            Vector3 forward = movement.normalized;
            Vector3 upAxis = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f ? Vector3.right : Vector3.up;
            Quaternion targetRotation = Quaternion.LookRotation(forward, upAxis);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationLerpSpeed * Time.deltaTime
            );
        }

        lastWorldPosition = transform.position;
    }

    protected void CacheAnchorPosition()
    {
        anchorLocalPosition = transform.localPosition;
        anchorWorldPosition = transform.position;
        hasCachedAnchorPosition = true;
    }

    protected Vector3 GetSafeDirection(Vector3 candidate, Vector3 fallback)
    {
        if (candidate.sqrMagnitude < 0.0001f)
        {
            return fallback;
        }

        return candidate.normalized;
    }

    protected virtual void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheAnchorPosition();
        }

        ResolveMiniGameManager();
    }

    private void ResolveMiniGameManager()
    {
        if (miniGameManager == null)
        {
            miniGameManager = GetComponentInParent<FeedingMiniGameManager>();
        }

        if (miniGameManager == null)
        {
            miniGameManager = FindFirstObjectByType<FeedingMiniGameManager>();
        }
    }
}

public class BirdDivePattern : BirdBase
{
    [Header("Dive Path")]
    [SerializeField] private Vector3 sweepDirection = Vector3.forward;
    [SerializeField] private Vector3 diveDirection = Vector3.down;
    [SerializeField] [Min(0f)] private float sweepDistance = 2f;
    [SerializeField] [Min(0f)] private float diveDepth = 2f;
    [SerializeField] [Min(0.05f)] private float cycleDuration = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float cycleOffset;

    [Header("Feeding Window")]
    [SerializeField] [Range(0f, 1f)] private float feedWindowThreshold = 0.85f;

    private float elapsedTime;

    protected override void Awake()
    {
        base.Awake();
        ResetBirdState();
    }

    protected virtual void Update()
    {
        elapsedTime += Time.deltaTime;
        UpdateDiveMotion();
        UpdateRotation();
    }

    public override void ResetBirdState()
    {
        base.ResetBirdState();
        elapsedTime = cycleOffset * Mathf.Max(0.05f, cycleDuration);
        UpdateDiveMotion();
        lastWorldPosition = transform.position;
    }

    private void UpdateDiveMotion()
    {
        float safeCycleDuration = Mathf.Max(0.05f, cycleDuration);
        float normalizedTime = Mathf.Repeat(elapsedTime / safeCycleDuration, 1f);
        float diveAmount = Mathf.Sin(normalizedTime * Mathf.PI);
        float sweepAmount = Mathf.Sin(normalizedTime * Mathf.PI * 2f);

        Vector3 normalizedSweepDirection = GetSafeDirection(sweepDirection, Vector3.forward);
        Vector3 normalizedDiveDirection = GetSafeDirection(diveDirection, Vector3.down);
        Vector3 positionOffset =
            normalizedSweepDirection * (sweepAmount * sweepDistance * 0.5f) +
            normalizedDiveDirection * (diveAmount * diveDepth);

        ApplyOffset(positionOffset);
        IsFeedWindowOpen = diveAmount >= feedWindowThreshold;
    }
}

public class BirdDiveMovement : BirdDivePattern
{
}
