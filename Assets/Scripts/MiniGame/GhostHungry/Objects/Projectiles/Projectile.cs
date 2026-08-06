using UnityEngine;
using UnityEngine.Events;

// FIX: change to interface for decoupling

[RequireComponent(typeof(Rigidbody))]
public abstract class Projectile : MonoBehaviour
{
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

	public void ActivateProjectile(Vector3 velocity) {
		physicsbody.isKinematic = false;
		physicsbody.linearVelocity = velocity;
	}

	public void DeactivateProjectile() {
		physicsbody.linearVelocity = Vector3.zero;
		physicsbody.isKinematic = true;
    }

    protected abstract bool ResetConditionsMet();
}