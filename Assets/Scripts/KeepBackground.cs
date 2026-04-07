using UnityEngine;

public class KeepBackground : MonoBehaviour
{
    private static KeepBackground Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        else
        {
            Destroy(gameObject);
        }
    }
}
