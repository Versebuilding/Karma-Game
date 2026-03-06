using UnityEngine;

/// <summary>
/// Individual falling food piece. Managed by FoodRainManager's object pool.
/// Falls downward with random tumble rotation, shrinks and disappears near ground.
///
/// Do NOT add this manually — FoodRainManager creates and pools these automatically.
/// </summary>
public class FallingFood : MonoBehaviour
{
    // Configured per-launch by FoodRainManager
    private float fallSpeed;
    private float groundLevel;
    private float tumbleSpeed;
    private Vector3 tumbleAxis;
    private Vector3 baseScale;
    private float vanishZone; // Y range above ground where shrink starts

    // Runtime
    private bool isActive;

    /// <summary>
    /// Initialize and launch this food piece. Called by FoodRainManager when
    /// pulling from pool.
    /// </summary>
    public void Launch(float speed, float ground, float tumble, float vanishHeight)
    {
        fallSpeed = speed;
        groundLevel = ground;
        tumbleSpeed = tumble;
        vanishZone = vanishHeight;
        baseScale = transform.localScale;

        // Random tumble direction — each food spins uniquely
        tumbleAxis = Random.onUnitSphere;

        // Random initial rotation so identical prefabs look different
        transform.rotation = Random.rotation;

        isActive = true;
    }

    void Update()
    {
        if (!isActive) return;

        // ── Fall ──
        transform.position += Vector3.down * (fallSpeed * Time.deltaTime);

        // ── Tumble rotation ──
        if (tumbleSpeed > 0f)
            transform.Rotate(tumbleAxis, tumbleSpeed * Time.deltaTime, Space.World);

        float y = transform.position.y;

        // ── Vanish zone: shrink as food approaches ground ──
        if (vanishZone > 0f && y < groundLevel + vanishZone)
        {
            float t = Mathf.Clamp01((y - groundLevel) / vanishZone);
            transform.localScale = baseScale * t;
        }

        // ── Hit ground: deactivate (returns to pool) ──
        if (y <= groundLevel)
        {
            isActive = false;
            gameObject.SetActive(false);
        }
    }

    void OnDisable()
    {
        isActive = false;
    }
}
