using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BreadProjectile : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody projectileRigidbody;
    [SerializeField] private Collider projectileCollider;

    [Header("Lifetime")]
    [SerializeField] [Min(0.1f)] private float lifetime = 6f;
    [SerializeField] [Min(0f)] private float destroyDelayAfterFeed = 0.05f;
    [SerializeField] [Min(0f)] private float destroyDelayAfterMiss = 0.05f;

    [Header("Collision")]
    [SerializeField] private bool ignoreOwnerColliders = true;
    [SerializeField] private bool rotateWithVelocity = true;
    [SerializeField] [Min(0f)] private float minVelocityToRotate = 0.1f;
    [SerializeField] [Min(0f)] private float minimumFlightTimeBeforeWorldMiss = 0.05f;
    [SerializeField] private bool registerLifetimeExpiryAsMiss;

    private Transform ownerRoot;
    private FeedingMiniGameManager miniGameManager;
    private float launchTime;
    private bool hasLaunched;
    private bool hasResolvedImpact;

    public Vector3 CurrentVelocity => projectileRigidbody != null ? projectileRigidbody.linearVelocity : Vector3.zero;
    public bool IsActiveProjectile => hasLaunched && !hasResolvedImpact && isActiveAndEnabled;

    private void Awake()
    {
        if (projectileRigidbody == null)
        {
            projectileRigidbody = GetComponent<Rigidbody>();
        }

        if (projectileCollider == null)
        {
            projectileCollider = GetComponent<Collider>();
        }
    }

    private void FixedUpdate()
    {
        if (!rotateWithVelocity || !hasLaunched || hasResolvedImpact || projectileRigidbody == null)
        {
            return;
        }

        Vector3 velocity = projectileRigidbody.linearVelocity;
        if (velocity.sqrMagnitude < minVelocityToRotate * minVelocityToRotate)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
    }

    public void Launch(
        Vector3 direction,
        float launchForce,
        Transform ownerTransform = null,
        FeedingMiniGameManager runtimeMiniGameManager = null)
    {
        if (projectileRigidbody == null)
        {
            projectileRigidbody = GetComponent<Rigidbody>();
        }

        if (projectileCollider == null)
        {
            projectileCollider = GetComponent<Collider>();
        }

        ownerRoot = ownerTransform;
        miniGameManager = runtimeMiniGameManager;
        launchTime = Time.time;
        hasLaunched = true;
        hasResolvedImpact = false;

        if (ignoreOwnerColliders && projectileCollider != null && ownerTransform != null)
        {
            Collider[] ownerColliders = ownerTransform.GetComponentsInChildren<Collider>();
            for (int i = 0; i < ownerColliders.Length; i++)
            {
                Physics.IgnoreCollision(projectileCollider, ownerColliders[i], true);
            }
        }

        projectileRigidbody.useGravity = true;
        projectileRigidbody.isKinematic = false;
        projectileRigidbody.detectCollisions = true;
        projectileRigidbody.linearVelocity = direction.normalized * launchForce;

        if (miniGameManager != null)
        {
            miniGameManager.RegisterProjectile(this);
        }

        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasResolvedImpact || collision == null)
        {
            return;
        }

        HandleImpact(collision.collider, true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasResolvedImpact || other == null)
        {
            return;
        }

        HandleImpact(other, false);
    }

    private void OnDestroy()
    {
        if (miniGameManager != null)
        {
            miniGameManager.UnregisterProjectile(this);
        }

        if (!registerLifetimeExpiryAsMiss || !hasLaunched || hasResolvedImpact)
        {
            return;
        }

        if (miniGameManager != null)
        {
            miniGameManager.RegisterMissedThrow(this, FeedFailureReason.LifetimeExpired);
        }
    }

    private void HandleImpact(Collider other, bool countWorldCollisionAsMiss)
    {
        if (other == null)
        {
            return;
        }

        if (ownerRoot != null && other.transform.root == ownerRoot.root)
        {
            return;
        }

        FeedAttemptResult result = TryFeedTarget(other, out FeedingTarget feedingTarget);
        if (result == FeedAttemptResult.Success)
        {
            ResolveSuccessfulFeed(feedingTarget != null ? feedingTarget.transform : null);
            return;
        }

        if (result == FeedAttemptResult.Missed)
        {
            ResolveMiss();
            return;
        }

        if (!countWorldCollisionAsMiss || Time.time < launchTime + minimumFlightTimeBeforeWorldMiss)
        {
            return;
        }

        if (miniGameManager != null)
        {
            miniGameManager.RegisterMissedThrow(this, FeedFailureReason.MissedThrow);
        }

        ResolveMiss();
    }

    private FeedAttemptResult TryFeedTarget(Collider other, out FeedingTarget feedingTarget)
    {
        feedingTarget = other.GetComponentInParent<FeedingTarget>();
        if (feedingTarget == null)
        {
            return FeedAttemptResult.Ignored;
        }

        return feedingTarget.TryFeed(this, out _);
    }

    private void ResolveSuccessfulFeed(Transform feedTargetTransform)
    {
        hasResolvedImpact = true;
        miniGameManager?.UnregisterProjectile(this);

        if (projectileRigidbody != null)
        {
            projectileRigidbody.linearVelocity = Vector3.zero;
            projectileRigidbody.angularVelocity = Vector3.zero;
            projectileRigidbody.isKinematic = true;
            projectileRigidbody.detectCollisions = false;
        }

        if (feedTargetTransform != null)
        {
            transform.SetParent(feedTargetTransform, true);
        }

        Destroy(gameObject, destroyDelayAfterFeed);
    }

    private void ResolveMiss()
    {
        hasResolvedImpact = true;
        miniGameManager?.UnregisterProjectile(this);

        if (projectileRigidbody != null)
        {
            projectileRigidbody.linearVelocity = Vector3.zero;
            projectileRigidbody.angularVelocity = Vector3.zero;
            projectileRigidbody.isKinematic = true;
            projectileRigidbody.detectCollisions = false;
        }

        Destroy(gameObject, destroyDelayAfterMiss);
    }

    private void OnValidate()
    {
        if (projectileRigidbody == null)
        {
            projectileRigidbody = GetComponent<Rigidbody>();
        }

        if (projectileCollider == null)
        {
            projectileCollider = GetComponent<Collider>();
        }
    }
}
