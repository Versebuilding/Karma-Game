using System.Collections;
using UnityEngine;

// FIX: make modular, separate feedable and cycle behavior

/// <summary>
/// An implementation contract for any feedable object which requires feed cycles
/// </summary>
public interface IFeedableBehavior
{
    // Feeding:
    /// <summary>
    /// Whether the object is currently in a feedable state or not
    /// </summary>
    bool IsFeedable { get; }
    /// <summary>
    /// Total amount, not number of times, the object has been fed
    /// </summary>
    int FedValue { get; }

    /// <summary>
    /// Process a feeding action which recalculates <see cref="FedValue"/> using <paramref name="value"/>
    /// </summary>
    /// <param name="value">Amount for object to be fed (defaults to simple incrementation)</param>
    void Feed(int value = 1);
    /// <summary>
    /// Create a feed state that persists for <paramref name="duration"/> seconds
    /// </summary>
    /// <param name="duration">Length of time (seconds) for the created state to exist</param>
    IEnumerator FeedableFor(float duration);


    // Cycling:
    /// <summary>
    /// State machine which will process each step of the feed cycle
    /// </summary>
    Coroutine CycleCoroutine { get; }

    /// <summary>
    /// Initialize a new cycler which will persist until manually stopped or it falls out of scope (i.e. destroyed/disabled)
    /// </summary>
    void StartAutoCycle();
    /// <summary>
    /// Gracefully terminate any cycler referenced by <see cref="CycleCoroutine"/>
    /// </summary>
    void StopAutoCycle();
    /// <summary>
    /// Set the active state of the cycler to <paramref name="enabled"/>, manipulate <see cref="CycleCoroutine"/> to comply
    /// </summary>
    /// <param name="enabled">Whether the cycler should be active (true) or inactive (false)</param>
    void SetAutoCycle(bool enabled);

    /// <summary>
    /// Persistent cycle logic utilized to build the state machine referenced by <see cref="CycleCoroutine"/>
    /// </summary>
    IEnumerator AutoCycle();


    // Shared:
    /// <summary>
    /// Reinitialize the object, returning it to its default state while restarting, if applicable, the cycling logic
    /// </summary>
    void ResetBehavior();
}

/* Implementation Starter Code:
void OnEnable() {
    if (autoCycle) {
        StartAutoCycle();
    }
}

private void OnDisable() {
    StopAutoCycle();
}

public void StartAutoCycle() {
    StopAutoCycle();
    cycleCoroutine = StartCoroutine(AutoCycle());
}

public void StopAutoCycle() {
    if (cycleCoroutine != null) {
        StopCoroutine(cycleCoroutine);
        cycleCoroutine = null;
    }
}

public void SetAutoCycle(bool enabled) {
    if (autoCycle != (autoCycle = enabled)) { // left-to-right evaluation (old != new), ensures that the value did actually change
        if (autoCycle) StartAutoCycle();
        else StopAutoCycle();
    }
}
*/