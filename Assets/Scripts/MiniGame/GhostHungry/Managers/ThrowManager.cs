using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/* Implement
- Charge Locking
- Don't like how aiming feels - needs refactoring
*/

// FIX: add key movement capabilities for accessibility

/// <summary>
/// A centralized system for stationary object throwing which interfaces with the physics simulation. It manages the
/// lifecycle for a set of identical <see cref="Projectile"/> instances, the objects which are thrown. The system
/// utilizes object pooling to reduce the overhead of constant instantiation and destruction of objects. 
/// </summary>
public class ThrowManager : MonoBehaviour
{
	/// <summary>
	/// Indicates that a <see cref="Projectile"/> instance has be thrown and is now part of the physics simulation
	/// </summary>
	public UnityEvent ThrowRegistered;

    [Header("References")]
    [Tooltip("The object, of type Projectile, which is instantiated for throwing")]
    [SerializeField] private Projectile sourceObject;
    [Tooltip("The location in the hierarchy where instances of sourceObject will be parented to")]
    [SerializeField] private Transform storageFolder;
    [Tooltip("The location where the thrown sourceObject will originate from")]
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private PlayerInputHandler inputHandler;

    [Header("Projectile")]
    [Tooltip("Number of pre-instantiated instances of sourceObject")]
    [SerializeField][Range(1, 32)] private int objectCount = 8;
    [Tooltip("The range from x (min) to y (max) for the throw force dependent on charge")]
    [SerializeField] private Vector2 throwForceRange = new(5, 15); // (Min (x), Max (y))

    [Header("Timers")]
    [Tooltip("How long it takes for the user to fully charge a single throw")]
    [SerializeField][Min(0.01f)] private float chargeDuration = 1.25f;
    [Tooltip("How long it takes before the user can throw another sourceObject")]
    [SerializeField][Min(0f)] private float throwCooldown = 2f; // FIX: needs to match animation

    // Projectile Properties:
    private Projectile[] base_projectiles;
	private List<Projectile> auxiliary_projectiles = new();
	private int activeFlag = 1;
    private int nextIndex = 0;
	private int num_aux_elements = 0;

	// Action Timers:
	private float chargeTimer;
    private float cooldownTimer;

	void Awake() {
		if (ThrowRegistered == null) ThrowRegistered = new();
		if (!storageFolder) {
			storageFolder = transform;
		}

		chargeTimer = chargeDuration;
        base_projectiles = new Projectile[objectCount];
	}

    void Start() {
        Cursor.lockState = CursorLockMode.Confined; // FIX: Move to Minigame Manager
        Cursor.visible = false; // FIX: Move to Minigame Manager

        for (int i = 0; i < base_projectiles.Length; i++) {
            base_projectiles[i] = CreateObject(i);

            base_projectiles[i].transform.position = new Vector3(i * 2, 5, -15);// FIX: remove magic numbers/functionalize
        }

        MoveObjectToReload();

        InputDEP();// FIX: release needs to be added to InputHandler - remove
    }

    void Update() {
        /* Control Structure: only one can occur per tick
        1. On Cooldown? -> tick cooldown
        2. Throw Registered? -> throw > signal such > reset state > create reload wait
        3. Charging? -> tick & clamp charge
        */

        if (cooldownTimer > 0) {
            cooldownTimer -= Time.deltaTime;
        }
        else if (action.WasReleasedThisFrame()) {// FIX: release needs to be added to InputHandler - replace 'inputHandler.InteractRelease'
            Throw();

            ThrowRegistered.Invoke();

            chargeTimer = chargeDuration;
            cooldownTimer = throwCooldown;

            Invoke(nameof(Reload), cooldownTimer);
        }
        else if (inputHandler.InteractHeld) {// FIX: InteractHeld stays during frame of release - unknown why
            chargeTimer = Mathf.Max(chargeTimer - Time.deltaTime, 0);
        }
    }

    private Projectile CreateObject(int reset_index) {
		Projectile projectile = Instantiate(sourceObject, storageFolder);

		projectile.DeactivateProjectile();
		projectile.Reset.AddListener(() => ResetThrowObject(reset_index));

		return projectile;
    }

    private void Throw() {
        Projectile projectile;

        if (nextIndex >= objectCount) {
			projectile = auxiliary_projectiles[nextIndex - objectCount];
		}
        else {
            projectile = base_projectiles[nextIndex];
        }

        projectile.ActivateProjectile(GetThrowVelocity());
    }

