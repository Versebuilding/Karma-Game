using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects nearby interactable objects using a trigger SphereCollider.
/// Selects the best target based on angle to the player's forward direction.
///
/// Optimization: Uses manual backward loop for null cleanup (no delegate allocation).
/// Minimum dot threshold prevents targeting objects behind the player.
/// </summary>
public class InteractionDetector : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 4f;

    /// <summary>Minimum dot product to consider a target (prevents targeting behind player).</summary>
    private const float MinDotThreshold = 0.3f; // ~72 degrees from forward

    /// <summary>The current best interactable target, or null.</summary>
    public InteractableBase CurrentTarget { get; private set; }

    /// <summary>UI team subscribes to this for interaction prompt display.</summary>
    public event Action<string> OnPromptChanged;

    /// <summary>UI team subscribes to this when prompt should hide.</summary>
    public event Action OnPromptHidden;

    private List<InteractableBase> inRange = new List<InteractableBase>();
    private PlayerController player;

    void Awake()
    {
        player = GetComponentInParent<PlayerController>();

        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = detectionRadius;

        // Kinematic Rigidbody needed for trigger events to fire.
        // Without it, this trigger collider is "static" and won't detect
        // other static colliders (like NPCs without Rigidbodies).
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void OnTriggerEnter(Collider other)
    {
        var interactable = other.GetComponentInParent<InteractableBase>();
        if (interactable != null && !inRange.Contains(interactable))
        {
            inRange.Add(interactable);
#if UNITY_EDITOR
            Debug.Log($"InteractionDetector: '{interactable.name}' entered range (total: {inRange.Count})");
#endif
        }
    }

    void OnTriggerExit(Collider other)
    {
        var interactable = other.GetComponentInParent<InteractableBase>();
        if (interactable != null)
        {
            inRange.Remove(interactable);
#if UNITY_EDITOR
            Debug.Log($"InteractionDetector: '{interactable.name}' exited range (total: {inRange.Count})");
#endif
        }
    }

    void Start()
    {
        if (player == null)
            Debug.LogWarning("InteractionDetector: No PlayerController found in parent!");

#if UNITY_EDITOR
        var col = GetComponent<SphereCollider>();
        var rb = GetComponent<Rigidbody>();
        Debug.Log($"InteractionDetector: Ready — SphereCollider(trigger={col?.isTrigger}, radius={col?.radius}), Rigidbody(kinematic={rb?.isKinematic}), player={(player != null ? player.name : "NULL")}");
#endif
    }

    void Update()
    {
        // Clean up destroyed objects (allocation-free backward loop)
        for (int i = inRange.Count - 1; i >= 0; i--)
        {
            if (inRange[i] == null)
                inRange.RemoveAt(i);
        }

        // Find best target: closest angle to player forward, above minimum threshold
        InteractableBase best = null;
        float bestScore = MinDotThreshold; // must exceed threshold to be considered

        foreach (var candidate in inRange)
        {
            if (!candidate.CanInteract(player)) continue;
            Vector3 toObj = (candidate.transform.position - player.transform.position).normalized;
            float dot = Vector3.Dot(player.transform.forward, toObj);
            if (dot > bestScore)
            {
                bestScore = dot;
                best = candidate;
            }
        }

        if (best != CurrentTarget)
        {
            CurrentTarget?.OnUntargeted();
            CurrentTarget = best;

            if (CurrentTarget != null)
            {
                CurrentTarget.OnTargeted();
                OnPromptChanged?.Invoke(CurrentTarget.InteractionPrompt);
            }
            else
            {
                OnPromptHidden?.Invoke();
            }
        }
    }
}
