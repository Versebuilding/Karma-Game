using System;
using System.Collections.Generic;
using UnityEngine;

public class FeedingMiniGameManager : MonoBehaviour
{
    [Serializable]
    private class PatternSetup
    {
        public string displayName = "Pattern";
        public BirdRoundPattern patternType = BirdRoundPattern.DiveFeed;
        [Min(1)] public int targetFeeds = 10;
        [Min(1f)] public float roundDuration = 90f;
    }

    [Header("Round")]
    [SerializeField] [Min(1f)] private float roundDuration = 90f;
    [SerializeField] [Min(1)] private int defaultTargetFeeds = 10;
    [SerializeField] private bool startOnStart = true;
    [SerializeField] private bool autoAdvanceToNextPattern = true;
    [SerializeField] private bool allowDebugPatternHotkey = true;
    [SerializeField] private KeyCode debugNextPatternKey = KeyCode.Alpha1;

    [Header("Patterns")]
    [SerializeField] private PatternSetup[] patterns;
    [SerializeField] [Min(0)] private int startingPatternIndex;
    [SerializeField] private BirdPatternGroupController birdPatternGroup;

    [Header("Persistence")]
    [SerializeField] private string bestTimePrefsKeyPrefix = "BirdFeedingBestTime";

    [Header("Failure Tuning")]
    [SerializeField] [Range(0f, 100f)] private float startingEfficiency = 100f;
    [SerializeField] [Min(0f)] private float failurePenalty = 10f;
    [SerializeField] [Min(0f)] private float legacySwarmFeedPanicRadius = 4f;
    [SerializeField] [Min(0.05f)] private float legacySwarmFeedPanicDuration = 1.25f;

    [Header("Runtime")]
    [SerializeField] private int currentScore;
    [SerializeField] private int successfulFeeds;
    [SerializeField] private int failedFeeds;
    [SerializeField] private float currentEfficiency;
    [SerializeField] private float remainingTime;
    [SerializeField] private bool isRunning;
    [SerializeField] private bool lastAttemptSucceeded;
    [SerializeField] private FeedFailureReason lastFailureReason = FeedFailureReason.None;
    [SerializeField] private string lastFeedbackMessage;
    [SerializeField] private int currentPatternIndex;
    [SerializeField] private float bestCompletionTimeSeconds = -1f;

    private readonly List<BreadProjectile> activeProjectiles = new List<BreadProjectile>();
    private readonly List<BirdSwarmMovement> legacySwarmBirds = new List<BirdSwarmMovement>();

    public int CurrentScore => currentScore;
    public int SuccessfulFeeds => successfulFeeds;
    public int FailedFeeds => failedFeeds;
    public float CurrentEfficiency => currentEfficiency;
    public float RemainingTime => remainingTime;
    public bool IsRunning => isRunning;
    public bool CanResolveFeedAttempts => isRunning;
    public bool LastAttemptSucceeded => lastAttemptSucceeded;
    public FeedFailureReason LastFailureReason => lastFailureReason;
    public string LastFeedbackMessage => lastFeedbackMessage;
    public int CurrentPatternIndex => currentPatternIndex;
    public int CurrentTargetFeeds => GetActivePatternTargetFeeds();
    public string CurrentPatternName => GetActivePatternName();
    public float BestCompletionTimeSeconds => bestCompletionTimeSeconds;
    public bool HasBestCompletionTime => bestCompletionTimeSeconds >= 0f;
    public float CurrentRoundDuration => GetActivePatternRoundDuration();

    private void Awake()
    {
        MigrateDebugHotkey();
        ResolveReferences();
        EnsurePatternSetups();
        currentPatternIndex = Mathf.Clamp(startingPatternIndex, 0, Mathf.Max(0, patterns.Length - 1));
        ApplyPattern(currentPatternIndex, false);
        ResetGame();
    }

    private void Start()
    {
        if (startOnStart)
        {
            BeginGame();
        }
    }

    private void Update()
    {
        if (allowDebugPatternHotkey && Input.GetKeyDown(debugNextPatternKey))
        {
            NextPattern();
            return;
        }

        if (!isRunning)
        {
            return;
        }

        remainingTime -= Time.deltaTime;
        if (remainingTime > 0f)
        {
            return;
        }

        remainingTime = 0f;
        EndGame(false);
    }

