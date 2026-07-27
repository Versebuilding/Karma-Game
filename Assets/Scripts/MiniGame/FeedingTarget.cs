using UnityEngine;

public enum FeedAttemptResult
{
    Ignored,
    Success,
    Missed,
}

public enum FeedFailureReason
{
    None,
    FeedWindowClosed,
    TargetUnavailable,
    MissedThrow,
    LifetimeExpired,
    BirdCollision,
}

public abstract class FeedingTarget : MonoBehaviour
{
    [Header("Scoring")]
    [SerializeField] [Min(0)] private int scoreValue = 1;
    [SerializeField] private bool singleUse;
    [SerializeField] [Min(0f)] private float feedCooldown = 0.25f;

    [Header("References")]
    [SerializeField] private FeedingMiniGameManager miniGameManager;

    private bool hasBeenFed;
    private float nextAllowedFeedTime;

    protected FeedingMiniGameManager MiniGameManager => miniGameManager;
    public int ScoreValue => scoreValue;
    public bool HasBeenFed => hasBeenFed;

    protected virtual void Awake()
    {
        ResolveManagerReference();
    }

    public bool CanBeFed()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        if (singleUse && hasBeenFed)
        {
            return false;
        }

        if (Time.time < nextAllowedFeedTime)
        {
            return false;
        }

        return CanBeFedInternal();
    }

    public FeedAttemptResult TryFeed(Component feedSource, out FeedFailureReason failureReason)
    {
        failureReason = FeedFailureReason.None;

        if (!isActiveAndEnabled)
        {
            return FeedAttemptResult.Ignored;
        }

        if (miniGameManager != null && !miniGameManager.CanResolveFeedAttempts)
        {
            return FeedAttemptResult.Ignored;
        }

        if (CanBeFed())
        {
            hasBeenFed = true;
            nextAllowedFeedTime = Time.time + feedCooldown;

            OnFed(feedSource);

            if (miniGameManager != null)
            {
                miniGameManager.RegisterSuccessfulFeed(this, feedSource);
            }

            return FeedAttemptResult.Success;
        }

        failureReason = GetFailureReason(feedSource);
        nextAllowedFeedTime = Time.time + feedCooldown;

        if (miniGameManager != null)
        {
            miniGameManager.RegisterFailedFeed(this, feedSource, failureReason);
        }

        OnMissed(feedSource, failureReason);
        return FeedAttemptResult.Missed;
    }

    public virtual void ResetTargetState()
    {
        hasBeenFed = false;
        nextAllowedFeedTime = 0f;
    }

    protected virtual FeedFailureReason GetFailureReason(Component feedSource)
    {
        if (singleUse && hasBeenFed)
        {
            return FeedFailureReason.TargetUnavailable;
        }

        if (Time.time < nextAllowedFeedTime)
        {
            return FeedFailureReason.TargetUnavailable;
        }

        return FeedFailureReason.TargetUnavailable;
    }

    protected abstract bool CanBeFedInternal();

    protected virtual void OnFed(Component feedSource)
    {
    }

    protected virtual void OnMissed(Component feedSource, FeedFailureReason failureReason)
    {
    }

    protected virtual void OnValidate()
    {
        ResolveManagerReference();
    }

    private void ResolveManagerReference()
    {
        if (miniGameManager == null)
        {
            miniGameManager = GetComponentInParent<FeedingMiniGameManager>();
        }

        if (miniGameManager == null)
        {
            miniGameManager = FindFirstObjectByType<FeedingMiniGameManager>();
        }
    }
}
