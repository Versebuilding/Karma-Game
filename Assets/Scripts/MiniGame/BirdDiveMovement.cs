using UnityEngine;

public class BirdDiveMovement : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private bool useLocalSpace = true;
    [SerializeField] private Vector3 sweepDirection = Vector3.forward;
    [SerializeField] private Vector3 diveDirection = Vector3.down;
    [SerializeField] [Min(0f)] private float sweepDistance = 2f;
    [SerializeField] [Min(0f)] private float diveDepth = 2f;
    [SerializeField] [Min(0.05f)] private float cycleDuration = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float cycleOffset = 0f;

    [Header("Feeding Window")]
    [SerializeField] [Range(0f, 1f)] private float feedWindowThreshold = 0.85f;

    [Header("Rotation")]
    [SerializeField] private bool rotateWithMovement = true;
    [SerializeField] [Min(0f)] private float rotationLerpSpeed = 10f;

    private Vector3 anchorLocalPosition;
    private Vector3 anchorWorldPosition;
    private Vector3 lastWorldPosition;
    private float elapsedTime;

    public bool IsFeedWindowOpen { get; private set; }

    private void Awake()
    {
        CacheAnchorPosition();
        elapsedTime = cycleOffset * cycleDuration;
        lastWorldPosition = transform.position;
        UpdateDiveMotion();
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        UpdateDiveMotion();
        UpdateRotation();
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

        if (useLocalSpace)
        {
            transform.localPosition = anchorLocalPosition + positionOffset;
        }
        else
        {
            transform.position = anchorWorldPosition + positionOffset;
        }

        IsFeedWindowOpen = diveAmount >= feedWindowThreshold;
    }

    private void UpdateRotation()
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

    private void CacheAnchorPosition()
    {
        anchorLocalPosition = transform.localPosition;
        anchorWorldPosition = transform.position;
    }

    private Vector3 GetSafeDirection(Vector3 candidate, Vector3 fallback)
    {
        if (candidate.sqrMagnitude < 0.0001f)
        {
            return fallback;
        }

        return candidate.normalized;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CacheAnchorPosition();
        }
    }
}
