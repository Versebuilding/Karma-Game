using UnityEngine;
using UnityEngine.Events;

// FIX: change to interface for decoupling

/// <summary>
/// An inheritable physics object which permits interfacing with the <see cref="ThrowManager"/>
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class Projectile : MonoBehaviour
{
	/// <summary>
	/// Request for reset, either destruction or returning to the available object pool
	/// </summary>
    public UnityEvent Reset = new UnityEvent();

	private Rigidbody physicsbody;

    void Awake() {
        physicsbody = GetComponent<Rigidbody>();
    }

    void Update() {
        if (!physicsbody.isKinematic && ResetConditionsMet()) {
            Reset.Invoke();
        }
    }

	/// <summary>
	/// Introduce this <see cref="Projectile"/> instance into the physics simulation with an initial velocity
	/// </summary>
	/// <param name="velocity">The initial velocity (x, y, z) to impose on this <see cref="Projectile"/> object</param>
	public void ActivateProjectile(Vector3 velocity) {
		physicsbody.isKinematic = false;
		physicsbody.linearVelocity = velocity;
	}

	/// <summary>
	/// Remove this <see cref="Projectile"/> instance from the physics simulation making it static
	/// </summary>
	public void DeactivateProjectile() {
		physicsbody.linearVelocity = Vector3.zero;
		physicsbody.isKinematic = true;
    }

	/// <summary>
	/// Check if the conditions are met for this object to request a reset
	/// </summary>
    protected abstract bool ResetConditionsMet();
}