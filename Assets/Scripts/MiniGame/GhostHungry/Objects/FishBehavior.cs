using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// FIX: make modular, separate feedable and cycle behavior
// FIX?: move animation to its own script

/* Implement:
- Phase 3 : "Some open only if ignored" - (scalar += delta/factor)
- Overfed decay : currently fedvalue only increases (doesn't make sense), it should decrease to allow falling out of greedy mode (Timer -= delta, <0 decrement)
*/

/// <summary>
/// Behavioral logic for a fish-based entity including the ability to be dynamically spawned and fed in a cyclic manner
/// </summary>
[DisallowMultipleComponent]
public class FishBehavior : MonoBehaviour, IFeedableBehavior, ISpawnInitializable
{
    // VARIABLES:
    // Constants:
    /// <summary>
    /// Length of time (seconds) for the fish's mouth opening/closing animation to fully execute
    /// </summary>
    public const float FISH_MOUTH_ANIMATION_TIME = 0.15F;
    /// <summary>
    /// Variance applied to the startup wait time of a fish's feed cycle
    /// </summary>
    public const float PHASE_OFFSET_VARIANCE = 0.75F; // FIX: move to inspector

    // Shared:
    /// <summary>
    /// Number of concurrent fish mouths open in the scene
    /// </summary>
    static public int CurrentOpenCount { get; private set; } = 0;

    // Runtime:
    public int FedValue { get; private set; } = 0;
    public bool IsFeedable { get; private set; } = false;
    public Coroutine CycleCoroutine { get; private set; }

    // Inspector:
    [Tooltip("The behavior data this fish instance will use in determining actions")]
    [SerializeField] private FishBehaviorDataSO fishData = null;
    [Tooltip("When true, this fish will automatically cycle between closed and open states")]
    [SerializeField] private bool autoCycle = true;
	[Tooltip("")] // FIX: add tooltip
	[SerializeField] private Animator animator; // FIX: add validation

	// Events:
	/// <summary>
	/// Invoked when a fish's mouth opens
	/// </summary>
	[Tooltip("Invoked when the mouth opens")]
    public UnityEvent OnOpen;
    /// <summary>
    /// Invoked when a fish's mouth closes
    /// </summary>
    [Tooltip("Invoked when the mouth closes")]
    public UnityEvent OnClose;
    /// <summary>
    /// Invoked when a fish is fed
    /// </summary>
    [Tooltip("Invoked when the fish is fed")]
    public UnityEvent OnFed; // FIX?: move to IFeedableBehavior

    // Expressions:
    /// <summary>
    /// Has the fish been fed a quantity of food that exceeds its overfeed threshold?
    /// </summary>
    public bool IsOverfed => FedValue > fishData.OverfeedThreshold;
    /// <summary>
    /// Can another fish open its mouth without exceeding the maximum allowed?
    /// </summary>
    public bool IsOpenAllowed => fishData.MaxConcurrentOpens == 0 || CurrentOpenCount < fishData.MaxConcurrentOpens;


    // UNITY PROCESSING:
    void Awake() {
        // ensure events exist (in case no listeners are added in the inspector)
        if (OnOpen == null) OnOpen = new();
        if (OnClose == null) OnClose = new();
        if (OnFed == null) OnFed = new();
    }

    void OnEnable() {
        // ensure instance has behavior data, disable otherwise as it cannot take action
        if (fishData == null) {
            gameObject.SetActive(false);
            return;
        }

        if (autoCycle) {
            StartAutoCycle();
        }
    }

    void OnDisable() {
        StopAutoCycle();
    }


    // FISH-BASED BEHAVIOR:
    // Single Cycle:
    public IEnumerator FeedableFor(float duration) {
        if (duration < FISH_MOUTH_ANIMATION_TIME) yield break; // allowing could cause erroneous behavior

        Open();
        yield return new WaitForSeconds(duration);
        Close();
	}

    // State Changers:
    private void Open() {
        if (IsFeedable) return;

        IsFeedable = true;
        CurrentOpenCount++;

		animator.SetTrigger("Open");
		OnOpen.Invoke();
	}
    
    private void Close(bool signal = true) {
        if (!IsFeedable) return;

        IsFeedable = false;
        CurrentOpenCount--;

        if (signal) {
			animator.SetTrigger("Close");
			OnClose.Invoke();
		}
    }

    // Event Manipulation:
    public void Feed(int value = 1) {
        FedValue += value;

		animator.SetTrigger("Fed");
		OnFed.Invoke();

        Close(signal: false);
    }

    // Utilities:
    private float ApplyVariance(float baseValue, float variance_factor) {
        float variance = baseValue * variance_factor;
        return Mathf.Max(0.01f, baseValue + Random.Range(-variance, variance));
    }


    // CONTROLS:
    public void InitializeSpawnedInstance(ScriptableObject data) { // FIX: too much processing/many operations
        if (data is PhaseSO phaseSO) {
            foreach (ScriptableObject packet in phaseSO.PhaseArguments) {
                if (packet is FishBehaviorDataSO behaviorData) {
                    OverrideFishBehavior(behaviorData);
                }
            }
        }
    }

    /// <summary>
    /// Override <see cref="fishData"/> with new dataset <paramref name="overrides"/>
    /// </summary>
    /// <param name="overrides">The override data for <see cref="fishData"/></param>
    public void OverrideFishBehavior(FishBehaviorDataSO overrides) {
        if (overrides == null) return;

        fishData = overrides;
        // while this is a shallow duplication, because the properties are read-only
        // I'm not as concerned with the typical dangers/errors
    }

    public void ResetBehavior() {
        FedValue = 0;

        if (IsFeedable) Close(); // FIX?: don't invoke close signal

        if (isActiveAndEnabled) {
            if (autoCycle) StartAutoCycle();
            else StopAutoCycle();
        }
    }


    // AUTO-CYCLE:
    public void StartAutoCycle() {
        StopAutoCycle();

        if (isActiveAndEnabled) CycleCoroutine = StartCoroutine(AutoCycle());
    }

    public void StopAutoCycle() {
        if (CycleCoroutine != null) {
            StopCoroutine(CycleCoroutine);
            CycleCoroutine = null;
        }
    }

    public void SetAutoCycle(bool enabled) {
        if (autoCycle != (autoCycle = enabled) && isActiveAndEnabled) { // left-to-right evaluation (old != new), ensures that the value did actually change
            if (autoCycle) StartAutoCycle();
            else StopAutoCycle();
        }
    }

    public IEnumerator AutoCycle() {
        // Phase Offset:
        float duration = ApplyVariance(fishData.ClosedDuration, PHASE_OFFSET_VARIANCE);
        yield return new WaitForSeconds(duration);

        // Cycle: closed -openable?> fakeout | open -<
        while (isActiveAndEnabled) {
            // Closed Logic:
            duration = ApplyVariance(fishData.ClosedDuration, fishData.RhythmVariance);
            if (fishData.GreedyModeActive && IsOverfed) {
                duration *= fishData.ClosedMultiplier;
            }

            yield return new WaitForSeconds(duration);

            // Open Logic:
            if (!IsOpenAllowed) {
                continue;
            }

            if (Random.value < fishData.FakeoutChance) {
                duration = fishData.FakeoutDuration;
            }
            else {
                duration = ApplyVariance(fishData.OpenDuration, fishData.RhythmVariance);
                if (fishData.GreedyModeActive && IsOverfed) {
                    duration *= fishData.OpenMultiplier;
                }
            }

            yield return FeedableFor(duration);
        }
    }
}