    public void BeginGame()
    {
        ResolveReferences();
        EnsurePatternSetups();
        ClearActiveProjectiles();

        currentScore = 0;
        successfulFeeds = 0;
        failedFeeds = 0;
        currentEfficiency = startingEfficiency;
        remainingTime = GetActivePatternRoundDuration();
        isRunning = true;
        lastAttemptSucceeded = false;
        lastFailureReason = FeedFailureReason.None;
        lastFeedbackMessage = string.Empty;

        birdPatternGroup?.ResetPatternState();
        ResetAllTargets();
    }

    public void EndGame()
    {
        EndGame(false);
    }

    public void ResetGame()
    {
        ClearActiveProjectiles();

        currentScore = 0;
        successfulFeeds = 0;
        failedFeeds = 0;
        currentEfficiency = startingEfficiency;
        remainingTime = GetActivePatternRoundDuration();
        isRunning = false;
        lastAttemptSucceeded = false;
        lastFailureReason = FeedFailureReason.None;
        lastFeedbackMessage = string.Empty;

        birdPatternGroup?.ResetPatternState();
        ResetAllTargets();
        LoadBestTimeForCurrentPattern();
    }

    public void NextPattern()
    {
        EnsurePatternSetups();
        if (patterns.Length == 0)
        {
            return;
        }

        int nextIndex = (currentPatternIndex + 1) % patterns.Length;
        ApplyPattern(nextIndex, true);
    }

    public void SetPattern(int patternIndex)
    {
        ApplyPattern(patternIndex, true);
    }

    public void RegisterSuccessfulFeed(FeedingTarget feedingTarget, Component feedSource)
    {
        if (!isRunning || feedingTarget == null)
        {
            return;
        }

        currentScore += feedingTarget.ScoreValue;
        successfulFeeds += 1;
        lastAttemptSucceeded = true;
        lastFailureReason = FeedFailureReason.None;
        lastFeedbackMessage = "Feed success. Score +" + feedingTarget.ScoreValue + ".";

        if (successfulFeeds >= GetActivePatternTargetFeeds())
        {
            CompleteRound();
        }
    }

    public void RegisterFailedFeed(
        FeedingTarget feedingTarget,
        Component feedSource,
        FeedFailureReason failureReason)
    {
        if (!isRunning)
        {
            return;
        }

        failedFeeds += 1;
        ApplyFailurePenalty();
        lastAttemptSucceeded = false;
        lastFailureReason = failureReason;
        lastFeedbackMessage = BuildFailureMessage(failureReason);
    }

    public void RegisterMissedThrow(BreadProjectile projectile, FeedFailureReason failureReason)
    {
        if (!isRunning)
        {
            return;
        }

        failedFeeds += 1;
        ApplyFailurePenalty();
        lastAttemptSucceeded = false;
        lastFailureReason = failureReason;
        lastFeedbackMessage = BuildFailureMessage(failureReason);

        if (projectile != null)
        {
            birdPatternGroup?.NotifyMissedThrow(projectile.transform.position);
        }
    }

    public void RegisterProjectile(BreadProjectile projectile)
    {
        if (projectile == null || activeProjectiles.Contains(projectile))
        {
            return;
        }

        activeProjectiles.Add(projectile);
    }

    public void UnregisterProjectile(BreadProjectile projectile)
    {
        if (projectile == null)
        {
            return;
        }

        activeProjectiles.Remove(projectile);
    }

    public void RegisterSwarmBird(BirdSwarmMovement swarmBird)
    {
        if (swarmBird == null || legacySwarmBirds.Contains(swarmBird))
        {
            return;
        }

        legacySwarmBirds.Add(swarmBird);
    }

    public void UnregisterSwarmBird(BirdSwarmMovement swarmBird)
    {
        if (swarmBird == null)
        {
            return;
        }

        legacySwarmBirds.Remove(swarmBird);
    }

