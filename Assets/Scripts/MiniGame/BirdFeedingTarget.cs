using UnityEngine;

public class BirdFeedingTarget : FeedingTarget
{
    [Header("Bird")]
    [SerializeField] private BirdDiveMovement birdDiveMovement;

    public override bool IsFeedableNow => base.IsFeedableNow && IsBirdFeedWindowOpen();

    protected override bool CanBeFed(Component feedSource)
    {
        return IsBirdFeedWindowOpen();
    }

    private bool IsBirdFeedWindowOpen()
    {
        if (birdDiveMovement == null)
        {
            return true;
        }

        return birdDiveMovement.IsFeedWindowOpen;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        if (birdDiveMovement == null)
        {
            birdDiveMovement = GetComponentInParent<BirdDiveMovement>();
        }
    }
}
