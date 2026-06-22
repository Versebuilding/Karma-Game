using UnityEngine;

public class BirdInterceptMovement : BirdBase
{
    [Header("Patrol")]
    [SerializeField] private Vector3 patrolDirection = Vector3.right;
    [SerializeField] [Min(0f)] private float patrolDistance = 3f;
    [SerializeField] [Min(0.05f)] private float patrolDuration = 3f;
    [SerializeField] [Range(0f, 1f)] private float patrolOffset;
    [SerializeField] [Min(0f)] private float hoverAmplitude = 0.3f;
    [SerializeField] [Min(0f)] private float hoverFrequency = 1.1f;
    [SerializeField] [Min(0f)] private float returnSpeed = 7f;

    [Header("Intercept")]
    [SerializeField] [Min(0f)] private float detectionRadius = 10f;
    [SerializeField] [Min(0f)] private float interceptSpeed = 11f;
    [SerializeField] [Min(0f)] private float interceptLeadTime = 0.12f;
    [SerializeField] [Min(0.05f)] private float maxInterceptDuration = 1.25f;
    [SerializeField] [Min(0f)] private float interceptCooldown = 1.2f;

    private BreadProjectile trackedProjectile;
    private float elapsedTime;
    private float interceptTimer;
    private float interceptCooldownTimer;

    protected override void Awake()
    {
        base.Awake();
        ResetBirdState();
    }

    protected virtual void Update()
    {
        elapsedTime += Time.deltaTime;

        if (interceptCooldownTimer > 0f)
        {
            interceptCooldownTimer = Mathf.Max(0f, interceptCooldownTimer - Time.deltaTime);
        }

        if (!IsIntercepting() && CanAcquireProjectile(out BreadProjectile projectile))
        {
            trackedProjectile = projectile;
            interceptTimer = Mathf.Max(0.05f, maxInterceptDuration);
        }

        if (IsIntercepting())
        {
            UpdateInterceptMotion();
        }
        else
        {
            UpdatePatrolMotion();
        }

        UpdateRotation();
    }

    public override void ResetBirdState()
    {
        base.ResetBirdState();
        elapsedTime = patrolOffset * Mathf.Max(0.05f, patrolDuration);
        trackedProjectile = null;
        interceptTimer = 0f;
        interceptCooldownTimer = 0f;
        UpdatePatrolMotion(true);
        lastWorldPosition = transform.position;
    }

    public override void NotifyFed(Component feedSource)
    {
        trackedProjectile = null;
        interceptTimer = 0f;
        interceptCooldownTimer = interceptCooldown;
        IsFeedWindowOpen = false;
    }

    private bool CanAcquireProjectile(out BreadProjectile projectile)
    {
        projectile = null;

        if (MiniGameManager == null || !MiniGameManager.IsRunning || interceptCooldownTimer > 0f)
        {
            return false;
        }

        return MiniGameManager.TryGetNearestActiveProjectile(transform.position, detectionRadius, out projectile);
    }

    private bool IsIntercepting()
    {
        return trackedProjectile != null && trackedProjectile.IsActiveProjectile && interceptTimer > 0f;
    }

    private void UpdateInterceptMotion()
    {
        interceptTimer -= Time.deltaTime;

        if (trackedProjectile == null || !trackedProjectile.IsActiveProjectile || interceptTimer <= 0f)
        {
            trackedProjectile = null;
            interceptTimer = 0f;
            interceptCooldownTimer = interceptCooldown;
            IsFeedWindowOpen = false;
            UpdatePatrolMotion();
            return;
        }

        Vector3 predictedPosition = trackedProjectile.transform.position + (trackedProjectile.CurrentVelocity * interceptLeadTime);
        Vector3 nextPosition = Vector3.MoveTowards(
            transform.position,
            predictedPosition,
            interceptSpeed * Time.deltaTime
        );

        ApplyWorldPosition(nextPosition);
        IsFeedWindowOpen = true;
    }

    private void UpdatePatrolMotion(bool immediate = false)
    {
        float safePatrolDuration = Mathf.Max(0.05f, patrolDuration);
        float patrolTime = Mathf.Repeat(elapsedTime / safePatrolDuration, 1f) * Mathf.PI * 2f;
        Vector3 direction = GetSafeDirection(patrolDirection, Vector3.right);
        Vector3 patrolOffsetVector = direction * (Mathf.Sin(patrolTime) * patrolDistance * 0.5f);
        patrolOffsetVector += Vector3.up * (Mathf.Sin(elapsedTime * hoverFrequency * Mathf.PI * 2f) * hoverAmplitude);

        Vector3 patrolWorldPosition = GetWorldPositionForOffset(patrolOffsetVector);
        if (immediate)
        {
            ApplyWorldPosition(patrolWorldPosition);
        }
        else
        {
            Vector3 nextPosition = Vector3.MoveTowards(
                transform.position,
                patrolWorldPosition,
                returnSpeed * Time.deltaTime
            );
            ApplyWorldPosition(nextPosition);
        }

        IsFeedWindowOpen = false;
    }
}
