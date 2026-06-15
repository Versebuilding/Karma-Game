using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Trajectory : MonoBehaviour
{
    private LineRenderer lineRenderer;

    [Header("References")]
    [Tooltip("The ThrowManager this trajectory queries to calculate and predict the launch arc")]
    [SerializeField] private ThrowManager throwManager;
    [Tooltip("The starting location of the trajectory line")]
    [SerializeField] private Transform throwOrigin;

    [Header("Simulation Settings")]
    [Tooltip("The maximum iterations the simulation will go through (cut off early if a hit is registered)")]
    [SerializeField] private int maxSimulationSteps = 50;
    [Tooltip("The time between each calculated iteration of the simulation (smaller => more precise stepping but shorter simulated path")]
    [SerializeField] private float timeStep = 0.05f;
    
    private void Awake() {
        lineRenderer = GetComponent<LineRenderer>();
    }

    void Update()
    {
        RenderTrajectory(throwOrigin.position, throwManager.GetThrowVelocity());
    }

    public void RenderTrajectory(Vector3 startPosition, Vector3 initialVelocity) {
        // Set Fundamental Values:
        lineRenderer.positionCount = maxSimulationSteps;
        Vector3 currentPosition = startPosition;
        Vector3 currentVelocity = initialVelocity;
        Vector3 gravity = Physics.gravity; // FIX: add multiplier capabilities

        // Line Stepping:
        for (int i = 0; i < maxSimulationSteps; i++) {
            lineRenderer.SetPosition(i, currentPosition);

            // Calculate Next Step:
            Vector3 nextPosition = currentPosition + (currentVelocity * timeStep) + (0.5f * timeStep * timeStep * gravity);
            // x(f) = x(i) + v(i)t + 1/2at^2
            // t * t * a : simple multiplication squaring & factor reordering gives better performance

            Vector3 nextVelocity = currentVelocity + (gravity * timeStep);
            // v(f) = v(i) + at

            // Check For Next Step Object Hit: set final step & return on hit
            Vector3 displacement = nextPosition - currentPosition;
            if (Physics.Raycast(currentPosition, displacement.normalized, out RaycastHit hit, displacement.magnitude)) {
                lineRenderer.positionCount = i + 1;
                lineRenderer.SetPosition(i, hit.point);
                break;
            }

            // Iterate Step:
            currentPosition = nextPosition;
            currentVelocity = nextVelocity;
        }
    }

    private void OnValidate() {
        if (throwOrigin == null) {
            throwOrigin = transform;
        }

        if (throwManager == null) {
            Debug.LogError("One or more references in the " + name + " object are null...");
        }
    }
}