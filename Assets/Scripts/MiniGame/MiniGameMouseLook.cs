using UnityEngine;

public class MiniGameMouseLook : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform yawRoot;
    [SerializeField] private Transform pitchPivot;
    [SerializeField] private PlayerInputHandler playerInputHandler;

    [Header("Input")]
    [SerializeField] private bool usePlayerInputHandler = true;
    [SerializeField] private bool useLegacyMouseFallback = true;

    [Header("Sensitivity")]
    [SerializeField] [Min(0f)] private float inputSystemSensitivity = 0.08f;
    [SerializeField] [Min(0f)] private float legacyMouseSensitivity = 3f;
    [SerializeField] private bool invertY = false;

    [Header("Pitch Limits")]
    [SerializeField] private float minPitch = -70f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = false;
    [SerializeField] private bool hideCursorWhenLocked = true;
    [SerializeField] private bool unlockCursorOnEscape = true;
    [SerializeField] private bool relockCursorOnLeftClick = true;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        if (yawRoot == null)
        {
            yawRoot = transform;
        }

        if (pitchPivot == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
            {
                pitchPivot = childCamera.transform;
            }
        }

        if (playerInputHandler == null)
        {
            playerInputHandler = GetComponent<PlayerInputHandler>();
        }

        yaw = yawRoot != null ? yawRoot.eulerAngles.y : transform.eulerAngles.y;
        pitch = pitchPivot != null ? NormalizeAngle(pitchPivot.localEulerAngles.x) : 0f;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            SetCursorLocked(true);
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SetCursorLocked(false);
    }

    private void Update()
    {
        HandleCursorState();

        if (pitchPivot == null || yawRoot == null)
        {
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 lookDelta = ReadLookDelta();
        if (lookDelta.sqrMagnitude < 0.000001f)
        {
            return;
        }

        yaw += lookDelta.x;
        pitch += invertY ? lookDelta.y : -lookDelta.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        yawRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
        pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleCursorState()
    {
        if (unlockCursorOnEscape && Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLocked(false);
        }

        if (!relockCursorOnLeftClick || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            SetCursorLocked(true);
        }
    }

    private Vector2 ReadLookDelta()
    {
        if (usePlayerInputHandler && playerInputHandler != null)
        {
            return playerInputHandler.LookInput * inputSystemSensitivity;
        }

        if (!useLegacyMouseFallback)
        {
            return Vector2.zero;
        }

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        return new Vector2(mouseX, mouseY) * legacyMouseSensitivity;
    }

    private void SetCursorLocked(bool shouldLock)
    {
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = shouldLock ? !hideCursorWhenLocked : true;
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    private void OnValidate()
    {
        if (yawRoot == null)
        {
            yawRoot = transform;
        }

        if (maxPitch < minPitch)
        {
            maxPitch = minPitch;
        }
    }
}
