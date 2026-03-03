using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Generic trigger zone that fires events when the player enters/exits.
/// Can optionally be single-use (disables after first trigger).
/// </summary>
public class TriggerZone : MonoBehaviour
{
    [Header("Settings")]
    public bool singleUse = false;
    public bool playerOnly = true;

    [Header("Events")]
    public UnityEvent OnPlayerEnter;
    public UnityEvent OnPlayerExit;

    private bool hasTriggered;

    void OnTriggerEnter(Collider other)
    {
        if (singleUse && hasTriggered) return;

        if (playerOnly && other.GetComponent<PlayerController>() == null) return;

        hasTriggered = true;
        OnPlayerEnter?.Invoke();
    }

    void OnTriggerExit(Collider other)
    {
        if (playerOnly && other.GetComponent<PlayerController>() == null) return;

        OnPlayerExit?.Invoke();
    }
}
