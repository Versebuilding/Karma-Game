using System;
using System.Collections.Generic;
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
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";

    private sealed class BirdVisualCache
    {
        public SpriteRenderer[] SpriteRenderers = Array.Empty<SpriteRenderer>();
        public Color[] SpriteColors = Array.Empty<Color>();
        public Renderer[] MaterialRenderers = Array.Empty<Renderer>();
        public string[] MaterialColorProperties = Array.Empty<string>();
        public Color[] MaterialColors = Array.Empty<Color>();
        public MaterialPropertyBlock[] PropertyBlocks = Array.Empty<MaterialPropertyBlock>();
    }

    [Header("References")]
    [SerializeField] private FeedingMiniGameManager miniGameManager;
    [SerializeField] private BreadThrower breadThrower;
    [SerializeField] private Transform facingTarget;
    [SerializeField] private Camera facingCamera;
    [SerializeField] private BirdFeedingTarget[] birdTargets;

    [Header("Common")]
    [SerializeField] private BirdRoundPattern currentPattern = BirdRoundPattern.DiveFeed;
    [SerializeField] private bool rotateWithMovement = true;
    [SerializeField] private bool facePlayerWhenReady = true;
    [SerializeField] private bool flattenFacingDirection = true;
    [SerializeField] [Min(0f)] private float rotationLerpSpeed = 10f;
    [SerializeField] [Min(0f)] private float facePlayerRotationLerpSpeed = 14f;
    [SerializeField] private bool useFeedWindowColorIndicator = true;
    [SerializeField] private Color feedWindowColor = Color.red;

    [Header("Dive Feed")]
    [SerializeField] [Min(0f)] private float diveSweepDistance = 1.4f;
    [SerializeField] [Min(0f)] private float diveDepth = 1.8f;
    [SerializeField] [Min(0.05f)] private float diveDuration = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float diveFeedWindowThreshold = 0.82f;
    [SerializeField] [Range(0f, 1f)] private float diveFacePlayerThreshold = 0.68f;

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
    [SerializeField] [Min(0f)] private float swarmMissGatherRadius = 4.5f;
    [SerializeField] [Min(0.05f)] private float swarmMissGatherDuration = 0.5f;
    [SerializeField] [Min(0f)] private float swarmMissGatherSpeed = 5.5f;
    [SerializeField] [Min(0f)] private float swarmMissGatherSpacing = 0.75f;
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
    private bool[] facePlayerStates = Array.Empty<bool>();
    private float[] panicTimers = Array.Empty<float>();
    private float[] panicDurations = Array.Empty<float>();
    private Vector3[] panicDirections = Array.Empty<Vector3>();
    private float[] gatherTimers = Array.Empty<float>();
    private Vector3[] gatherPoints = Array.Empty<Vector3>();
    private bool[] scatterAfterGather = Array.Empty<bool>();
    private float[] collisionCooldownTimers = Array.Empty<float>();
    private BreadProjectile[] trackedProjectiles = Array.Empty<BreadProjectile>();
    private float[] interceptTimers = Array.Empty<float>();
    private float[] interceptCooldownTimers = Array.Empty<float>();
    private bool[] fedStates = Array.Empty<bool>();
    private BirdVisualCache[] birdVisuals = Array.Empty<BirdVisualCache>();

    public BirdRoundPattern CurrentPattern => currentPattern;
    public int BirdCount => birdTargets != null ? birdTargets.Length : 0;

    private void Awake()
    {
        ResolveReferences();
        DisableLegacyMovementScripts();
        CacheBirdData();
        ResetPatternState();
    }

    private void OnDisable()
    {
        RestoreAllBirdVisuals();
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

        UpdateBirdVisuals();
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
            if (birdTargets[i] != null && !birdTargets[i].gameObject.activeSelf)
            {
                birdTargets[i].gameObject.SetActive(true);
            }

            if (birdTransforms[i] == null)
            {
                continue;
            }

            birdTransforms[i].localPosition = anchorLocalPositions[i];
            feedWindowStates[i] = false;
            facePlayerStates[i] = false;
            panicTimers[i] = 0f;
            panicDurations[i] = 0f;
            panicDirections[i] = Vector3.zero;
            gatherTimers[i] = 0f;
            gatherPoints[i] = birdTransforms[i].position;
            scatterAfterGather[i] = false;
            collisionCooldownTimers[i] = 0f;
            trackedProjectiles[i] = null;
            interceptTimers[i] = 0f;
            interceptCooldownTimers[i] = 0f;
            fedStates[i] = false;
            lastWorldPositions[i] = birdTransforms[i].position;
            ApplyBirdVisual(i, false);
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
        facePlayerStates[birdIndex] = false;
        panicTimers[birdIndex] = 0f;
        panicDurations[birdIndex] = 0f;
        gatherTimers[birdIndex] = 0f;
        scatterAfterGather[birdIndex] = false;
        collisionCooldownTimers[birdIndex] = 0f;
        trackedProjectiles[birdIndex] = null;
        interceptTimers[birdIndex] = 0f;
        interceptCooldownTimers[birdIndex] = interceptCooldown;
        ApplyBirdVisual(birdIndex, false);

        if (currentPattern == BirdRoundPattern.SwarmPanic)
        {
            TriggerNearbyPanic(feedPosition, swarmFeedPanicRadius, birdIndex);
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

        feedWindowStates[birdIndex] = false;
    }

    public void NotifyMissedThrow(Vector3 worldPosition)
    {
        if (currentPattern != BirdRoundPattern.SwarmPanic)
        {
            return;
        }

        TriggerNearbyGather(worldPosition);
    }

    private void UpdateDivePattern()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i))
            {
                feedWindowStates[i] = false;
                facePlayerStates[i] = false;
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
            facePlayerStates[i] = diveAmount >= diveFacePlayerThreshold;
        }
    }

    private void UpdateCirclePattern()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i))
            {
                feedWindowStates[i] = false;
                facePlayerStates[i] = false;
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
            facePlayerStates[i] = isSnatching;
        }
    }

    private void UpdateSwarmPattern()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i))
            {
                feedWindowStates[i] = false;
                facePlayerStates[i] = false;
                continue;
            }

            if (gatherTimers[i] > 0f)
            {
                UpdateSwarmGatherMotion(i);
                continue;
            }

            if (scatterAfterGather[i])
            {
                scatterAfterGather[i] = false;
                TriggerPanic(i, gatherPoints[i], swarmPanicDuration);
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
                float safePanicDuration = Mathf.Max(0.05f, panicDurations[i]);
                float panicProgress = 1f - (panicTimers[i] / safePanicDuration);
                panicOffset = panicDirections[i] * (Mathf.Sin(panicProgress * Mathf.PI) * swarmPanicScatterDistance);
                feedWindowOpen = false;
            }

            ApplyOffset(i, orbitOffset + bobOffset + (inwardDirection * dartAmount) + panicOffset);
            feedWindowStates[i] = feedWindowOpen;
            facePlayerStates[i] = isDarting && panicTimers[i] <= 0f;
        }

        CheckSwarmCollisions();
    }

    private void UpdateSwarmGatherMotion(int birdIndex)
    {
        Vector3 ringOffset = GetSwarmGatherOffset(birdIndex);
        Vector3 bobOffset = Vector3.up * (Mathf.Sin((Time.time + birdIndex) * swarmBobFrequency * Mathf.PI * 2f) * swarmBobAmplitude);
        Vector3 targetWorldPosition = gatherPoints[birdIndex] + ringOffset + bobOffset;
        Vector3 nextWorldPosition = Vector3.MoveTowards(
            birdTransforms[birdIndex].position,
            targetWorldPosition,
            swarmMissGatherSpeed * Time.deltaTime);

        ApplyWorldPosition(birdIndex, nextWorldPosition);
        feedWindowStates[birdIndex] = false;
        facePlayerStates[birdIndex] = false;
    }

    private Vector3 GetSwarmGatherOffset(int birdIndex)
    {
        float angle = GetPhaseOffset(birdIndex) * Mathf.PI * 2f;
        return new Vector3(Mathf.Cos(angle), 0.2f, Mathf.Sin(angle)) * swarmMissGatherSpacing;
    }

    private void UpdateAggressivePattern()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i))
            {
                feedWindowStates[i] = false;
                facePlayerStates[i] = false;
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
        facePlayerStates[birdIndex] = false;
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
        facePlayerStates[birdIndex] = true;
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
            if (!IsBirdAvailable(i) || collisionCooldownTimers[i] > 0f || gatherTimers[i] > 0f)
            {
                continue;
            }

            for (int j = i + 1; j < birdTransforms.Length; j++)
            {
                if (!IsBirdAvailable(j) || collisionCooldownTimers[j] > 0f || gatherTimers[j] > 0f)
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

    private void TriggerNearbyGather(Vector3 worldPosition)
    {
        float radiusSqr = swarmMissGatherRadius * swarmMissGatherRadius;
        bool hasNearbyBird = false;

        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i))
            {
                continue;
            }

            if ((birdTransforms[i].position - worldPosition).sqrMagnitude > radiusSqr)
            {
                continue;
            }

            hasNearbyBird = true;
            gatherTimers[i] = swarmMissGatherDuration;
            gatherPoints[i] = worldPosition;
            scatterAfterGather[i] = true;
            panicTimers[i] = 0f;
            panicDurations[i] = 0f;
            facePlayerStates[i] = false;
            feedWindowStates[i] = false;
        }

        if (!hasNearbyBird)
        {
            TriggerGlobalPanic(worldPosition, swarmPanicDuration);
        }
    }

    private void TriggerPanic(int birdIndex, Vector3 threatPosition, float duration)
    {
        gatherTimers[birdIndex] = 0f;
        scatterAfterGather[birdIndex] = false;
        panicDurations[birdIndex] = Mathf.Max(0.05f, duration);
        panicTimers[birdIndex] = panicDurations[birdIndex];

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

            if (gatherTimers[i] > 0f)
            {
                gatherTimers[i] = Mathf.Max(0f, gatherTimers[i] - Time.deltaTime);
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

    private void UpdateBirdVisuals()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            bool shouldShowFeedWindow = IsBirdAvailable(i) && feedWindowStates[i];
            ApplyBirdVisual(i, shouldShowFeedWindow);
        }
    }

    private void ApplyBirdVisual(int birdIndex, bool isFeedWindowOpen)
    {
        if (!useFeedWindowColorIndicator ||
            birdIndex < 0 ||
            birdIndex >= birdVisuals.Length ||
            birdVisuals[birdIndex] == null)
        {
            return;
        }

        BirdVisualCache visualCache = birdVisuals[birdIndex];

        for (int i = 0; i < visualCache.SpriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = visualCache.SpriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            Color targetColor = isFeedWindowOpen ? feedWindowColor : visualCache.SpriteColors[i];
            targetColor.a = visualCache.SpriteColors[i].a;
            spriteRenderer.color = targetColor;
        }

        for (int i = 0; i < visualCache.MaterialRenderers.Length; i++)
        {
            Renderer renderer = visualCache.MaterialRenderers[i];
            string colorProperty = visualCache.MaterialColorProperties[i];
            if (renderer == null || string.IsNullOrEmpty(colorProperty))
            {
                continue;
            }

            MaterialPropertyBlock propertyBlock = visualCache.PropertyBlocks[i];
            propertyBlock.Clear();

            Color targetColor = isFeedWindowOpen ? feedWindowColor : visualCache.MaterialColors[i];
            targetColor.a = visualCache.MaterialColors[i].a;
            propertyBlock.SetColor(colorProperty, targetColor);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void RestoreAllBirdVisuals()
    {
        for (int i = 0; i < birdVisuals.Length; i++)
        {
            ApplyBirdVisual(i, false);
        }
    }

    private void UpdateBirdRotations()
    {
        for (int i = 0; i < birdTransforms.Length; i++)
        {
            if (!IsBirdAvailable(i))
            {
                continue;
            }

            Vector3 desiredForward = Vector3.zero;
            float lerpSpeed = rotationLerpSpeed;
            bool hasDesiredForward = false;

            if (facePlayerWhenReady &&
                facePlayerStates[i] &&
                TryGetFacingDirection(i, out Vector3 facingDirection))
            {
                desiredForward = facingDirection;
                lerpSpeed = facePlayerRotationLerpSpeed;
                hasDesiredForward = true;
            }
            else if (rotateWithMovement)
            {
                Vector3 movement = birdTransforms[i].position - lastWorldPositions[i];
                if (movement.sqrMagnitude > 0.0001f)
                {
                    desiredForward = movement.normalized;
                    hasDesiredForward = true;
                }
            }

            if (hasDesiredForward)
            {
                Vector3 upAxis = Mathf.Abs(Vector3.Dot(desiredForward, Vector3.up)) > 0.98f
                    ? Vector3.right
                    : Vector3.up;
                Quaternion targetRotation = Quaternion.LookRotation(desiredForward, upAxis);
                birdTransforms[i].rotation = Quaternion.Slerp(
                    birdTransforms[i].rotation,
                    targetRotation,
                    lerpSpeed * Time.deltaTime);
            }

            lastWorldPositions[i] = birdTransforms[i].position;
        }
    }

    private bool TryGetFacingDirection(int birdIndex, out Vector3 direction)
    {
        direction = Vector3.zero;

        if (birdIndex < 0 || birdIndex >= birdTransforms.Length || birdTransforms[birdIndex] == null)
        {
            return false;
        }

        Transform target = GetFacingReference();
        if (target == null)
        {
            return false;
        }

        Vector3 candidateDirection = target.position - birdTransforms[birdIndex].position;
        Vector3 unflattenedDirection = candidateDirection;

        if (flattenFacingDirection)
        {
            candidateDirection.y = 0f;
        }

        if (candidateDirection.sqrMagnitude < 0.0001f)
        {
            candidateDirection = unflattenedDirection;
        }

        if (candidateDirection.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        direction = candidateDirection.normalized;
        return true;
    }

    private Transform GetFacingReference()
    {
        if (facingTarget != null)
        {
            return facingTarget;
        }

        if (facingCamera != null)
        {
            return facingCamera.transform;
        }

        if (breadThrower != null)
        {
            if (breadThrower.AimCamera != null)
            {
                return breadThrower.AimCamera.transform;
            }

            if (breadThrower.ThrowOrigin != null)
            {
                return breadThrower.ThrowOrigin;
            }

            return breadThrower.transform;
        }

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
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
        if (birdTransforms[birdIndex] == null)
        {
            return;
        }

        birdTransforms[birdIndex].localPosition = anchorLocalPositions[birdIndex] + localOffset;
    }

    private void ApplyWorldPosition(int birdIndex, Vector3 worldPosition)
    {
        if (birdTransforms[birdIndex] == null)
        {
            return;
        }

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

        bool requiresRebuild = birdTransforms.Length != birdTargets.Length;
        if (!requiresRebuild)
        {
            for (int i = 0; i < birdTargets.Length; i++)
            {
                Transform targetTransform = birdTargets[i] != null ? birdTargets[i].transform : null;
                if (birdTransforms[i] != targetTransform)
                {
                    requiresRebuild = true;
                    break;
                }
            }
        }

        if (!requiresRebuild)
        {
            return;
        }

        int birdCount = birdTargets.Length;
        birdTransforms = new Transform[birdCount];
        anchorLocalPositions = new Vector3[birdCount];
        lastWorldPositions = new Vector3[birdCount];
        feedWindowStates = new bool[birdCount];
        facePlayerStates = new bool[birdCount];
        panicTimers = new float[birdCount];
        panicDurations = new float[birdCount];
        panicDirections = new Vector3[birdCount];
        gatherTimers = new float[birdCount];
        gatherPoints = new Vector3[birdCount];
        scatterAfterGather = new bool[birdCount];
        collisionCooldownTimers = new float[birdCount];
        trackedProjectiles = new BreadProjectile[birdCount];
        interceptTimers = new float[birdCount];
        interceptCooldownTimers = new float[birdCount];
        fedStates = new bool[birdCount];
        birdVisuals = new BirdVisualCache[birdCount];

        for (int i = 0; i < birdCount; i++)
        {
            birdTransforms[i] = birdTargets[i] != null ? birdTargets[i].transform : null;
            if (birdTransforms[i] == null)
            {
                continue;
            }

            anchorLocalPositions[i] = birdTransforms[i].localPosition;
            lastWorldPositions[i] = birdTransforms[i].position;
            gatherPoints[i] = birdTransforms[i].position;
            birdVisuals[i] = BuildVisualCache(birdTransforms[i]);
        }
    }

    private BirdVisualCache BuildVisualCache(Transform birdRoot)
    {
        BirdVisualCache visualCache = new BirdVisualCache();
        if (birdRoot == null)
        {
            return visualCache;
        }

        SpriteRenderer[] spriteRenderers = birdRoot.GetComponentsInChildren<SpriteRenderer>(true);
        visualCache.SpriteRenderers = spriteRenderers;
        visualCache.SpriteColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            visualCache.SpriteColors[i] = spriteRenderers[i] != null
                ? spriteRenderers[i].color
                : Color.white;
        }

        Renderer[] allRenderers = birdRoot.GetComponentsInChildren<Renderer>(true);
        List<Renderer> materialRenderers = new List<Renderer>(allRenderers.Length);
        List<string> colorProperties = new List<string>(allRenderers.Length);
        List<Color> materialColors = new List<Color>(allRenderers.Length);
        List<MaterialPropertyBlock> propertyBlocks = new List<MaterialPropertyBlock>(allRenderers.Length);

        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer renderer = allRenderers[i];
            if (renderer == null || renderer is SpriteRenderer)
            {
                continue;
            }

            string colorProperty = ResolveColorProperty(renderer);
            if (string.IsNullOrEmpty(colorProperty))
            {
                continue;
            }

            materialRenderers.Add(renderer);
            colorProperties.Add(colorProperty);
            materialColors.Add(renderer.sharedMaterial.GetColor(colorProperty));
            propertyBlocks.Add(new MaterialPropertyBlock());
        }

        visualCache.MaterialRenderers = materialRenderers.ToArray();
        visualCache.MaterialColorProperties = colorProperties.ToArray();
        visualCache.MaterialColors = materialColors.ToArray();
        visualCache.PropertyBlocks = propertyBlocks.ToArray();
        return visualCache;
    }

    private string ResolveColorProperty(Renderer renderer)
    {
        if (renderer == null || renderer.sharedMaterial == null)
        {
            return null;
        }

        if (renderer.sharedMaterial.HasProperty(BaseColorProperty))
        {
            return BaseColorProperty;
        }

        if (renderer.sharedMaterial.HasProperty(ColorProperty))
        {
            return ColorProperty;
        }

        return null;
    }

    private void ResolveReferences()
    {
        if (miniGameManager == null)
        {
            miniGameManager = FindFirstObjectByType<FeedingMiniGameManager>();
        }

        if (breadThrower == null)
        {
            breadThrower = FindFirstObjectByType<BreadThrower>();
        }

        if (facingCamera == null && breadThrower != null)
        {
            facingCamera = breadThrower.AimCamera;
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

        if (snatchInterval < snatchDuration)
        {
            snatchInterval = snatchDuration;
        }

        if (snatchFeedWindowDuration > snatchDuration)
        {
            snatchFeedWindowDuration = snatchDuration;
        }

        if (swarmDartInterval < swarmDartDuration)
        {
            swarmDartInterval = swarmDartDuration;
        }

        if (swarmFeedWindowDuration > swarmDartDuration)
        {
            swarmFeedWindowDuration = swarmDartDuration;
        }
    }
}
