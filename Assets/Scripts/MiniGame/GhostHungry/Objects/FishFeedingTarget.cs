using UnityEngine;

/// <summary>
/// An extender for a <see cref="Collider"/> component which formats it as a viable target for feeding fish instances
/// </summary>
[RequireComponent(typeof(Collider))]
public class FishFeedingTarget : FeedingTarget
{
    // Variables:
    [Header("Detection")]
    [Tooltip("When true, this target only accepts objects that have a BreadProjectile component")]
    [SerializeField] private bool requireProjectileComponent = true;

    [Header("References")]
    [Tooltip("The fish behavior system which will utilize this target")]
    [SerializeField] private FishBehavior fishBehavior;
    [Tooltip("The collider utilized to give this object shape")]
    [SerializeField] private Collider trigger;

    // Unity Processing:
    void OnEnable() {
        // only enable if all components are correctly set
        if (fishBehavior == null || trigger == null || !trigger.isTrigger) {
            enabled = false;
        }
    }

    void OnTriggerEnter(Collider other) {
        if (!CanBeFed() || (requireProjectileComponent && other.GetComponent<BreadProjectile>() == null)) return; // FIX: remove runtime component getting

        if (TryFeed(other.transform, out _) == FeedAttemptResult.Success) {
            ResetOrDestroyBread(other.gameObject);
        }
    }

    // Projectile Cleanup:
    private void ResetOrDestroyBread(GameObject bread) {
        // FIX: remove runtime component getting
        if (bread.TryGetComponent(out Projectile projectile)) {
            projectile.Reset.Invoke();

            return;
        }

        Destroy(bread); // fallback - if not pooled, mark for deletion at end-of-frame
    }

    // Feeding Behaviors:
    protected override bool CanBeFedInternal() {
        return fishBehavior.IsFeedable;
    }

    protected override void OnFed(Component feedSource) {
        fishBehavior.Feed();
    }

#if UNITY_EDITOR
    // Editor-time Property Validation:
    protected override void OnValidate() {
        base.OnValidate();

        if (fishBehavior == null) {
            Debug.LogError($"{GetType()} '{name}': '{nameof(fishBehavior)}' is null...");
        }

        if (trigger == null) {
            Debug.LogError($"{GetType()} '{name}': '{nameof(trigger)}' is null...");
        }
        else if (trigger.isTrigger == false) {
            Debug.LogError($"{GetType()} '{name}': The attached '{nameof(trigger)}' collider should be set as a trigger but isn't...");
        }
    }
#endif
}

/* Programmer's Notes:
- While the script has been designed to work with either the BreadProjectile or Throw systems, one system should be decided on and the
other removed for complexity sake
- Both the FeedingTarget object and BreadProjectile system (if decided) needs to be updated to utilize the strategies outlined in the
best practices documentation so this file can do the same
*/