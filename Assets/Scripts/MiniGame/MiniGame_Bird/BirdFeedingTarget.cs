using UnityEngine;

public class BirdFeedingTarget : FeedingTarget
{
    [Header("Bird")]
    [SerializeField] private bool requireFeedWindow;
    [SerializeField] private BirdBase bird;
    [SerializeField] private BirdPatternGroupController birdPatternGroup;

    protected override void Awake()
    {
        base.Awake();
        ResolveBirdReferences();
    }

    protected override bool CanBeFedInternal()
    {
        ResolveBirdReferences();

        if (!requireFeedWindow)
        {
            return true;
        }

        if (birdPatternGroup != null)
        {
            return birdPatternGroup.IsBirdFeedWindowOpen(this);
        }

        if (bird == null)
        {
            return true;
        }

        return bird.IsFeedWindowOpen;
    }

    protected override FeedFailureReason GetFailureReason(Component feedSource)
    {
        ResolveBirdReferences();

        if (!requireFeedWindow)
        {
            return base.GetFailureReason(feedSource);
        }

        if (birdPatternGroup != null && !birdPatternGroup.IsBirdFeedWindowOpen(this))
        {
            return FeedFailureReason.FeedWindowClosed;
        }

        if (bird != null && !bird.IsFeedWindowOpen)
        {
            return FeedFailureReason.FeedWindowClosed;
        }

        return base.GetFailureReason(feedSource);
    }

    public override void ResetTargetState()
    {
        base.ResetTargetState();
        ResolveBirdReferences();

        if (birdPatternGroup != null)
        {
            return;
        }

        if (bird != null)
        {
            bird.ResetBirdState();
        }
    }

    protected override void OnFed(Component feedSource)
    {
        ResolveBirdReferences();

        if (birdPatternGroup != null)
        {
            birdPatternGroup.NotifyBirdFed(this, feedSource);
            return;
        }

        if (bird != null)
        {
            bird.NotifyFed(feedSource);
        }
    }

    protected override void OnMissed(Component feedSource, FeedFailureReason failureReason)
    {
        ResolveBirdReferences();

        if (birdPatternGroup != null)
        {
            birdPatternGroup.NotifyBirdMissed(this, feedSource, failureReason);
            return;
        }

        if (bird != null)
        {
            bird.NotifyMissed(feedSource, failureReason);
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        ResolveBirdReferences();
    }

    private void ResolveBirdReferences()
    {
        if (birdPatternGroup == null)
        {
            birdPatternGroup = GetComponentInParent<BirdPatternGroupController>();
        }

        if (bird == null)
        {
            bird = GetComponentInParent<BirdBase>();
        }
    }
}
