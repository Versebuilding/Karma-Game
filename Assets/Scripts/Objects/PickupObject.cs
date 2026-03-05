using System.Collections;
using UnityEngine;

/// <summary>
/// Objects that can be picked up, carried, thrown, dropped, and stacked.
/// Requires Rigidbody + Collider on the GameObject.
///
/// Elegance features:
///   - Smooth lerp to carryPoint (0.2s ease-out) instead of instant snap
///   - Optional audio clips for pickup, drop, and throw
///   - Weight affects throw distance (heavier = shorter throw, via CarryState)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PickupObject : InteractableBase
{
    [Header("Pickup Settings")]
    public float weight = 1f;
    public bool isStackable = false;
    public Vector3 stackOffset = Vector3.up;

    [Header("Audio (optional)")]
    [Tooltip("Sound when picked up")]
    [SerializeField] private AudioClip pickupSound;

    [Tooltip("Sound when dropped")]
    [SerializeField] private AudioClip dropSound;

    [Tooltip("Sound when thrown")]
    [SerializeField] private AudioClip throwSound;

    private Rigidbody rb;
    private Collider col;

    // Smooth pickup lerp
    private const float PickupLerpDuration = 0.2f;
    private Coroutine pickupLerpCoroutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        prompt = "Pick Up";
    }

    public override bool CanInteract(PlayerController player)
    {
        // Can't pick up if already carrying something
        return player.carriedObject == null;
    }

    public override void Interact(PlayerController player)
    {
        PickUp(player);
    }

    public void PickUp(PlayerController player)
    {
        if (player.carryPoint == null)
        {
            Debug.LogWarning("PickupObject: No carryPoint assigned on PlayerController.");
            return;
        }

        // Disable physics and parent to carry point
        rb.isKinematic = true;
        col.enabled = false;
        transform.SetParent(player.carryPoint);

        // Smooth lerp to carry position instead of instant snap
        if (pickupLerpCoroutine != null) StopCoroutine(pickupLerpCoroutine);
        pickupLerpCoroutine = StartCoroutine(LerpToCarryPoint());

        player.carriedObject = gameObject;
        player.stateMachine.SetState<CarryState>();

        PlaySound(pickupSound);
    }

    public void Drop(Vector3 position)
    {
        CancelLerp();
        transform.SetParent(null);
        transform.position = position;
        rb.isKinematic = false;
        col.enabled = true;
        PlaySound(dropSound);
    }

    public void Throw(Vector3 force)
    {
        CancelLerp();
        transform.SetParent(null);
        rb.isKinematic = false;
        col.enabled = true;
        rb.AddForce(force, ForceMode.Impulse);
        PlaySound(throwSound);
    }

    /// <summary>
    /// Stack this object on top of another PickupObject.
    /// </summary>
    public void StackOn(PickupObject other)
    {
        Drop(other.transform.position + stackOffset);
    }

    // ─── Smooth Pickup Lerp ─────────────────────────────────────

    /// <summary>Smoothly lerp to carry position with ease-out curve for dreamy feel.</summary>
    private IEnumerator LerpToCarryPoint()
    {
        Vector3 startLocal = transform.localPosition;
        Quaternion startRot = transform.localRotation;
        float elapsed = 0f;

        while (elapsed < PickupLerpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / PickupLerpDuration);
            // Ease-out: fast start, smooth settle
            t = 1f - (1f - t) * (1f - t);

            transform.localPosition = Vector3.Lerp(startLocal, Vector3.zero, t);
            transform.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, t);
            yield return null;
        }

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        pickupLerpCoroutine = null;
    }

    private void CancelLerp()
    {
        if (pickupLerpCoroutine != null)
        {
            StopCoroutine(pickupLerpCoroutine);
            pickupLerpCoroutine = null;
        }
    }

    // ─── Audio ──────────────────────────────────────────────────

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        // PlayClipAtPoint: no AudioSource component needed on every pickup object
        AudioSource.PlayClipAtPoint(clip, transform.position);
    }
}
