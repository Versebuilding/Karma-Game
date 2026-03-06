using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Cloudy with a Chance of Meatballs" food rain system.
/// Spawns giant 3D food prefabs that fall from the sky, tumble, and vanish at ground level.
///
/// Features:
///   - Object pooling (no runtime Instantiate/Destroy overhead)
///   - Configurable speed, scale, spawn rate, area
///   - Per-item randomization (speed, scale, rotation, tumble)
///   - Public API: StartRain() / StopRain() for level triggers
///   - Auto-loads food prefabs via Inspector array or context menu
///   - Auto-detects model size and adjusts scale if models are tiny
///   - Auto-assigns fallback URP material if renderers have no material
///
/// Setup:
///   1. Create an empty GameObject in the scene, add this script
///   2. Drag food prefabs into the Food Prefabs array
///      (or right-click component → "Auto-Load Food Prefabs From Folder")
///   3. Position the GameObject where you want the rain centered
///   4. Adjust spawn area, speed, scale in Inspector
///   5. Call StopRain() from a trigger/dialogue action when ready
/// </summary>
public class FoodRainManager : MonoBehaviour
{
    // ─── Food Prefabs ──────────────────────────────────────────
    [Header("Food Prefabs")]
    [Tooltip("3D food model prefabs to spawn. Drag from Assets/Prefab/Environment/Food")]
    [SerializeField] private GameObject[] foodPrefabs;

    // ─── Spawn Settings ────────────────────────────────────────
    [Header("Spawn Area")]
    [Tooltip("Radius of the spawn area (XZ plane) around this object")]
    [Range(10f, 100f)]
    [SerializeField] private float spawnRadius = 30f;

    [Tooltip("Height above this object where food spawns")]
    [Range(20f, 150f)]
    [SerializeField] private float spawnHeight = 50f;

    [Tooltip("Food items spawned per second")]
    [Range(0.5f, 20f)]
    [SerializeField] private float spawnRate = 3f;

    // ─── Fall Speed ────────────────────────────────────────────
    [Header("Speed")]
    [Tooltip("Base fall speed (units per second)")]
    [Range(2f, 40f)]
    [SerializeField] private float fallSpeed = 8f;

