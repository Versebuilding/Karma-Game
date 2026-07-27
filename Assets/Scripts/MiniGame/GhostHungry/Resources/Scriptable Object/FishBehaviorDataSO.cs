using UnityEngine;

/// <summary>
/// A data container which aggregates fish-based behavior information
/// </summary>
/// <remarks>
/// <b>Primary Use:</b> <see cref="FishBehavior"/> system
/// </remarks>
[CreateAssetMenu(fileName = "NewFishBehaviorDataSO", menuName = "Karma/Entity Behavior/Fish Behavior SO", order = 0)]
public class FishBehaviorDataSO : ScriptableObject
{
    [Header("Limits")]
    [Tooltip("Limits how many fish mouths may be open concurrently for this phase (0 = unlimited)")]
    [SerializeField][Min(0)] private int maxConcurrentOpens = 0;
    /// <summary>
    /// Limit on how many fish mouths may be open concurrently for this phase (0 = unlimited)
    /// </summary>
    public int MaxConcurrentOpens => maxConcurrentOpens;


    [Header("Timing")]
    [Tooltip("Base length of time (seconds) that a fish's mouth will stay open")]
    [SerializeField][Min(FishBehavior.FISH_MOUTH_ANIMATION_TIME)] private float openDuration = 2 * FishBehavior.FISH_MOUTH_ANIMATION_TIME;
    /// <summary>
    /// Base length of time (seconds) that a fish's mouth will stay open
    /// </summary>
    public float OpenDuration => openDuration;

    [Tooltip("Base length of time (seconds) that a fish's mouth will stay closed")]
    [SerializeField][Min(0.01f)] private float closedDuration = 4f;
    /// <summary>
    /// Base length of time (seconds) that a fish's mouth will stay closed
    /// </summary>
    public float ClosedDuration => closedDuration;

    [Tooltip("Variation factor [0-1] for durations, determines the viable offset from the base value (0 = no offset, 1 = total offset)")]
    [SerializeField][Range(0f, 1f)] private float rhythmVariance = 0f;
    /// <summary>
    /// Variation factor for durations, determines the viable offset from the base value
    /// </summary>
    /// <remarks>
    /// <b>Range:</b> [0 - 1] <br/>
    /// - 0 = no offset <br/>
    /// - 1 = total offset
    /// </remarks>
    public float RhythmVariance => rhythmVariance;


    [Header("Fakeout")]
    [Tooltip("Chance factor [0-1] for any fish to perform a fakeout (short open used to 'trick' players)")]
    [SerializeField][Range(0f, 1f)] private float fakeoutChance = 0f;
    /// <summary>
    /// Chance factor for any fish to perform a fakeout
    /// </summary>
    /// <remarks>
    /// <b>Fakeout:</b> A short open phase used to 'trick' players into prematurely throwing<br/>
    /// <b>Range:</b> [0 - 1] <br/>
    /// - 0 = no fakeouts <br/>
    /// - 1 = only fakeouts
    /// </remarks>
    public float FakeoutChance => fakeoutChance;

    [Tooltip("Length of time (seconds) that a fish's mouth will stay open for a fakeout")]
    [SerializeField][Min(FishBehavior.FISH_MOUTH_ANIMATION_TIME)] private float fakeoutDuration = FishBehavior.FISH_MOUTH_ANIMATION_TIME;
    /// <summary>
    /// Length of time (seconds) that a fish's mouth will stay open for a fakeout
    /// </summary>
    /// <remarks>
    /// - Must be a shorter length of time than <see cref="openDuration"/>
    /// </remarks>
    public float FakeoutDuration => fakeoutDuration;


    [Header("Greedy Mode")]
    [Tooltip("Will greedy mode, overfeeding fish, be active during this phase")]
    [SerializeField] private bool greedyModeActive = false;
    public bool GreedyModeActive => greedyModeActive;

    [Tooltip("Feed value at which the fish is considered overfed")]
    [SerializeField][Min(0)] private int overfeedThreshold = 5;
    /// <summary>
    /// If greedy mode is active, <see cref="greedyModeActive"/>, feed value at which the fish is considered overfed
    /// </summary>
    public int OverfeedThreshold => overfeedThreshold;

    [Tooltip("Open duration multiplier once overfed")]
    [SerializeField][Min(0f)] private float openMultiplier = 1;
    /// <summary>
    /// If greedy mode is active, <see cref="greedyModeActive"/>, open duration multiplier once overfed
    /// </summary>
    public float OpenMultiplier => openMultiplier;

    [Tooltip("Closed duration multiplier once overfed")]
    [SerializeField][Min(0f)] private float closedMultiplier = 1;
    /// <summary>
    /// If greedy mode is active, <see cref="greedyModeActive"/>, closed duration multiplier once overfed
    /// </summary>
    public float ClosedMultiplier => closedMultiplier;


#if UNITY_EDITOR
    private void OnValidate() {
        if (openDuration < fakeoutDuration) {
            openDuration = fakeoutDuration;
        }
    }
#endif
}