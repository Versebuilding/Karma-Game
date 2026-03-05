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
    [Tooltip("Height above parent to hover (must be >= floatAmplitude to prevent terrain clipping)")]
    [Range(0f, 5f)] [SerializeField] private float floatHeight = 0.25f;

    [Tooltip("How high the ghost bobs up and down (in local units)")]
    [Range(0.05f, 2f)] [SerializeField] private float floatAmplitude = 0.15f;

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
        // Auto-migrate: reduce old high defaults on pre-existing scene instances
        // (changing code defaults doesn't update already-serialized Inspector values)
        if (floatHeight >= 0.9f)
            floatHeight = 0.25f;
        if (floatAmplitude >= 0.25f)
            floatAmplitude = 0.15f;

        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;

        if (randomizeOffset)
        {
            floatOffset = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    void Update()
    {
        // Sine wave float — floatHeight keeps the ghost above the parent (terrain level).
        // With floatHeight >= floatAmplitude, the ghost never dips below the parent's Y.
        float sineOffset = Mathf.Sin(Time.time * floatFrequency * Mathf.PI * 2f + floatOffset) * floatAmplitude;
        float yOffset = floatHeight + sineOffset;
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

        // Draw top and bottom of bob range (including floatHeight offset)
        Vector3 top = worldPos + Vector3.up * (floatHeight + floatAmplitude);
        Vector3 bottom = worldPos + Vector3.up * (floatHeight - floatAmplitude);
        Gizmos.DrawLine(top, bottom);
        Gizmos.DrawSphere(top, 0.1f);
        Gizmos.DrawSphere(bottom, 0.1f);

        // Draw terrain line (parent position) for reference
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
        Gizmos.DrawLine(worldPos + Vector3.left * 0.5f, worldPos + Vector3.right * 0.5f);
    }
}