    [Tooltip("Random speed variation per item (e.g. 0.3 = ±30%)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float speedVariance = 0.25f;

    // ─── Scale ─────────────────────────────────────────────────
    [Header("Scale")]
    [Tooltip("Base scale multiplier for food (giant food!)")]
    [Range(1f, 50f)]
    [SerializeField] private float foodScale = 5f;

    [Tooltip("Random scale variation per item (e.g. 0.3 = ±30%)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float scaleVariance = 0.3f;

    // ─── Ground & Vanish ───────────────────────────────────────
    [Header("Ground")]
    [Tooltip("Y position of the ground plane (food vanishes here)")]
    [SerializeField] private float groundLevel = 0f;

    [Tooltip("Height above ground where food starts shrinking (0 = instant pop)")]
    [Range(0f, 10f)]
    [SerializeField] private float vanishHeight = 3f;

    // ─── Tumble ────────────────────────────────────────────────
    [Header("Tumble")]
    [Tooltip("Rotation speed while falling (degrees per second)")]
    [Range(0f, 360f)]
    [SerializeField] private float tumbleSpeed = 120f;

    // ─── Pool ──────────────────────────────────────────────────
    [Header("Pool")]
    [Tooltip("Initial pool size (auto-expands if needed)")]
    [Range(10, 100)]
    [SerializeField] private int poolSize = 30;

    // ─── State ─────────────────────────────────────────────────
    [Header("State")]
    [Tooltip("Whether food rain starts automatically on scene load")]
    [SerializeField] private bool rainOnStart = true;

    // ─── Runtime ───────────────────────────────────────────────
    private List<FallingFood> pool = new List<FallingFood>();
    private Transform poolParent;
    private Coroutine rainCoroutine;
    private bool isRaining;
    private int spawnDebugCount; // first N spawns get debug logged
    private Material fallbackMaterial; // bright URP material for models with missing materials
    private float autoScaleMultiplier = 1f; // auto-detected scale fix for tiny models

    /// <summary>Whether food is currently raining.</summary>
    public bool IsRaining => isRaining;

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC API — call from triggers, dialogue actions, etc.
    // ═══════════════════════════════════════════════════════════

    /// <summary>Start the food rain.</summary>
    public void StartRain()
    {
        if (isRaining) return;

        if (foodPrefabs == null || foodPrefabs.Length == 0)
        {
            Debug.LogError("FoodRainManager: No food prefabs assigned! " +
                "Use menu 'Karma > Setup Food Rain' or click 'Load Food Prefabs from Folder' in Inspector.");
            return;
        }

        isRaining = true;

        if (rainCoroutine != null)
            StopCoroutine(rainCoroutine);
        rainCoroutine = StartCoroutine(RainCoroutine());

        Debug.Log("FoodRainManager: Rain started!");
    }

    /// <summary>
    /// Stop the food rain. Already-falling food continues to ground.
    /// Call from a trigger zone, dialogue action, or cutscene script.
    /// </summary>
    public void StopRain()
    {
        if (!isRaining) return;

        isRaining = false;

        if (rainCoroutine != null)
        {
            StopCoroutine(rainCoroutine);
            rainCoroutine = null;
        }

        Debug.Log("FoodRainManager: Rain stopped!");
    }

    /// <summary>Stop rain AND immediately remove all falling food.</summary>
    public void StopRainImmediate()
    {
        StopRain();

        // Deactivate all active pool items
        foreach (var food in pool)
        {
            if (food != null && food.gameObject.activeSelf)
                food.gameObject.SetActive(false);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    void Awake()
    {
        // Auto-load food prefabs if array is empty (Editor safety net)
        // This catches cases where the component was added manually without
        // running 'Karma > Setup Food Rain' or clicking 'Load Food Prefabs'.
#if UNITY_EDITOR
        if (foodPrefabs == null || foodPrefabs.Length == 0)
            EditorAutoLoadFoodPrefabs();
#endif

        // Create a parent object to keep Hierarchy clean
        poolParent = new GameObject("FoodRainPool").transform;
        poolParent.SetParent(transform);
        poolParent.localPosition = Vector3.zero;

        // Create fallback material (bright URP Lit) for models with missing materials
        CreateFallbackMaterial();

        // Pre-build the pool
        BuildPool();
    }

    void Start()
    {
        if (rainOnStart)
            StartRain();
    }

    void OnDisable()
    {
        StopRain();
    }

    // ═══════════════════════════════════════════════════════════
    //  SPAWN LOOP
    // ═══════════════════════════════════════════════════════════

    private IEnumerator RainCoroutine()
    {
        // Small random initial delay so multiple managers don't sync
        yield return new WaitForSeconds(Random.Range(0f, 0.5f));

        while (isRaining)
        {
            SpawnFood();

            // Wait based on spawn rate (items per second)
            float interval = 1f / Mathf.Max(spawnRate, 0.1f);
            // Add slight jitter so drops don't feel rhythmic
            float jitter = interval * Random.Range(-0.3f, 0.3f);
            yield return new WaitForSeconds(interval + jitter);
        }

        rainCoroutine = null;
    }

    private void SpawnFood()
    {
        FallingFood food = GetFromPool();
        if (food == null)
        {
            if (spawnDebugCount < 3)
                Debug.LogWarning("FoodRainManager: GetFromPool returned null! Pool may be empty or all items null.");
            return;
        }

        // ── Random spawn position within radius ──
        Vector2 randomXZ = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = transform.position + new Vector3(
            randomXZ.x,
            spawnHeight,
            randomXZ.y
        );

        // Reset local position before setting world position (clears baked-in prefab offsets)
        food.transform.localPosition = Vector3.zero;
        food.transform.position = spawnPos;

        // ── Random scale (giant!) with auto-scale for tiny models ──
        float scale = foodScale * autoScaleMultiplier * Random.Range(1f - scaleVariance, 1f + scaleVariance);
        food.transform.localScale = Vector3.one * scale;

        // ── Random fall speed ──
        float speed = fallSpeed * Random.Range(1f - speedVariance, 1f + speedVariance);

        // ── Activate and launch ──
        food.gameObject.SetActive(true);
        food.Launch(speed, groundLevel, tumbleSpeed, vanishHeight);

        // Diagnostic: log first few spawns with comprehensive info
        if (spawnDebugCount < 5)
        {
            var renderers = food.GetComponentsInChildren<Renderer>(true);
            int enabledRenderers = 0;
            string materialInfo = "none";
            string boundsInfo = "none";
            foreach (var r in renderers)
            {
                if (r.enabled) enabledRenderers++;
                if (r.sharedMaterial != null)
                    materialInfo = $"{r.sharedMaterial.shader.name} ({r.sharedMaterial.name})";
                else
                    materialInfo = "NULL material!";
                boundsInfo = $"center={r.bounds.center} size={r.bounds.size}";
            }
            var meshFilters = food.GetComponentsInChildren<MeshFilter>(true);
            string meshInfo = "none";
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                    meshInfo = $"{mf.sharedMesh.name} (verts={mf.sharedMesh.vertexCount})";
                else
                    meshInfo = "NULL mesh!";
            }
            Debug.Log($"FoodRainManager: Spawned '{food.name}' at {spawnPos} " +
                $"scale={scale:F1} (base={foodScale} auto={autoScaleMultiplier:F1}x) speed={speed:F1} " +
                $"renderers={renderers.Length} (enabled={enabledRenderers}) " +
                $"material=[{materialInfo}] mesh=[{meshInfo}] " +
                $"bounds=[{boundsInfo}] worldScale={food.transform.lossyScale} " +
                $"childCount={food.transform.childCount}");
            spawnDebugCount++;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  OBJECT POOL
    // ═══════════════════════════════════════════════════════════

    private void BuildPool()
    {
        if (foodPrefabs == null || foodPrefabs.Length == 0) return;

        // First pass: create pool items
        for (int i = 0; i < poolSize; i++)
        {
            CreatePoolItem();
        }

        // Health check: measure model bounds and detect issues
        // IMPORTANT: Pool items are deactivated, so Renderer.bounds is unreliable.
        // Use MeshFilter.sharedMesh.bounds which works regardless of active state.
        int nullCount = 0;
        int hasRenderer = 0;
        int hasMaterial = 0;
        int hasMesh = 0;
        float maxMeshSize = 0f;

        foreach (var f in pool)
        {
            if (f == null) { nullCount++; continue; }

            var renderers = f.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                hasRenderer++;
                foreach (var r in renderers)
                {
                    if (r.sharedMaterial != null) hasMaterial++;
                }
            }

            // Use mesh bounds (works even when deactivated)
            var meshFilters = f.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters.Length > 0)
            {
                hasMesh++;
                foreach (var mf in meshFilters)
                {
                    if (mf.sharedMesh != null)
                    {
                        float meshSize = mf.sharedMesh.bounds.size.magnitude;
                        if (meshSize > maxMeshSize) maxMeshSize = meshSize;
                    }
                }
            }
        }

        // Auto-scale detection: if models are very small in their native mesh space,
        // calculate a multiplier to bring them to a visible size.
        // Typical food model should be ~0.2-1.0 units (20cm - 1m) at native scale.
        if (maxMeshSize > 0f && maxMeshSize < 0.1f)
        {
            // Extremely tiny models (< 10cm) — probably millimeter scale
            autoScaleMultiplier = 1.0f / maxMeshSize;
            Debug.LogWarning($"FoodRainManager: Models are extremely tiny (meshBounds={maxMeshSize:F4}). " +
                $"Auto-scale multiplier set to {autoScaleMultiplier:F1}x");
        }
        else if (maxMeshSize > 0f && maxMeshSize < 0.5f)
        {
            // Small models — scale up to ~1 unit
            autoScaleMultiplier = 1.0f / maxMeshSize;
            Debug.Log($"FoodRainManager: Models are small (meshBounds={maxMeshSize:F3}). " +
                $"Auto-scale multiplier set to {autoScaleMultiplier:F1}x");
        }

        Debug.Log($"FoodRainManager: Pool built — {pool.Count}/{poolSize} items, " +
            $"{hasRenderer} with renderers, {hasMaterial} with materials, {hasMesh} with meshes. " +
            $"maxMeshBounds={maxMeshSize:F4}. autoScale={autoScaleMultiplier:F1}x. " +
            $"Manager at {transform.position}, groundLevel={groundLevel}, spawnHeight={spawnHeight}");

        if (hasRenderer == 0)
            Debug.LogError("FoodRainManager: NO RENDERERS found on any pool item! " +
                "Food will be invisible. Check that FBX models have meshes and materials.");

        if (hasMaterial == 0 && hasRenderer > 0)
            Debug.LogWarning("FoodRainManager: Renderers found but NO MATERIALS. " +
                "Fallback materials have been assigned.");
    }

    /// <summary>
    /// Create a bright fallback material for models with missing materials.
    /// Uses URP Lit shader with a bright yellow-orange color so food is always visible.
    /// </summary>
    private void CreateFallbackMaterial()
    {
        // Try URP Lit first, then fallback to Standard
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        if (shader != null)
        {
            fallbackMaterial = new Material(shader);
            fallbackMaterial.name = "FoodFallback";
            fallbackMaterial.color = new Color(1f, 0.8f, 0.2f, 1f); // bright golden yellow
            // Set _BaseColor for URP
            if (fallbackMaterial.HasProperty("_BaseColor"))
                fallbackMaterial.SetColor("_BaseColor", new Color(1f, 0.8f, 0.2f, 1f));
        }
    }

    private FallingFood CreatePoolItem()
    {
        if (foodPrefabs == null || foodPrefabs.Length == 0) return null;

        // Pick a random prefab
        GameObject prefab = foodPrefabs[Random.Range(0, foodPrefabs.Length)];
        if (prefab == null) return null;

        GameObject obj = Instantiate(prefab, poolParent);
        obj.name = prefab.name + "_Pool";

        // Clear baked-in POSITION from the prefab variant
        // (these prefabs were saved from scene instances with world positions baked in)
        // NOTE: Do NOT reset localRotation — the prefab bakes a -90° X rotation that
        // corrects the FBX import orientation. Removing it makes models face wrong way.
        obj.transform.localPosition = Vector3.zero;

        // ── Fix missing materials on all renderers ──
        var renderers = obj.GetComponentsInChildren<Renderer>(true);
        bool hadMissingMaterials = false;

        if (renderers.Length == 0)
        {
            // No renderers at all — try to find MeshFilters and add MeshRenderers
            var meshFilters = obj.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters.Length > 0)
            {
                foreach (var mf in meshFilters)
                {
                    if (mf.GetComponent<MeshRenderer>() == null)
                    {
                        var mr = mf.gameObject.AddComponent<MeshRenderer>();
                        if (fallbackMaterial != null)
                            mr.sharedMaterial = fallbackMaterial;
                    }
                }
                // Re-fetch renderers after adding
                renderers = obj.GetComponentsInChildren<Renderer>(true);
                Debug.Log($"FoodRainManager: Added MeshRenderers to '{obj.name}' — now has {renderers.Length} renderer(s)");
            }
            else
            {
                // No meshes at all — create a primitive cube as placeholder
                Debug.LogWarning($"FoodRainManager: '{obj.name}' has NO meshes or renderers! " +
                    "Adding placeholder cube.");
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(obj.transform, false);
                cube.transform.localScale = Vector3.one * 0.3f;
                // Remove collider from primitive
                var col = cube.GetComponent<Collider>();
                if (col != null) Destroy(col);
                // Apply fallback material
                var cubeRenderer = cube.GetComponent<Renderer>();
                if (cubeRenderer != null && fallbackMaterial != null)
                    cubeRenderer.sharedMaterial = fallbackMaterial;
                renderers = obj.GetComponentsInChildren<Renderer>(true);
            }
        }

        // Fix materials on existing renderers
        foreach (var r in renderers)
        {
            if (r.sharedMaterial == null)
            {
                hadMissingMaterials = true;
                if (fallbackMaterial != null)
                    r.sharedMaterial = fallbackMaterial;
            }
            // Ensure renderer is enabled
            r.enabled = true;
        }

        if (hadMissingMaterials)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"FoodRainManager: '{obj.name}' had missing materials — " +
                "assigned fallback yellow material. To fix permanently: " +
                "select FBX in Project, change Material Import Mode to 'Import via Material Description', " +
                "then Apply.");
#endif
        }

        obj.SetActive(false);

        // Add FallingFood component if not already on the prefab
        FallingFood food = obj.GetComponent<FallingFood>();
        if (food == null)
            food = obj.AddComponent<FallingFood>();

        // Strip physics components — we do our own movement
        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        // Strip colliders (food shouldn't block player/ghosts)
        foreach (var col in obj.GetComponentsInChildren<Collider>())
            Destroy(col);

        pool.Add(food);
        return food;
    }

    private FallingFood GetFromPool()
    {
        // Find an inactive item
        foreach (var food in pool)
        {
            if (food != null && !food.gameObject.activeSelf)
                return food;
        }

        // Pool exhausted — expand
        FallingFood newFood = CreatePoolItem();
        if (newFood != null)
        {
            Debug.Log($"FoodRainManager: Pool expanded to {pool.Count}");
        }
        return newFood;
    }

    // ═══════════════════════════════════════════════════════════
    //  EDITOR AUTO-LOAD
    // ═══════════════════════════════════════════════════════════

#if UNITY_EDITOR
    private const string FoodPrefabFolder = "Assets/Prefab/Environment/Food";

    /// <summary>
    /// Auto-load food prefabs from the known folder when the component
    /// is added in the Inspector or modified. Prevents empty array issues.
    /// </summary>
    void OnValidate()
    {
        if (foodPrefabs == null || foodPrefabs.Length == 0)
            EditorAutoLoadFoodPrefabs();
    }

    private void EditorAutoLoadFoodPrefabs()
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { FoodPrefabFolder });

        if (guids.Length == 0)
        {
            Debug.LogWarning($"FoodRainManager: No prefabs found in '{FoodPrefabFolder}'.");
            return;
        }

        var prefabList = new List<GameObject>();
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                prefabList.Add(prefab);
        }

