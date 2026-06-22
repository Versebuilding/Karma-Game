using System;
using UnityEngine;

public enum BirdRoundPattern
{
    DiveFeed = 0,
    CircleAndSnatch = 1,
    SwarmPanic = 2,
    AggressiveHunger = 3,
}

public class BirdPatternGroupController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FeedingMiniGameManager miniGameManager;
    [SerializeField] private BirdFeedingTarget[] birdTargets;

    [Header("Common")]
    [SerializeField] private BirdRoundPattern currentPattern = BirdRoundPattern.DiveFeed;
    [SerializeField] private bool rotateWithMovement = true;
    [SerializeField] [Min(0f)] private float rotationLerpSpeed = 10f;

    [Header("Dive Feed")]
    [SerializeField] [Min(0f)] private float diveSweepDistance = 1.4f;
    [SerializeField] [Min(0f)] private float diveDepth = 1.8f;
    [SerializeField] [Min(0.05f)] private float diveDuration = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float diveFeedWindowThreshold = 0.82f;

    [Header("Circle & Snatch")]
    [SerializeField] [Min(0f)] private float circleRadius = 1.8f;
    [SerializeField] [Min(0.05f)] private float circleDuration = 3.2f;
    [SerializeField] [Min(0f)] private float circleBobAmplitude = 0.2f;
    [SerializeField] [Min(0f)] private float circleBobFrequency = 1.2f;
    [SerializeField] [Min(0.05f)] private float snatchInterval = 2.4f;
    [SerializeField] [Min(0.05f)] private float snatchDuration = 0.55f;
    [SerializeField] [Min(0f)] private float snatchDistance = 1.2f;
    [SerializeField] [Min(0.01f)] private float snatchFeedWindowDuration = 0.28f;

    [Header("Swarm Panic")]
    [SerializeField] [Min(0f)] private float swarmOrbitRadius = 1.3f;
    [SerializeField] [Min(0.05f)] private float swarmOrbitDuration = 2.5f;
    [SerializeField] [Min(0f)] private float swarmBobAmplitude = 0.2f;
    [SerializeField] [Min(0f)] private float swarmBobFrequency = 1.5f;
    [SerializeField] [Min(0.05f)] private float swarmDartInterval = 1.8f;
    [SerializeField] [Min(0.05f)] private float swarmDartDuration = 0.45f;
    [SerializeField] [Min(0f)] private float swarmDartDistance = 1f;
    [SerializeField] [Min(0.01f)] private float swarmFeedWindowDuration = 0.22f;
    [SerializeField] [Min(0f)] private float swarmPanicScatterDistance = 2.6f;
    [SerializeField] [Min(0.05f)] private float swarmPanicDuration = 1.6f;
    [SerializeField] [Min(0.05f)] private float swarmCollisionDistance = 0.9f;
    [SerializeField] [Min(0f)] private float swarmCollisionCooldown = 0.35f;
    [SerializeField] [Min(0f)] private float swarmFeedPanicRadius = 4f;

    [Header("Aggressive Hunger")]
    [SerializeField] [Min(0f)] private float patrolDistance = 3.2f;
    [SerializeField] [Min(0.05f)] private float patrolDuration = 3.1f;
    [SerializeField] [Min(0f)] private float patrolHoverAmplitude = 0.35f;
    [SerializeField] [Min(0f)] private float patrolHoverFrequency = 1.05f;
    [SerializeField] [Min(0f)] private float returnSpeed = 8f;
    [SerializeField] [Min(0f)] private float detectionRadius = 12f;
    [SerializeField] [Min(0f)] private float interceptSpeed = 13f;
    [SerializeField] [Min(0f)] private float interceptLeadTime = 0.15f;
    [SerializeField] [Min(0.05f)] private float maxInterceptDuration = 1.35f;
    [SerializeField] [Min(0f)] private float interceptCooldown = 1.4f;

    private Transform[] birdTransforms = Array.Empty<Transform>();
    private Vector3[] anchorLocalPositions = Array.Empty<Vector3>();
    private Vector3[] lastWorldPositions = Array.Empty<Vector3>();
    private bool[] feedWindowStates = Array.Empty<bool>();
    private float[] panicTimers = Array.Empty<float>();
    private Vector3[] panicDirections = Array.Empty<Vector3>();
    private float[] collisionCooldownTimers = Array.Empty<float>();
    private BreadProjectile[] trackedProjectiles = Array.Empty<BreadProjectile>();
    private float[] interceptTimers = Array.Empty<float>();
    private float[] interceptCooldownTimers = Array.Empty<float>();
    private bool[] fedStates = Array.Empty<bool>();

    public BirdRoundPattern CurrentPattern => currentPattern;
    public int BirdCount => birdTargets != null ? birdTargets.Length : 0;

    private void Awake()
    {
        ResolveReferences();
        DisableLegacyMovementScripts();
        CacheBirdData();
        ResetPatternState();
    }

    private void Update()
    {
        if (birdTransforms.Length == 0)
        {
            return;
        }

        UpdateTimers();

        switch (currentPattern)
        {
            case BirdRoundPattern.DiveFeed:
                UpdateDivePattern();
                break;
            case BirdRoundPattern.CircleAndSnatch:
                UpdateCirclePattern();
                break;
            case BirdRoundPattern.SwarmPanic:
                UpdateSwarmPattern();
                break;
            case BirdRoundPattern.AggressiveHunger:
                UpdateAggressivePattern();
                break;
        }

        UpdateBirdRotations();
    }

    public void SetPattern(BirdRoundPattern pattern)
    {
        currentPattern = pattern;
        ResetPatternState();
    }

    public void ResetPatternState()
    {
        ResolveReferences();
        CacheBirdData();

        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (birdTransforms[i] == null)
            {
                continue;
            }

            if (birdTargets[i] != null && !birdTargets[i].gameObject.activeSelf)
            {
                birdTargets[i].gameObject.SetActive(true);
            }

            fedStates[i] = false;
            birdTransforms[i].localPosition = anchorLocalPositions[i];
            feedWindowStates[i] = false;
            panicTimers[i] = 0f;
            panicDirections[i] = Vector3.zero;
            collisionCooldownTimers[i] = 0f;
            trackedProjectiles[i] = null;
            interceptTimers[i] = 0f;
            interceptCooldownTimers[i] = 0f;
            lastWorldPositions[i] = birdTransforms[i].position;
        }
    }

    public bool IsBirdFeedWindowOpen(BirdFeedingTarget target)
    {
        int birdIndex = GetBirdIndex(target);
        if (birdIndex < 0 || birdIndex >= feedWindowStates.Length)
        {
            return false;
        }

        return feedWindowStates[birdIndex];
    }

    public void NotifyBirdFed(BirdFeedingTarget target, Component feedSource)
    {
        int birdIndex = GetBirdIndex(target);
        if (birdIndex < 0)
        {
            return;
        }

        Vector3 feedPosition = birdTransforms[birdIndex] != null
            ? birdTransforms[birdIndex].position
            : transform.position;

        fedStates[birdIndex] = true;
        feedWindowStates[birdIndex] = false;
        panicTimers[birdIndex] = 0f;
        collisionCooldownTimers[birdIndex] = 0f;

        switch (currentPattern)
        {
            case BirdRoundPattern.SwarmPanic:
                TriggerNearbyPanic(feedPosition, swarmFeedPanicRadius, birdIndex);
                break;
            case BirdRoundPattern.AggressiveHunger:
                trackedProjectiles[birdIndex] = null;
                interceptTimers[birdIndex] = 0f;
                interceptCooldownTimers[birdIndex] = interceptCooldown;
                break;
        }

        if (birdTargets[birdIndex] != null)
        {
            birdTargets[birdIndex].gameObject.SetActive(false);
        }
    }

    public void NotifyBirdMissed(BirdFeedingTarget target, Component feedSource, FeedFailureReason failureReason)
    {
        int birdIndex = GetBirdIndex(target);
        if (birdIndex < 0)
        {
            return;
        }

        if (currentPattern == BirdRoundPattern.SwarmPanic &&
            failureReason == FeedFailureReason.FeedWindowClosed)
        {
            TriggerPanic(birdIndex, birdTransforms[birdIndex].position, swarmPanicDuration);
        }
    }

    public void NotifyMissedThrow(Vector3 worldPosition)
    {
        if (currentPattern == BirdRoundPattern.SwarmPanic)
        {
            TriggerGlobalPanic(worldPosition, swarmPanicDuration);
        }
    }

    private void UpdateDivePattern()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i))
            {
                feedWindowStates[i] = false;
                continue;
            }

            float phase = GetPhase(i, diveDuration);
            float diveAmount = Mathf.Sin(phase * Mathf.PI);
            float sweepAmount = Mathf.Sin(phase * Mathf.PI * 2f);
            Vector3 sweepDirection = Quaternion.Euler(0f, i * 36f, 0f) * Vector3.right;

            Vector3 offset =
                sweepDirection * (sweepAmount * diveSweepDistance * 0.5f) +
                (Vector3.down * (diveAmount * diveDepth));

            ApplyOffset(i, offset);
            feedWindowStates[i] = diveAmount >= diveFeedWindowThreshold;
        }
    }

    private void UpdateCirclePattern()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i))
            {
                feedWindowStates[i] = false;
                continue;
            }

            float phase = GetPhase(i, circleDuration);
            float orbitAngle = phase * Mathf.PI * 2f;
            Vector3 orbitOffset =
                (Vector3.right * Mathf.Cos(orbitAngle) + Vector3.forward * Mathf.Sin(orbitAngle)) * circleRadius;
            Vector3 bobOffset = Vector3.up * (Mathf.Sin((Time.time + i) * circleBobFrequency * Mathf.PI * 2f) * circleBobAmplitude);

            float snatchTimer = Mathf.Repeat(Time.time + (i * 0.17f), Mathf.Max(snatchInterval, snatchDuration));
            bool isSnatching = snatchTimer <= snatchDuration;
            float snatchAmount = 0f;
            bool feedWindowOpen = false;

            if (isSnatching)
            {
                float snatchProgress = snatchTimer / Mathf.Max(0.05f, snatchDuration);
                snatchAmount = Mathf.Sin(snatchProgress * Mathf.PI) * snatchDistance;
                float halfWindow = snatchFeedWindowDuration * 0.5f;
                feedWindowOpen = Mathf.Abs(snatchTimer - (snatchDuration * 0.5f)) <= halfWindow;
            }

            Vector3 inwardDirection = orbitOffset.sqrMagnitude < 0.0001f ? Vector3.back : -orbitOffset.normalized;
            ApplyOffset(i, orbitOffset + bobOffset + (inwardDirection * snatchAmount));
            feedWindowStates[i] = feedWindowOpen;
        }
    }

    private void UpdateSwarmPattern()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i))
            {
                feedWindowStates[i] = false;
                continue;
            }

            float phase = GetPhase(i, swarmOrbitDuration);
            float orbitAngle = phase * Mathf.PI * 2f;
            Vector3 orbitOffset =
                (Vector3.right * Mathf.Cos(orbitAngle) + Vector3.forward * Mathf.Sin(orbitAngle)) * swarmOrbitRadius;
            Vector3 bobOffset = Vector3.up * (Mathf.Sin((Time.time + i) * swarmBobFrequency * Mathf.PI * 2f) * swarmBobAmplitude);

            float dartTimer = Mathf.Repeat(Time.time + (i * 0.13f), Mathf.Max(swarmDartInterval, swarmDartDuration));
            bool isDarting = dartTimer <= swarmDartDuration;
            float dartAmount = 0f;
            bool feedWindowOpen = false;

            if (isDarting)
            {
                float dartProgress = dartTimer / Mathf.Max(0.05f, swarmDartDuration);
                dartAmount = Mathf.Sin(dartProgress * Mathf.PI) * swarmDartDistance;
                float halfWindow = swarmFeedWindowDuration * 0.5f;
                feedWindowOpen = Mathf.Abs(dartTimer - (swarmDartDuration * 0.5f)) <= halfWindow;
            }

            Vector3 inwardDirection = orbitOffset.sqrMagnitude < 0.0001f ? Vector3.back : -orbitOffset.normalized;
            Vector3 panicOffset = Vector3.zero;

            if (panicTimers[i] > 0f)
            {
                float panicProgress = 1f - (panicTimers[i] / swarmPanicDuration);
                panicOffset = panicDirections[i] * (Mathf.Sin(panicProgress * Mathf.PI) * swarmPanicScatterDistance);
                feedWindowOpen = false;
            }

            ApplyOffset(i, orbitOffset + bobOffset + (inwardDirection * dartAmount) + panicOffset);
            feedWindowStates[i] = feedWindowOpen;
        }

        CheckSwarmCollisions();
    }

    private void UpdateAggressivePattern()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i))
            {
                feedWindowStates[i] = false;
                trackedProjectiles[i] = null;
                interceptTimers[i] = 0f;
                continue;
            }

            if (CanAcquireProjectile(i, out BreadProjectile projectile))
            {
                trackedProjectiles[i] = projectile;
                interceptTimers[i] = maxInterceptDuration;
            }

            if (IsIntercepting(i))
            {
                UpdateInterceptMotion(i);
            }
            else
            {
                UpdatePatrolMotion(i);
            }
        }
    }

    private void UpdatePatrolMotion(int birdIndex)
    {
        float phase = GetPhase(birdIndex, patrolDuration);
        Vector3 direction = Quaternion.Euler(0f, birdIndex * 36f, 0f) * Vector3.right;
        Vector3 offset = direction * (Mathf.Sin(phase * Mathf.PI * 2f) * patrolDistance * 0.5f);
        offset += Vector3.up * (Mathf.Sin((Time.time + birdIndex) * patrolHoverFrequency * Mathf.PI * 2f) * patrolHoverAmplitude);

        Vector3 targetWorldPosition = transform.TransformPoint(anchorLocalPositions[birdIndex] + offset);
        Vector3 nextWorldPosition = Vector3.MoveTowards(
            birdTransforms[birdIndex].position,
            targetWorldPosition,
            returnSpeed * Time.deltaTime);

        ApplyWorldPosition(birdIndex, nextWorldPosition);
        feedWindowStates[birdIndex] = false;
    }

    private void UpdateInterceptMotion(int birdIndex)
    {
        interceptTimers[birdIndex] = Mathf.Max(0f, interceptTimers[birdIndex] - Time.deltaTime);

        if (!IsIntercepting(birdIndex))
        {
            trackedProjectiles[birdIndex] = null;
            interceptCooldownTimers[birdIndex] = interceptCooldown;
            UpdatePatrolMotion(birdIndex);
            return;
        }

        Vector3 predictedPosition = trackedProjectiles[birdIndex].transform.position +
            (trackedProjectiles[birdIndex].CurrentVelocity * interceptLeadTime);

        Vector3 nextWorldPosition = Vector3.MoveTowards(
            birdTransforms[birdIndex].position,
            predictedPosition,
            interceptSpeed * Time.deltaTime);

        ApplyWorldPosition(birdIndex, nextWorldPosition);
        feedWindowStates[birdIndex] = true;
    }

    private bool CanAcquireProjectile(int birdIndex, out BreadProjectile projectile)
    {
        projectile = null;

        if (miniGameManager == null || !miniGameManager.IsRunning || interceptCooldownTimers[birdIndex] > 0f)
        {
            return false;
        }

        if (trackedProjectiles[birdIndex] != null && trackedProjectiles[birdIndex].IsActiveProjectile)
        {
            return false;
        }

        return miniGameManager.TryGetNearestActiveProjectile(
            birdTransforms[birdIndex].position,
            detectionRadius,
            out projectile);
    }

    private bool IsIntercepting(int birdIndex)
    {
        return trackedProjectiles[birdIndex] != null &&
            trackedProjectiles[birdIndex].IsActiveProjectile &&
            interceptTimers[birdIndex] > 0f;
    }

    private void CheckSwarmCollisions()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i) || collisionCooldownTimers[i] > 0f)
            {
                continue;
            }

            for (int j = i + 1; j < birdTransforms.Length; j++)
            {
                if (!IsBirdAvailable(j) || collisionCooldownTimers[j] > 0f)
                {
                    continue;
                }

                if (Vector3.Distance(birdTransforms[i].position, birdTransforms[j].position) > swarmCollisionDistance)
                {
                    continue;
                }

                collisionCooldownTimers[i] = swarmCollisionCooldown;
                collisionCooldownTimers[j] = swarmCollisionCooldown;

                Vector3 collisionPoint = (birdTransforms[i].position + birdTransforms[j].position) * 0.5f;
                TriggerGlobalPanic(collisionPoint, swarmPanicDuration);
                miniGameManager?.NotifyBirdCollision(collisionPoint);
                return;
            }
        }
    }

    private void TriggerNearbyPanic(Vector3 worldPosition, float radius, int excludeIndex)
    {
        float radiusSqr = radius * radius;
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (i == excludeIndex || !IsBirdAvailable(i))
            {
                continue;
            }

            if ((birdTransforms[i].position - worldPosition).sqrMagnitude > radiusSqr)
            {
                continue;
            }

            TriggerPanic(i, worldPosition, swarmPanicDuration);
        }
    }

    private void TriggerGlobalPanic(Vector3 worldPosition, float duration)
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (IsBirdAvailable(i))
            {
                TriggerPanic(i, worldPosition, duration);
            }
        }
    }

    private void TriggerPanic(int birdIndex, Vector3 threatPosition, float duration)
    {
        panicTimers[birdIndex] = Mathf.Max(0.05f, duration);

        Vector3 fleeDirection = birdTransforms[birdIndex].position - threatPosition;
        fleeDirection += new Vector3(
            UnityEngine.Random.Range(-0.4f, 0.4f),
            0f,
            UnityEngine.Random.Range(-0.4f, 0.4f));
        fleeDirection.y = 0.15f;

        panicDirections[birdIndex] = GetSafeDirection(fleeDirection, Vector3.forward + Vector3.up);
    }

    private void UpdateTimers()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (panicTimers[i] > 0f)
            {
                panicTimers[i] = Mathf.Max(0f, panicTimers[i] - Time.deltaTime);
            }

            if (collisionCooldownTimers[i] > 0f)
            {
                collisionCooldownTimers[i] = Mathf.Max(0f, collisionCooldownTimers[i] - Time.deltaTime);
            }

            if (interceptCooldownTimers[i] > 0f)
            {
                interceptCooldownTimers[i] = Mathf.Max(0f, interceptCooldownTimers[i] - Time.deltaTime);
            }
        }
    }

    private void UpdateBirdRotations()
    {
        if (!rotateWithMovement)
        {
            return;
        }

        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i))
            {
                continue;
            }

            Vector3 movement = birdTransforms[i].position - lastWorldPositions[i];
            if (movement.sqrMagnitude > 0.0001f)
            {
                Vector3 forward = movement.normalized;
                Vector3 upAxis = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f ? Vector3.right : Vector3.up;
                Quaternion targetRotation = Quaternion.LookRotation(forward, upAxis);
                birdTransforms[i].rotation = Quaternion.Slerp(
                    birdTransforms[i].rotation,
                    targetRotation,
                    rotationLerpSpeed * Time.deltaTime);
            }

            lastWorldPositions[i] = birdTransforms[i].position;
        }
    }

    private float GetPhase(int birdIndex, float duration)
    {
        return Mathf.Repeat((Time.time / Mathf.Max(0.05f, duration)) + GetPhaseOffset(birdIndex), 1f);
    }

    private float GetPhaseOffset(int birdIndex)
    {
        return birdTransforms.Length <= 1 ? 0f : birdIndex / (float)birdTransforms.Length;
    }

    private void ApplyOffset(int birdIndex, Vector3 localOffset)
    {
        birdTransforms[birdIndex].localPosition = anchorLocalPositions[birdIndex] + localOffset;
    }

    private void ApplyWorldPosition(int birdIndex, Vector3 worldPosition)
    {
        birdTransforms[birdIndex].position = worldPosition;
    }

    private Vector3 GetSafeDirection(Vector3 candidate, Vector3 fallback)
    {
        if (candidate.sqrMagnitude < 0.0001f)
        {
            return fallback.normalized;
        }

        return candidate.normalized;
    }

    private int GetBirdIndex(BirdFeedingTarget target)
    {
        if (target == null)
        {
            return -1;
        }

        for (int i = 0; i < birdTargets.Length; i++)
        {
            if (birdTargets[i] == target)
            {
                return i;
            }
        }

        return -1;
    }

    private void DisableLegacyMovementScripts()
    {
        BirdBase[] legacyBirdBases = GetComponentsInChildren<BirdBase>(true);
        for (int i = 0; i < legacyBirdBases.Length; i++)
        {
            if (legacyBirdBases[i] != null)
            {
                legacyBirdBases[i].enabled = false;
            }
        }
    }

    private void CacheBirdData()
    {
        if (birdTargets == null)
        {
            birdTargets = Array.Empty<BirdFeedingTarget>();
        }

        int birdCount = birdTargets.Length;
        birdTransforms = new Transform[birdCount];
        anchorLocalPositions = new Vector3[birdCount];
        lastWorldPositions = new Vector3[birdCount];
        feedWindowStates = new bool[birdCount];
        panicTimers = new float[birdCount];
        panicDirections = new Vector3[birdCount];
        collisionCooldownTimers = new float[birdCount];
        trackedProjectiles = new BreadProjectile[birdCount];
        interceptTimers = new float[birdCount];
        interceptCooldownTimers = new float[birdCount];
        fedStates = new bool[birdCount];

        for (int i = 0; i < birdCount; i++)
        {
            birdTransforms[i] = birdTargets[i] != null ? birdTargets[i].transform : null;
            if (birdTransforms[i] == null)
            {
                continue;
            }

            anchorLocalPositions[i] = birdTransforms[i].localPosition;
            lastWorldPositions[i] = birdTransforms[i].position;
        }
    }

    private void ResolveReferences()
    {
        if (miniGameManager == null)
        {
            miniGameManager = FindFirstObjectByType<FeedingMiniGameManager>();
        }

        if (birdTargets == null || birdTargets.Length == 0)
        {
            birdTargets = GetComponentsInChildren<BirdFeedingTarget>(true);
            Array.Sort(birdTargets, CompareTargets);
        }
    }

    private int CompareTargets(BirdFeedingTarget left, BirdFeedingTarget right)
    {
        if (left == right)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        return string.CompareOrdinal(left.name, right.name);
    }

    private bool IsBirdAvailable(int birdIndex)
    {
        return birdIndex >= 0 &&
            birdIndex < birdTargets.Length &&
            birdTargets[birdIndex] != null &&
            birdTargets[birdIndex].gameObject.activeInHierarchy &&
            !fedStates[birdIndex] &&
            birdTransforms[birdIndex] != null;
    }

    private void OnValidate()
    {
        ResolveReferences();
    }
}
