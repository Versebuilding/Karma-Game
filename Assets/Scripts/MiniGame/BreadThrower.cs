using UnityEngine;

public class BreadThrower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BreadProjectile breadProjectilePrefab;
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private Camera aimCamera;

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
    public float ChargeNormalized => maxChargeTime <= 0f ? 1f : Mathf.Clamp01(chargeTimer / maxChargeTime);

    private void Awake()
    {
        if (throwOrigin == null)
        {
            throwOrigin = transform;
        }

        if (aimCamera == null && findMainCameraIfMissing)
        {
            aimCamera = Camera.main;
        }
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

    private void TryStartCharge()
    {
        if (Time.time < nextThrowTime)
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

        if (breadProjectilePrefab == null || throwOrigin == null)
        {
            chargeTimer = 0f;
            return;
        }

        Vector3 throwDirection = GetThrowDirection();
        float throwForce = Mathf.Lerp(minThrowForce, maxThrowForce, ChargeNormalized);

        BreadProjectile projectileInstance = Instantiate(
            breadProjectilePrefab,
            throwOrigin.position,
            Quaternion.LookRotation(throwDirection)
        );

        projectileInstance.Launch(throwDirection, throwForce, transform);

        chargeTimer = 0f;
        nextThrowTime = Time.time + throwCooldown;
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
    }
}
