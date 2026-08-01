using UnityEngine;

public class BirdCircleMovement : BirdBase
{
    [Header("Circle Path")]
    [SerializeField] private Vector3 orbitRight = Vector3.right;
    [SerializeField] private Vector3 orbitForward = Vector3.forward;
    [SerializeField] [Min(0f)] private float circleRadius = 2f;
    [SerializeField] [Min(0.05f)] private float circleDuration = 3f;
    [SerializeField] [Range(0f, 1f)] private float phaseOffset;
    [SerializeField] [Min(0f)] private float bobAmplitude = 0.15f;
    [SerializeField] [Min(0f)] private float bobFrequency = 1.2f;

    [Header("Snatch")]
    [SerializeField] [Min(0.05f)] private float snatchInterval = 2.2f;
    [SerializeField] [Min(0.05f)] private float snatchDuration = 0.65f;
    [SerializeField] [Min(0f)] private float snatchDistance = 1.1f;
    [SerializeField] [Min(0.01f)] private float feedWindowDuration = 0.3f;

    private float elapsedTime;

    protected override void Awake()
    {
        base.Awake();
        ResetBirdState();
    }

    protected virtual void Update()
    {
        elapsedTime += Time.deltaTime;
        UpdateCircleMotion();
        UpdateRotation();
    }

    public override void ResetBirdState()
    {
        base.ResetBirdState();
        elapsedTime = phaseOffset * Mathf.Max(0.05f, circleDuration);
        UpdateCircleMotion();
        lastWorldPosition = transform.position;
    }

    protected virtual void UpdateCircleMotion()
    {
        float safeCircleDuration = Mathf.Max(0.05f, circleDuration);
        float orbitAngle = Mathf.Repeat(elapsedTime / safeCircleDuration, 1f) * Mathf.PI * 2f;

        Vector3 right = GetSafeDirection(orbitRight, Vector3.right);
        Vector3 forward = GetSafeDirection(orbitForward, Vector3.forward);
        Vector3 orbitOffset = ((right * Mathf.Cos(orbitAngle)) + (forward * Mathf.Sin(orbitAngle))) * circleRadius;
        Vector3 bobOffset = Vector3.up * (Mathf.Sin(elapsedTime * bobFrequency * Mathf.PI * 2f) * bobAmplitude);

        float safeSnatchInterval = Mathf.Max(snatchInterval, snatchDuration);
        float snatchTimer = Mathf.Repeat(elapsedTime, safeSnatchInterval);
        bool isSnatching = snatchTimer <= snatchDuration;

        float snatchAmount = 0f;
        IsFeedWindowOpen = false;

        if (isSnatching)
        {
            float snatchProgress = snatchTimer / Mathf.Max(0.05f, snatchDuration);
            snatchAmount = Mathf.Sin(snatchProgress * Mathf.PI) * snatchDistance;
            float halfWindow = feedWindowDuration * 0.5f;
            IsFeedWindowOpen = Mathf.Abs(snatchTimer - (snatchDuration * 0.5f)) <= halfWindow;
        }

        Vector3 inwardDirection = orbitOffset.sqrMagnitude < 0.0001f
            ? -forward
            : -orbitOffset.normalized;

        ApplyOffset(orbitOffset + bobOffset + (inwardDirection * snatchAmount));
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        if (snatchInterval < snatchDuration)
        {
            snatchInterval = snatchDuration;
        }

        if (feedWindowDuration > snatchDuration)
        {
            feedWindowDuration = snatchDuration;
        }
    }
}
