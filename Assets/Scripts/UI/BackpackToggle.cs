using UnityEngine;

/// <summary>
/// Open/close control for the backpack panel. The panel starts CLOSED and
/// toggles with the B key (and Escape closes it). Lives on the Backpack
/// root and toggles its children, so this component keeps running while
/// the panel is hidden.
/// </summary>
public class BackpackToggle : MonoBehaviour
{
    [Tooltip("Key that opens/closes the backpack")]
    [SerializeField] private KeyCode toggleKey = KeyCode.B;

    public bool IsOpen { get; private set; }

    private void Start()
    {
        SetOpen(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetOpen(!IsOpen);
        else if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        foreach (Transform child in transform)
            child.gameObject.SetActive(open);
    }
}
