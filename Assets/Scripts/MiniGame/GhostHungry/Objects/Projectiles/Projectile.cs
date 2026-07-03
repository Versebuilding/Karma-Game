using UnityEngine;
using UnityEngine.Events;

// FIX: change to interface for decoupling

[RequireComponent(typeof(Rigidbody))]
public abstract class Projectile : MonoBehaviour
{
    public UnityEvent Reset = new UnityEvent();

    public Rigidbody Physicsbody { get; private set; }

    void Awake() {
        Physicsbody = GetComponent<Rigidbody>();
    }

    void Update() {
        if (!Physicsbody.isKinematic && ResetConditionsMet()) {
            Reset.Invoke();
        }
    }

    protected abstract bool ResetConditionsMet();
}
