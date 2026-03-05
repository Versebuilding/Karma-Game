using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager that pools and spawns SparkleVFX instances.
/// Attach to the GameManagers object (auto-added by Karma > Setup Game Systems).
///
/// Usage:
///   SparkleVFXManager.Instance.PlayAtPlayer();        // sparkle at Sammy
///   SparkleVFXManager.Instance.PlayAt(worldPos);      // sparkle at position
///   SparkleVFXManager.Instance.PlayAtTransform(npc);  // sparkle at NPC
/// </summary>
public class SparkleVFXManager : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────
    public static SparkleVFXManager Instance { get; private set; }

    // ─── Settings ────────────────────────────────────────────
    [Header("Prefab")]
    [Tooltip("SparkleVFX prefab (created via Karma > Create Sparkle VFX Prefab)")]
    [SerializeField] private GameObject sparklePrefab;

    [Header("Pool")]
    [Tooltip("Number of pre-instantiated sparkle instances")]
    [Range(1, 20)]
    [SerializeField] private int poolSize = 5;

    [Header("Positioning")]
    [Tooltip("Vertical offset when spawning at a transform")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("Tag used to find the player")]
    [SerializeField] private string playerTag = "Player";

    // ─── Runtime ─────────────────────────────────────────────
    private List<SparkleVFX> pool;
    private Transform playerTransform;

    // ─── Unity Lifecycle ─────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        InitializePool();
        FindPlayer();
    }

    // ─── Public API ──────────────────────────────────────────

    /// <summary>Play a sparkle burst at the given world position.</summary>
    public void PlayAt(Vector3 position)
    {
        SparkleVFX sparkle = GetFromPool();
        if (sparkle != null)
            sparkle.Play(position);
    }

    /// <summary>Play a sparkle burst at a transform's position (+ spawnOffset).</summary>
    public void PlayAtTransform(Transform target)
    {
        if (target == null) return;
        PlayAt(target.position + spawnOffset);
    }

    /// <summary>Play a sparkle burst at the player's position.</summary>
    public void PlayAtPlayer()
    {
        if (playerTransform == null)
            FindPlayer();

        if (playerTransform != null)
            PlayAt(playerTransform.position + spawnOffset);
    }

    /// <summary>Assign the sparkle prefab at runtime (used by Editor tools).</summary>
    public void SetPrefab(GameObject prefab)
    {
        sparklePrefab = prefab;
    }

    // ─── Pool ────────────────────────────────────────────────

    private void InitializePool()
    {
        pool = new List<SparkleVFX>(poolSize);

        if (sparklePrefab == null)
        {
            Debug.LogWarning("SparkleVFXManager: No sparklePrefab assigned! Use Karma > Create Sparkle VFX Prefab.");
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(sparklePrefab, transform);
            obj.name = $"SparkleVFX_Pool_{i}";
            obj.SetActive(false);

            SparkleVFX sparkle = obj.GetComponent<SparkleVFX>();
            if (sparkle != null)
                pool.Add(sparkle);
        }
    }

    private SparkleVFX GetFromPool()
    {
        if (pool == null) return null;

        // Find first inactive instance
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && !pool[i].gameObject.activeInHierarchy)
                return pool[i];
        }

        // All busy — expand pool by one
        if (sparklePrefab != null)
        {
            GameObject obj = Instantiate(sparklePrefab, transform);
            obj.name = $"SparkleVFX_Pool_{pool.Count}";
            obj.SetActive(false);

            SparkleVFX sparkle = obj.GetComponent<SparkleVFX>();
            if (sparkle != null)
            {
                pool.Add(sparkle);
                return sparkle;
            }
        }

        return null;
    }

    private void FindPlayer()
    {
        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject playerObj = GameObject.FindWithTag(playerTag);
            if (playerObj != null)
                playerTransform = playerObj.transform;
        }
    }
}