    public void NotifySwarmBirdFed(BirdSwarmMovement sourceBird, Vector3 feedPosition)
    {
        for (int i = legacySwarmBirds.Count - 1; i >= 0; i--)
        {
            BirdSwarmMovement swarmBird = legacySwarmBirds[i];
            if (swarmBird == null)
            {
                legacySwarmBirds.RemoveAt(i);
                continue;
            }

            if (swarmBird == sourceBird)
            {
                continue;
            }

            float distanceToFeed = Vector3.Distance(swarmBird.transform.position, feedPosition);
            if (distanceToFeed > legacySwarmFeedPanicRadius)
            {
                continue;
            }

            swarmBird.TriggerPanic(feedPosition, legacySwarmFeedPanicDuration);
        }
    }

    public bool TryGetNearestActiveProjectile(
        Vector3 worldPosition,
        float detectionRadius,
        out BreadProjectile projectile)
    {
        projectile = null;

        float maxDistanceSqr = detectionRadius * detectionRadius;
        float closestDistanceSqr = float.MaxValue;

        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            BreadProjectile candidate = activeProjectiles[i];
            if (candidate == null || !candidate.IsActiveProjectile)
            {
                activeProjectiles.RemoveAt(i);
                continue;
            }

            float distanceSqr = (candidate.transform.position - worldPosition).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr || distanceSqr >= closestDistanceSqr)
            {
                continue;
            }

