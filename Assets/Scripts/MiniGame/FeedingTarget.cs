using UnityEngine;

public abstract class FeedingTarget : MonoBehaviour
{
    [Header("Scoring")]
    [SerializeField] [Min(0)] private int scoreValue = 1;
    [SerializeField] private bool singleUse = false;
    [SerializeField] [Min(0f)] private float feedCooldown = 0.25f;

    [Header("References")]
    [SerializeField] private FeedingMiniGameManager miniGameManager;

    private bool hasBeenFed;
    private float nextAllowedFeedTime;

    public int ScoreValue => scoreValue;
    public bool HasBeenFed => hasBeenFed;

    public virtual bool IsFeedableNow
    {
        get
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }

            if (singleUse && hasBeenFed)
            {
                return false;
            }

            return Time.time >= nextAllowedFeedTime;
        }
    }

    protected virtual void Awake()
    {
        if (miniGameManager == null)
        {
            miniGameManager = GetComponentInParent<FeedingMiniGameManager>();
        }
    }

    public bool TryFeed(Component feedSource)
    {
        if (!IsFeedableNow)
        {
            return false;
        }

        if (!CanBeFed(feedSource))
        {
            return false;
        }

        if (miniGameManager != null && !miniGameManager.TryRegisterFeed(this, feedSource))
        {
            return false;
        }

        hasBeenFed = true;
        nextAllowedFeedTime = Time.time + feedCooldown;

        OnFed(feedSource);
        return true;
    }

    public virtual void ResetTargetState()
    {
        hasBeenFed = false;
        nextAllowedFeedTime = 0f;
    }

    protected abstract bool CanBeFed(Component feedSource);

    protected virtual void OnFed(Component feedSource)
    {
    }

    protected virtual void OnValidate()
    {
        if (miniGameManager == null)
        {
            miniGameManager = GetComponentInParent<FeedingMiniGameManager>();
        }
    }
}
