using UnityEngine;

public class BirdSwarmMovement : BirdBase
{
    private readonly Collider[] collisionBuffer = new Collider[8];

    [Header("Swarm Orbit")]
    [SerializeField] private Vector3 orbitRight = Vector3.right;
    [SerializeField] private Vector3 orbitForward = Vector3.forward;
    [SerializeField] [Min(0f)] private float orbitRadius = 1.4f;
    [SerializeField] [Min(0.05f)] private float orbitDuration = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float phaseOffset;
    [SerializeField] [Min(0f)] private float bobAmplitude = 0.2f;
    [SerializeField] [Min(0f)] private float bobFrequency = 1.6f;

    [Header("Feed Window")]
    [SerializeField] [Min(0.05f)] private float dartInterval = 1.8f;
    [SerializeField] [Min(0.05f)] private float dartDuration = 0.45f;
    [SerializeField] [Min(0f)] private float dartDistance = 0.9f;
    [SerializeField] [Min(0.01f)] private float feedWindowDuration = 0.22f;

    [Header("Panic")]
    [SerializeField] [Min(0f)] private float panicScatterDistance = 2.5f;
    [SerializeField] [Min(0.05f)] private float defaultPanicDuration = 1.5f;
    [SerializeField] [Min(0f)] private float collisionPanicCooldown = 0.3f;
    [SerializeField] [Min(0.05f)] private float collisionCheckRadius = 0.9f;

    private float elapsedTime;
    private float panicTimer;
    private float activePanicDuration;
    private float nextCollisionPanicTime;
    private Vector3 panicDirection;

    protected override void Awake()
    {
        base.Awake();
        ResetBirdState();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (MiniGameManager != null)
        {
            MiniGameManager.RegisterSwarmBird(this);
        }
    }

    private void OnDisable()
    {
        if (MiniGameManager != null)
        {
            MiniGameManager.UnregisterSwarmBird(this);
        }
    }

    protected virtual void Update()
    {
        elapsedTime += Time.deltaTime;

        if (panicTimer > 0f)
        {
            panicTimer = Mathf.Max(0f, panicTimer - Time.deltaTime);
        }

        UpdateSwarmMotion();
        CheckForSwarmCollision();
        UpdateRotation();
    }

    public override void ResetBirdState()
    {
        base.ResetBirdState();
        elapsedTime = phaseOffset * Mathf.Max(0.05f, orbitDuration);
        panicTimer = 0f;
        activePanicDuration = 0f;
        panicDirection = Vector3.zero;
        nextCollisionPanicTime = 0f;
        UpdateSwarmMotion();
        lastWorldPosition = transform.position;
    }

    public override void NotifyFed(Component feedSource)
    {
        if (MiniGameManager != null)
        {
            MiniGameManager.NotifySwarmBirdFed(this, transform.position);
        }
    }

    public override void NotifyMissed(Component feedSource, FeedFailureReason failureReason)
    {
        if (failureReason == FeedFailureReason.FeedWindowClosed)
        {
            TriggerPanic(transform.position, defaultPanicDuration);
        }
    }

    public override void TriggerPanic(Vector3 threatPosition, float duration)
    {
        activePanicDuration = Mathf.Max(0.05f, duration > 0f ? duration : defaultPanicDuration);
        panicTimer = activePanicDuration;

        Vector3 fleeDirection = transform.position - threatPosition;
        fleeDirection += new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
        fleeDirection.y = 0.15f;
        panicDirection = GetSafeDirection(fleeDirection, Vector3.up + Vector3.forward);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || Time.time < nextCollisionPanicTime)
        {
            return;
        }

        BirdSwarmMovement otherBird = other.GetComponentInParent<BirdSwarmMovement>();
        if (otherBird == null || otherBird == this)
        {
            return;
        }

        nextCollisionPanicTime = Time.time + collisionPanicCooldown;

        if (MiniGameManager != null)
        {
            MiniGameManager.NotifyBirdCollision((transform.position + otherBird.transform.position) * 0.5f);
        }
    }

    private void CheckForSwarmCollision()
    {
        if (MiniGameManager == null || Time.time < nextCollisionPanicTime)
        {
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            collisionCheckRadius,
            collisionBuffer,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = collisionBuffer[i];
            if (hitCollider == null)
            {
                continue;
            }

            BirdSwarmMovement otherBird = hitCollider.GetComponentInParent<BirdSwarmMovement>();
            if (otherBird == null || otherBird == this)
            {
                continue;
            }

            nextCollisionPanicTime = Time.time + collisionPanicCooldown;
            MiniGameManager.NotifyBirdCollision((transform.position + otherBird.transform.position) * 0.5f);
            return;
        }
    }

    private void UpdateSwarmMotion()
    {
        float safeOrbitDuration = Mathf.Max(0.05f, orbitDuration);
        float orbitAngle = Mathf.Repeat(elapsedTime / safeOrbitDuration, 1f) * Mathf.PI * 2f;

        Vector3 right = GetSafeDirection(orbitRight, Vector3.right);
        Vector3 forward = GetSafeDirection(orbitForward, Vector3.forward);
        Vector3 orbitOffset = ((right * Mathf.Cos(orbitAngle)) + (forward * Mathf.Sin(orbitAngle))) * orbitRadius;
        Vector3 bobOffset = Vector3.up * (Mathf.Sin(elapsedTime * bobFrequency * Mathf.PI * 2f) * bobAmplitude);

        float safeDartInterval = Mathf.Max(dartInterval, dartDuration);
        float dartTimer = Mathf.Repeat(elapsedTime, safeDartInterval);
        bool isDarting = dartTimer <= dartDuration;

        float dartAmount = 0f;
        IsFeedWindowOpen = false;

        if (isDarting)
        {
            float dartProgress = dartTimer / Mathf.Max(0.05f, dartDuration);
            dartAmount = Mathf.Sin(dartProgress * Mathf.PI) * dartDistance;
            float halfWindow = feedWindowDuration * 0.5f;
            IsFeedWindowOpen = Mathf.Abs(dartTimer - (dartDuration * 0.5f)) <= halfWindow;
        }

        Vector3 inwardDirection = orbitOffset.sqrMagnitude < 0.0001f
            ? -forward
            : -orbitOffset.normalized;

        Vector3 panicOffset = Vector3.zero;
        if (panicTimer > 0f && activePanicDuration > 0f)
        {
            float panicProgress = 1f - (panicTimer / activePanicDuration);
            panicOffset = panicDirection * (Mathf.Sin(panicProgress * Mathf.PI) * panicScatterDistance);
            IsFeedWindowOpen = false;
        }

        ApplyOffset(orbitOffset + bobOffset + (inwardDirection * dartAmount) + panicOffset);
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        if (dartInterval < dartDuration)
        {
            dartInterval = dartDuration;
        }

        if (feedWindowDuration > dartDuration)
        {
            feedWindowDuration = dartDuration;
        }
    }
}
