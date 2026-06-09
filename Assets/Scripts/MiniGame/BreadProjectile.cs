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

    [Header("Collision")]
    [SerializeField] private bool ignoreOwnerColliders = true;
    [SerializeField] private bool rotateWithVelocity = true;
    [SerializeField] [Min(0f)] private float minVelocityToRotate = 0.1f;

    private Transform ownerRoot;
    private bool hasLaunched;
    private bool hasResolvedFeed;

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
        if (!rotateWithVelocity || !hasLaunched || hasResolvedFeed || projectileRigidbody == null)
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

    public void Launch(Vector3 direction, float launchForce, Transform ownerTransform = null)
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
        hasLaunched = true;
        hasResolvedFeed = false;

        if (ignoreOwnerColliders && projectileCollider != null && ownerTransform != null)
        {
            Collider[] ownerColliders = ownerTransform.GetComponentsInChildren<Collider>();
            for (int i = 0; i < ownerColliders.Length; i++)
            {
                Physics.IgnoreCollision(projectileCollider, ownerColliders[i], true);
            }
        }

        projectileRigidbody.linearVelocity = direction.normalized * launchForce;

        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryFeedTarget(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryFeedTarget(other);
    }

    private void TryFeedTarget(Collider other)
    {
        if (hasResolvedFeed || other == null)
        {
            return;
        }

        if (ownerRoot != null && other.transform.root == ownerRoot.root)
        {
            return;
        }

        FeedingTarget feedingTarget = other.GetComponentInParent<FeedingTarget>();
        if (feedingTarget == null)
        {
            return;
        }

        if (!feedingTarget.TryFeed(this))
        {
            return;
        }

        ResolveSuccessfulFeed(feedingTarget.transform);
    }

    private void ResolveSuccessfulFeed(Transform feedTargetTransform)
    {
        hasResolvedFeed = true;

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
