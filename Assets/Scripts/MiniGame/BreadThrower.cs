using UnityEngine;

public class BreadThrower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BreadProjectile breadProjectilePrefab;
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private FeedingMiniGameManager miniGameManager;

    [Header("Charge")]
    [SerializeField] [Min(0f)] private float minThrowForce = 6f;
    [SerializeField] [Min(0.01f)] private float maxThrowForce = 16f;
    [SerializeField] [Min(0.01f)] private float maxChargeTime = 1.25f;
    [SerializeField] [Min(0f)] private float throwCooldown = 0.15f;

    [Header("Aim")]
    [SerializeField] [Min(0f)] private float upwardBias = 0.15f;
    [SerializeField] private bool useCameraForwardWhenAvailable = true;
    [SerializeField] private bool findMainCameraIfMissing = true;
    [SerializeField] [Min(0.1f)] private float aimDistance = 50f;
    [SerializeField] private LayerMask aimLayers = Physics.DefaultRaycastLayers;

    private float chargeTimer;
    private float nextThrowTime;
    private bool isCharging;

    public bool IsCharging => isCharging;
    public Transform ThrowOrigin => throwOrigin;
    public Camera AimCamera => aimCamera;
    public FeedingMiniGameManager MiniGameManager => miniGameManager;
    public float ChargeNormalized => maxChargeTime <= 0f ? 1f : Mathf.Clamp01(chargeTimer / maxChargeTime);
    public float CurrentThrowForce => Mathf.Lerp(minThrowForce, maxThrowForce, ChargeNormalized);

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryStartCharge();
        }

        if (isCharging && Input.GetMouseButton(0))
        {
            chargeTimer += Time.deltaTime;
        }

        if (isCharging && Input.GetMouseButtonUp(0))
        {
            ReleaseThrow();
        }
    }

    public Vector3 GetCurrentLaunchVelocity()
    {
        if (throwOrigin == null)
        {
            return Vector3.zero;
        }

        return GetThrowDirection() * CurrentThrowForce;
    }

    private void TryStartCharge()
    {
        if (Time.time < nextThrowTime)
        {
            return;
        }

        if (miniGameManager != null && !miniGameManager.IsRunning)
        {
            return;
        }

        if (breadProjectilePrefab == null || throwOrigin == null)
        {
            return;
        }

        chargeTimer = 0f;
        isCharging = true;
    }

    private void ReleaseThrow()
    {
        isCharging = false;

        if (miniGameManager != null && !miniGameManager.IsRunning)
        {
            chargeTimer = 0f;
            return;
        }

        if (breadProjectilePrefab == null || throwOrigin == null)
        {
            chargeTimer = 0f;
            return;
        }

        Vector3 throwDirection = GetThrowDirection();
        float throwForce = CurrentThrowForce;

        BreadProjectile projectileInstance = Instantiate(
            breadProjectilePrefab,
            throwOrigin.position,
            Quaternion.LookRotation(throwDirection)
        );

        projectileInstance.Launch(throwDirection, throwForce, transform, miniGameManager);

        chargeTimer = 0f;
        nextThrowTime = Time.time + throwCooldown;
    }

    private void ResolveReferences()
    {
        if (throwOrigin == null)
        {
            throwOrigin = transform;
        }

        if (aimCamera == null && findMainCameraIfMissing)
        {
            aimCamera = Camera.main;
        }

        if (miniGameManager == null)
        {
            miniGameManager = GetComponentInParent<FeedingMiniGameManager>();
            if (miniGameManager == null)
            {
                miniGameManager = FindFirstObjectByType<FeedingMiniGameManager>();
            }
        }
    }

    private Vector3 GetThrowDirection()
    {
        Vector3 direction = throwOrigin.forward;

        if (useCameraForwardWhenAvailable && aimCamera != null)
        {
            Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 targetPoint = aimRay.origin + (aimRay.direction * aimDistance);

            if (Physics.Raycast(
                aimRay,
                out RaycastHit hit,
                aimDistance,
                aimLayers,
                QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;
            }

            direction = targetPoint - throwOrigin.position;
        }

        direction += Vector3.up * upwardBias;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }

        return direction.normalized;
    }

    private void OnValidate()
    {
        if (maxThrowForce < minThrowForce)
        {
            maxThrowForce = minThrowForce;
        }

        if (throwOrigin == null)
        {
            throwOrigin = transform;
        }

        ResolveReferences();
    }
}
