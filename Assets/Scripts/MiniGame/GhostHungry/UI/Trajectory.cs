using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Trajectory : MonoBehaviour
{
    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    private LineRenderer lineRenderer;

    [Header("References")]
    [Tooltip("The legacy ThrowManager this trajectory can query to calculate and predict the launch arc")]
    [SerializeField] private ThrowManager throwManager;
    [Tooltip("Optional BreadThrower source for the shared bird feeding prototype")]
    [SerializeField] private BreadThrower breadThrower;
    [Tooltip("The starting location of the trajectory line")]
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private Transform ignoredRoot;

    [Header("Display")]
    [SerializeField] private bool showWhileChargingOnly;

    [Header("Simulation Settings")]
    [Tooltip("The maximum iterations the simulation will go through (cut off early if a hit is registered)")]
    [SerializeField] [Min(2)] private int maxSimulationSteps = 50;
    [Tooltip("The time between each calculated iteration of the simulation (smaller => more precise stepping but shorter simulated path")]
    [SerializeField] [Min(0.001f)] private float timeStep = 0.05f;
    [SerializeField] private LayerMask collisionMask = Physics.DefaultRaycastLayers;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        if (!TryGetTrajectoryData(out Vector3 startPosition, out Vector3 initialVelocity))
        {
            HideTrajectory();
            return;
        }

        RenderTrajectory(startPosition, initialVelocity);
    }

    public void RenderTrajectory(Vector3 startPosition, Vector3 initialVelocity)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.positionCount = maxSimulationSteps;
        Vector3 currentPosition = startPosition;
        Vector3 currentVelocity = initialVelocity;
        Vector3 gravity = Physics.gravity;

        for (int i = 0; i < maxSimulationSteps; i++)
        {
            lineRenderer.SetPosition(i, currentPosition);

            Vector3 nextPosition = currentPosition
                + (currentVelocity * timeStep)
                + (0.5f * timeStep * timeStep * gravity);

            Vector3 nextVelocity = currentVelocity + (gravity * timeStep);

            Vector3 displacement = nextPosition - currentPosition;
            if (TryGetCollision(currentPosition, displacement, out RaycastHit hit))
            {
                lineRenderer.positionCount = i + 1;
                lineRenderer.SetPosition(i, hit.point);
                return;
            }

            currentPosition = nextPosition;
            currentVelocity = nextVelocity;
        }
    }

    private bool TryGetTrajectoryData(out Vector3 startPosition, out Vector3 initialVelocity)
    {
        startPosition = Vector3.zero;
        initialVelocity = Vector3.zero;

        if (breadThrower != null)
        {
            if (breadThrower.MiniGameManager != null && !breadThrower.MiniGameManager.IsRunning)
            {
                return false;
            }

            if (showWhileChargingOnly && !breadThrower.IsCharging)
            {
                return false;
            }

            if (throwOrigin == null)
            {
                return false;
            }

            startPosition = throwOrigin.position;
            initialVelocity = breadThrower.GetCurrentLaunchVelocity();
            return true;
        }

        if (throwManager != null && throwOrigin != null)
        {
            startPosition = throwOrigin.position;
            initialVelocity = throwManager.GetThrowVelocity();
            return true;
        }

        return false;
    }

    private bool TryGetCollision(Vector3 currentPosition, Vector3 displacement, out RaycastHit closestHit)
    {
        closestHit = default;

        float distance = displacement.magnitude;
        if (distance <= 0.0001f)
        {
            return false;
        }

        int hitCount = Physics.RaycastNonAlloc(
            currentPosition,
            displacement / distance,
            hitBuffer,
            distance,
            collisionMask,
            triggerInteraction);

        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null || ShouldIgnore(hit.collider))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
            }
        }

        return closestDistance < float.MaxValue;
    }

    private bool ShouldIgnore(Collider other)
    {
        if (ignoredRoot == null || other == null)
        {
            return false;
        }

        return other.transform.root == ignoredRoot.root;
    }

    private void HideTrajectory()
    {
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }
    }

    private void ResolveReferences()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (breadThrower == null)
        {
            breadThrower = GetComponentInParent<BreadThrower>();
        }

        if (throwOrigin == null)
        {
            if (breadThrower != null)
            {
                throwOrigin = breadThrower.ThrowOrigin;
            }
            else
            {
                throwOrigin = transform;
            }
        }

        if (ignoredRoot == null)
        {
            if (breadThrower != null)
            {
                ignoredRoot = breadThrower.transform;
            }
            else if (throwManager != null)
            {
                ignoredRoot = throwManager.transform;
            }
        }
    }

    private void OnValidate()
    {
        if (maxSimulationSteps < 2)
        {
            maxSimulationSteps = 2;
        }

        if (timeStep < 0.001f)
        {
            timeStep = 0.001f;
        }

        if (throwOrigin == null && breadThrower == null)
        {
            throwOrigin = transform;
        }
    }
}
