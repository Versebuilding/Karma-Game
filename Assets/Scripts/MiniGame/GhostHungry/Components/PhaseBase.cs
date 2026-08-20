using UnityEngine;

/// <summary>
/// Base ScriptableObject for per-phase custom logic.
/// Override methods to implement phase behaviour (optional).
/// Kept intentionally minimal so it compiles and is editor-friendly.
/// </summary>
public abstract class PhaseBase : MonoBehaviour {
    /// <summary>Called once when the phase begins.</summary>
    public virtual void OnEnter() { }

    /// <summary>Called every frame while the phase is active. dt is Time.deltaTime.</summary>
    public virtual void OnUpdate(float dt) { }

    /// <summary>Called once when the phase ends.</summary>
    public virtual void OnExit() { }

    /// <summary>Optional: request immediate transition to next phase.</summary>
    public virtual bool RequestImmediateTransition() => false;
}