using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Toggleable lever that fires events on/off.
/// Player interacts with E to toggle.
/// </summary>
public class Lever : InteractableBase
{
    [Header("Events")]
    public UnityEvent OnToggleOn;
    public UnityEvent OnToggleOff;

    private bool isOn;

    void Awake()
    {
        prompt = "Pull Lever";
    }

    public override void Interact(PlayerController player)
    {
        isOn = !isOn;

        if (isOn)
            OnToggleOn?.Invoke();
        else
            OnToggleOff?.Invoke();
    }
}