        foodPrefabs = prefabList.ToArray();
        Debug.Log($"FoodRainManager: Auto-loaded {foodPrefabs.Length} food prefabs from {FoodPrefabFolder}");
    }
#endif

    // ═══════════════════════════════════════════════════════════
    //  GIZMOS
    // ═══════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        // Spawn area (blue circle at spawn height)
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.3f);
        Vector3 spawnCenter = transform.position + Vector3.up * spawnHeight;
        DrawCircleGizmo(spawnCenter, spawnRadius);

        // Ground level (red plane)
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.2f);
        Vector3 groundCenter = new Vector3(transform.position.x, groundLevel, transform.position.z);
        Gizmos.DrawCube(groundCenter, new Vector3(spawnRadius * 2f, 0.05f, spawnRadius * 2f));

        // Vanish zone (yellow band above ground)
        if (vanishHeight > 0f)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.15f);
            Vector3 vanishCenter = groundCenter + Vector3.up * (vanishHeight * 0.5f);
            Gizmos.DrawCube(vanishCenter, new Vector3(spawnRadius * 2f, vanishHeight, spawnRadius * 2f));
        }

        // Vertical lines showing fall path
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.15f);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 edge = transform.position + new Vector3(
                Mathf.Cos(angle) * spawnRadius, 0f, Mathf.Sin(angle) * spawnRadius);
            Gizmos.DrawLine(
                edge + Vector3.up * spawnHeight,
                new Vector3(edge.x, groundLevel, edge.z));
        }
    }

    private void DrawCircleGizmo(Vector3 center, float radius)
    {
        int segments = 32;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
