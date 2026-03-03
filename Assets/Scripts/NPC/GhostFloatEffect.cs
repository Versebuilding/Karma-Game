using UnityEngine;

/// <summary>
/// Visual-only floating effect for ghost NPCs. Attach to the child mesh object.
/// Creates a smooth sine-wave bob and optional rotation wobble.
/// Completely independent of GhostNPC — purely cosmetic.
///
/// Setup: Add to the child object that has the mesh/Animator (NOT the root with NavMeshAgent).
/// </summary>
public class GhostFloatEffect : MonoBehaviour
{
    // ─── Float Settings ──────────────────────────────────────────
    [Header("Float Settings")]
    [Tooltip("How high the ghost bobs up and down (in local units)")]
    [Range(0.05f, 2f)] [SerializeField] private float floatAmplitude = 0.3f;

    [Tooltip("Speed of the floating motion (cycles per second)")]
    [Range(0.1f, 5f)] [SerializeField] private float floatFrequency = 1f;

    [Tooltip("Phase offset for the sine wave (use to desync multiple ghosts)")]
    [Range(0f, 6.28f)] [SerializeField] private float floatOffset = 0f;

    [Tooltip("Randomize offset on Start so each ghost bobs differently")]
    [SerializeField] private bool randomizeOffset = true;

    // ─── Rotation Wobble ─────────────────────────────────────────
    [Header("Rotation Wobble")]
    [Tooltip("Enable subtle rotation wobble while floating")]
    [SerializeField] private bool enableWobble = true;

    [Tooltip("Maximum wobble angle in degrees")]
    [Range(0f, 15f)] [SerializeField] private float wobbleAmount = 3f;

    [Tooltip("Speed of the wobble rotation")]
    [Range(0.1f, 3f)] [SerializeField] private float wobbleSpeed = 0.7f;

    // ─── Runtime ─────────────────────────────────────────────────
    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;

    void Start()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;

        if (randomizeOffset)
        {
            floatOffset = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    void Update()
    {
        // Sine wave float
        float yOffset = Mathf.Sin(Time.time * floatFrequency * Mathf.PI * 2f + floatOffset) * floatAmplitude;
        transform.localPosition = new Vector3(
            baseLocalPosition.x,
            baseLocalPosition.y + yOffset,
            baseLocalPosition.z
        );

        // Optional rotation wobble
        if (enableWobble && wobbleAmount > 0f)
        {
            float wobbleZ = Mathf.Sin(Time.time * wobbleSpeed * Mathf.PI * 2f + floatOffset * 0.7f) * wobbleAmount;
            float wobbleX = Mathf.Sin(Time.time * wobbleSpeed * Mathf.PI * 2f * 0.6f + floatOffset * 1.3f) * (wobbleAmount * 0.5f);
            transform.localRotation = baseLocalRotation * Quaternion.Euler(wobbleX, 0f, wobbleZ);
        }
    }

    /// <summary>
    /// Reset to base position (useful if you need to snap back for cutscenes).
    /// </summary>
    public void ResetToBase()
    {
        transform.localPosition = baseLocalPosition;
        transform.localRotation = baseLocalRotation;
    }

    void OnDrawGizmosSelected()
    {
        // Show float range in Scene view
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.4f);
        Vector3 worldPos = Application.isPlaying
            ? transform.parent.TransformPoint(baseLocalPosition)
            : transform.position;

        // Draw top and bottom of bob range
        Vector3 top = worldPos + Vector3.up * floatAmplitude;
        Vector3 bottom = worldPos - Vector3.up * floatAmplitude;
        Gizmos.DrawLine(top, bottom);
        Gizmos.DrawSphere(top, 0.1f);
        Gizmos.DrawSphere(bottom, 0.1f);
    }
}
