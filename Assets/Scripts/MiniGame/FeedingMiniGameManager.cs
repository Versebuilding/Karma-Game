using UnityEngine;

public class FeedingMiniGameManager : MonoBehaviour
{
    [Header("Round")]
    [SerializeField] [Min(1f)] private float roundDuration = 30f;
    [SerializeField] private bool startOnStart = true;

    [Header("Runtime")]
    [SerializeField] private int currentScore;
    [SerializeField] private float remainingTime;
    [SerializeField] private bool isRunning;

    public int CurrentScore => currentScore;
    public float RemainingTime => remainingTime;
    public bool IsRunning => isRunning;

    private void Awake()
    {
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
        EndGame();
    }

    public void BeginGame()
    {
        currentScore = 0;
        remainingTime = Mathf.Max(1f, roundDuration);
        isRunning = true;

        ResetAllTargets();
    }

    public void EndGame()
    {
        isRunning = false;
    }

    public void ResetGame()
    {
        currentScore = 0;
        remainingTime = Mathf.Max(1f, roundDuration);
        isRunning = false;

        ResetAllTargets();
    }

    public bool TryRegisterFeed(FeedingTarget feedingTarget, Component feedSource)
    {
        if (!isRunning || feedingTarget == null)
        {
            return false;
        }

        currentScore += feedingTarget.ScoreValue;
        return true;
    }

    private void ResetAllTargets()
    {
        FeedingTarget[] feedingTargets = GetComponentsInChildren<FeedingTarget>(true);
        for (int i = 0; i < feedingTargets.Length; i++)
        {
            feedingTargets[i].ResetTargetState();
        }
    }
}
