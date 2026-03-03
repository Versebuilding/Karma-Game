using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pressure plate that activates when enough weight is on it.
/// Responds to both the player and PickupObjects.
/// </summary>
public class PressurePlate : MonoBehaviour
{
    [Header("Settings")]
    public float requiredWeight = 1f;
    public float playerWeight = 5f;

    [Header("Events")]
    public UnityEvent OnActivated;
    public UnityEvent OnDeactivated;

    private float currentWeight;
    private bool isActive;

    void OnTriggerEnter(Collider other)
    {
        var pickup = other.GetComponent<PickupObject>();
        if (pickup != null)
            currentWeight += pickup.weight;

        if (other.GetComponent<PlayerController>() != null)
            currentWeight += playerWeight;

        CheckActivation();
    }

    void OnTriggerExit(Collider other)
    {
        var pickup = other.GetComponent<PickupObject>();
        if (pickup != null)
            currentWeight -= pickup.weight;

        if (other.GetComponent<PlayerController>() != null)
            currentWeight -= playerWeight;

        currentWeight = Mathf.Max(0f, currentWeight);
        CheckActivation();
    }

    private void CheckActivation()
    {
        if (currentWeight >= requiredWeight && !isActive)
        {
            isActive = true;
            OnActivated?.Invoke();
        }
        else if (currentWeight < requiredWeight && isActive)
        {
            isActive = false;
            OnDeactivated?.Invoke();
        }
    }
}
