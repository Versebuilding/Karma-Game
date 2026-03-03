using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects nearby interactable objects using a trigger SphereCollider.
/// Selects the best target based on angle to the player's forward direction.
/// </summary>
public class InteractionDetector : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 4f;

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
    }

    void OnTriggerEnter(Collider other)
    {
        var interactable = other.GetComponent<InteractableBase>();
        if (interactable != null && !inRange.Contains(interactable))
            inRange.Add(interactable);
    }

    void OnTriggerExit(Collider other)
    {
        var interactable = other.GetComponent<InteractableBase>();
        if (interactable != null)
            inRange.Remove(interactable);
    }

    void Update()
    {
        // Clean up destroyed objects
        inRange.RemoveAll(item => item == null);

        // Find best target: closest angle to player forward
        InteractableBase best = null;
        float bestScore = -1f;

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
