using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform player;
    public float smoothSpeed = 8f;

    void LateUpdate()
    {
        if (!player) return;

        // Match player rotation smoothly
        Quaternion targetRotation = Quaternion.Euler(
            0,
            player.eulerAngles.y,
            0
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            smoothSpeed * Time.deltaTime
        );

        // Follow player position
        transform.position = player.position;
    }

    
}