            closestDistanceSqr = distanceSqr;
            projectile = candidate;
        }

        return projectile != null;
    }

    public void NotifyBirdCollision(Vector3 collisionPoint)
    {
        if (!isRunning)
        {
            return;
        }

        failedFeeds += 1;
        ApplyFailurePenalty();
        lastAttemptSucceeded = false;
        lastFailureReason = FeedFailureReason.BirdCollision;
        lastFeedbackMessage = BuildFailureMessage(FeedFailureReason.BirdCollision);
    }

    private void EndGame(bool completedRound)
    {
        if (!isRunning && !completedRound)
        {
            return;
        }

        isRunning = false;

        if (completedRound)
        {
            float elapsedSeconds = Mathf.Max(0f, GetActivePatternRoundDuration() - remainingTime);
            SaveBestTimeIfNeeded(elapsedSeconds);
            lastFeedbackMessage = "Round complete.";
        }
        else if (remainingTime <= 0f)
        {
            lastFeedbackMessage = "Time up.";
        }

        ClearActiveProjectiles();
    }

    private void CompleteRound()
    {
        EndGame(true);

        if (!autoAdvanceToNextPattern)
        {
            return;
        }

        if (patterns == null || currentPatternIndex >= patterns.Length - 1)
        {
            return;
        }

        ApplyPattern(currentPatternIndex + 1, true);
    }

    private void ApplyPattern(int patternIndex, bool restartRound)
    {
        ResolveReferences();
        EnsurePatternSetups();

        if (patterns.Length == 0)
        {
            currentPatternIndex = 0;
            return;
        }

        currentPatternIndex = Mathf.Clamp(patternIndex, 0, patterns.Length - 1);

        if (birdPatternGroup != null)
        {
            birdPatternGroup.SetPattern(patterns[currentPatternIndex].patternType);
        }

        LoadBestTimeForCurrentPattern();

        if (restartRound)
        {
            BeginGame();
        }
    }

    private void EnsurePatternSetups()
    {
        if (patterns != null && patterns.Length > 0)
        {
            return;
        }

        patterns = new[]
        {
            CreatePatternSetup("Pattern 1 - Dive Feed", BirdRoundPattern.DiveFeed),
            CreatePatternSetup("Pattern 2 - Circle & Snatch", BirdRoundPattern.CircleAndSnatch),
            CreatePatternSetup("Pattern 3 - Swarm Panic", BirdRoundPattern.SwarmPanic),
            CreatePatternSetup("Pattern 4 - Aggressive Hunger", BirdRoundPattern.AggressiveHunger),
        };
    }

    private PatternSetup CreatePatternSetup(string displayName, BirdRoundPattern patternType)
    {
        return new PatternSetup
        {
            displayName = displayName,
            patternType = patternType,
            targetFeeds = defaultTargetFeeds,
            roundDuration = roundDuration,
        };
    }

    private void ClearActiveProjectiles()
    {
        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            BreadProjectile projectile = activeProjectiles[i];
            if (projectile == null)
            {
                continue;
            }

            Destroy(projectile.gameObject);
        }

        activeProjectiles.Clear();
        legacySwarmBirds.Clear();
    }

    private void ResetAllTargets()
    {
        FeedingTarget[] feedingTargets = FindObjectsByType<FeedingTarget>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < feedingTargets.Length; i++)
        {
            feedingTargets[i].ResetTargetState();
        }
    }

    private int GetActivePatternTargetFeeds()
    {
        int targetFeeds;

        if (patterns == null || patterns.Length == 0)
        {
            targetFeeds = Mathf.Max(1, defaultTargetFeeds);
        }
        else
        {
            targetFeeds = Mathf.Max(1, patterns[currentPatternIndex].targetFeeds);
        }

        if (birdPatternGroup != null && birdPatternGroup.BirdCount > 0)
        {
            targetFeeds = Mathf.Min(targetFeeds, birdPatternGroup.BirdCount);
        }

        return Mathf.Max(1, targetFeeds);
    }

    private float GetActivePatternRoundDuration()
    {
        if (patterns == null || patterns.Length == 0)
        {
            return Mathf.Max(1f, roundDuration);
        }

        return Mathf.Max(1f, patterns[currentPatternIndex].roundDuration);
    }

    private string GetActivePatternName()
    {
        if (patterns == null || patterns.Length == 0)
        {
            return "Pattern";
        }

        return string.IsNullOrWhiteSpace(patterns[currentPatternIndex].displayName)
            ? "Pattern"
            : patterns[currentPatternIndex].displayName;
    }

    private string BuildFailureMessage(FeedFailureReason failureReason)
    {
        switch (failureReason)
        {
            case FeedFailureReason.FeedWindowClosed:
                return "Missed timing window.";
            case FeedFailureReason.TargetUnavailable:
                return "Target is not feedable right now.";
            case FeedFailureReason.LifetimeExpired:
                return "Bread expired before reaching a target.";
            case FeedFailureReason.BirdCollision:
                return "Birds panicked after colliding.";
            case FeedFailureReason.MissedThrow:
            default:
                return "Bread missed the target.";
        }
    }

    private void ApplyFailurePenalty()
    {
        currentEfficiency = Mathf.Max(0f, currentEfficiency - failurePenalty);
    }

    private void LoadBestTimeForCurrentPattern()
    {
        bestCompletionTimeSeconds = PlayerPrefs.GetFloat(GetBestTimePrefsKey(), -1f);
    }

    private void SaveBestTimeIfNeeded(float elapsedSeconds)
    {
        if (bestCompletionTimeSeconds >= 0f && elapsedSeconds >= bestCompletionTimeSeconds)
        {
            return;
        }

        bestCompletionTimeSeconds = elapsedSeconds;
        PlayerPrefs.SetFloat(GetBestTimePrefsKey(), bestCompletionTimeSeconds);
        PlayerPrefs.Save();
    }

    private string GetBestTimePrefsKey()
    {
        return bestTimePrefsKeyPrefix + "_" + gameObject.scene.name + "_" + currentPatternIndex;
    }

    private void ResolveReferences()
    {
        if (birdPatternGroup == null)
        {
            birdPatternGroup = FindFirstObjectByType<BirdPatternGroupController>();
        }
    }

    private void OnValidate()
    {
        MigrateDebugHotkey();

        if (startingEfficiency < 0f)
        {
            startingEfficiency = 0f;
        }

        if (startingEfficiency > 100f)
        {
            startingEfficiency = 100f;
        }

        if (defaultTargetFeeds < 1)
        {
            defaultTargetFeeds = 1;
        }

        if (roundDuration < 1f)
        {
            roundDuration = 1f;
        }

        if (startingPatternIndex < 0)
        {
            startingPatternIndex = 0;
        }
    }

    private void MigrateDebugHotkey()
    {
        if (debugNextPatternKey == KeyCode.None || debugNextPatternKey == KeyCode.Space)
        {
            debugNextPatternKey = KeyCode.Alpha1;
        }
    }
}