    /// <returns>
    /// An object's initial velocity for its thrown trajectory, based upon the current charge and mouse position
    /// </returns>
    public Vector3 GetThrowVelocity() {
        return (GetThrowDirection() + Vector3.up) * Mathf.Lerp(throwForceRange.y, throwForceRange.x, chargeTimer / chargeDuration);
    }

    private Vector3 GetThrowDirection() { // FIX: update to new Input system upon approval
        Vector3 direction = new Vector3(Mathf.Lerp(-10, 10, Input.mousePosition.x / Camera.main.pixelWidth), 0, 0) - throwOrigin.position; // FIX: convert to dynamic positioning
        // / : mouse's horizontal position on screen as a factor
        // Lerp : mouse's factor converted into 3D space location (target)
        // - : 3D direction from origin to target

        direction.y = 0; // Project xyz vector onto xz plane

        return direction.normalized;
    }

    private void Reload() {
        // Fail State: All objects active, create a temporary object
        if (activeFlag == ((1 << objectCount) - 1)) { // FIX: try to optimize
			nextIndex = objectCount + auxiliary_projectiles.Count;

			for (int i = 0; i < auxiliary_projectiles.Count; i++) {
				if (!auxiliary_projectiles[i]) {
					nextIndex = objectCount + i;

					auxiliary_projectiles[i] = CreateObject(nextIndex);
                    num_aux_elements++;

					MoveObjectToReload();

					return;
				}
			}

			auxiliary_projectiles.Add(CreateObject(nextIndex));
			num_aux_elements++;

			MoveObjectToReload();

			return;
        }

        // Get the next viable non-active index
        for (int tries = 1; tries < objectCount; tries++) {
            int i = (nextIndex + tries) % objectCount;

            if (((activeFlag >> i) & 1) == 0) {
                activeFlag |= 1 << i;
                nextIndex = i;

                MoveObjectToReload();

                break;
            }
        }
    }

    private void MoveObjectToReload() {
        if (nextIndex >= objectCount) {
			auxiliary_projectiles[nextIndex - objectCount].transform.position = throwOrigin.position;

            return;
        }
        
        base_projectiles[nextIndex].transform.position = throwOrigin.position;
    }

	/// <summary>
	/// Reintroduce the <see cref="Projectile"/> object at <paramref name="index"/> into the available object pool or destroy it if its a temp object
	/// </summary>
	/// <param name="index">The referencing index for the object, which child of <see cref="storageFolder"/> is being reset</param>
	public void ResetThrowObject(int index) {
        // Destroy temporary objects
        if (index >= objectCount) {
			index -= objectCount;

			Destroy(auxiliary_projectiles[index].gameObject);
            auxiliary_projectiles[index] = null;

			if (--num_aux_elements == 0) {
				auxiliary_projectiles.Clear();
			}

			return;
        }

        // Reset permanent objects
        Projectile projectile = base_projectiles[index];

        projectile.DeactivateProjectile();
        projectile.transform.position = new Vector3(index * 2, 5, -15);// FIX: remove magic numbers/functionalize

        activeFlag ^= 1 << index;
    }

    private void OnValidate() {
        // Validate throw force range is viable
        if (throwForceRange.x < 0) {
            throwForceRange.x = 0;
        }

        if (throwForceRange.y < 0.01f) {
            throwForceRange.y = 0.01f;
        }

        if (throwForceRange.y < throwForceRange.x) {
            throwForceRange.y = throwForceRange.x;
        }

        if (throwOrigin == null) {
            throwOrigin = transform;
        }

        // Validate references are set
        if (sourceObject == null || inputHandler == null) {
            Debug.LogError("One or more required references in the " + name + " object are null...");
        }

        if (storageFolder == null) {
            Debug.LogWarning("The storageFolder reference is null, instantiation will occur under the manager itself...");
        }
    }

    // DEP: Removeable once the denoted (FIX) tag is rectified
    
    // FIX: release needs to be added to InputHandler
    [Header("DEP")]
    [SerializeField] private InputActionAsset inputActions;
    InputAction action;
    private void InputDEP() {
        action = inputActions.FindActionMap("Player", true).FindAction("Interact", true);
    }
}