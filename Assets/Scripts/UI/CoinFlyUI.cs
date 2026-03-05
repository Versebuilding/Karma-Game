using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fly-to-wallet coin effect: when coins are gained, spawns 3 gold circle Images
/// at screen center, pops them in, pauses briefly, then staggers each coin flying
/// toward the CoinCounter HUD element (top-right).
///
/// Animation flow:
///   1. Spawn 3 coins at center with random scatter
///   2. Pop-in (0.15s) — scale 0 → 1.0 with ease-out
///   3. Pause  (0.4s)  — hold visible at center
///   4. Stagger-fly (0.5s each, 0.12s delay) — ease-in-out to CoinCounter
///   5. On arrival: destroy coin, pulse the CoinCounter
///
/// Subscribes to WalletManager.OnCoinsChanged (positive deltas only).
///
/// Setup:
///   1. Add to HUDCanvas
///   2. Set flyTarget to the CoinCounter RectTransform
///   3. UISetupTool wires this automatically
/// </summary>
public class CoinFlyUI : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────
    [Header("Fly Target")]
    [Tooltip("The CoinCounter RectTransform to fly toward")]
    [SerializeField] private RectTransform flyTarget;

    // ─── Coin Appearance ─────────────────────────────────────
    [Header("Coin Appearance")]
    [Tooltip("Size of each coin circle in pixels")]
    [Range(16f, 48f)]
    [SerializeField] private float coinSize = 24f;

    [Tooltip("Color of the coin circles")]
    [SerializeField] private Color coinColor = new Color(1f, 0.85f, 0.2f, 1f); // gold

    // ─── Animation Timing ────────────────────────────────────
    [Header("Animation Timing")]
    [Tooltip("Duration of the pop-in effect for each coin")]
    [Range(0.05f, 0.3f)]
    [SerializeField] private float popInDuration = 0.15f;

    [Tooltip("Pause duration at center before flying")]
    [Range(0.1f, 1f)]
    [SerializeField] private float pauseDuration = 0.4f;

    [Tooltip("Duration of the flight to target")]
    [Range(0.2f, 1f)]
    [SerializeField] private float flyDuration = 0.5f;

    [Tooltip("Delay between each coin's flight start")]
    [Range(0.05f, 0.3f)]
    [SerializeField] private float staggerDelay = 0.12f;

    // ─── Spawn Settings ──────────────────────────────────────
    [Header("Spawn")]
    [Tooltip("Number of coins to spawn")]
    [Range(1, 6)]
    [SerializeField] private int coinCount = 3;

    [Tooltip("Random scatter radius around center (pixels)")]
    [Range(0f, 40f)]
    [SerializeField] private float scatterRadius = 15f;

    // ─── Runtime ─────────────────────────────────────────────
    private Canvas parentCanvas;
    private bool isSubscribed;

    // ─── Unity Lifecycle ─────────────────────────────────────

    void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        // Fallback: WalletManager.Instance may be null during OnEnable
        TrySubscribe();
    }

    void OnDisable()
    {
        if (WalletManager.Instance != null)
            WalletManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
        isSubscribed = false;
    }

    private void TrySubscribe()
    {
        if (isSubscribed) return;
        if (WalletManager.Instance == null) return;

        WalletManager.Instance.OnCoinsChanged += HandleCoinsChanged;
        isSubscribed = true;
    }

    // ─── Event Handler ───────────────────────────────────────

    private void HandleCoinsChanged(int newTotal, int delta)
    {
        // Only animate for coin gains, not spending
        if (delta <= 0) return;
        if (parentCanvas == null) return;

        StartCoroutine(SpawnAndFlyCoins());
    }

    // ─── Spawn & Fly ─────────────────────────────────────────

    private IEnumerator SpawnAndFlyCoins()
    {
        // Spawn coins at center screen with random scatter
        var coins = new List<RectTransform>();
        Vector3 screenCenter = new Vector3(
            Screen.width * 0.5f,
            Screen.height * 0.55f,
            0f
        );

        for (int i = 0; i < coinCount; i++)
        {
            var coinObj = new GameObject($"FlyingCoin_{i}");
            coinObj.transform.SetParent(parentCanvas.transform, false);

            var coinRect = coinObj.AddComponent<RectTransform>();
            coinRect.sizeDelta = new Vector2(coinSize, coinSize);

            var coinImage = coinObj.AddComponent<Image>();
            coinImage.color = coinColor;
            coinImage.raycastTarget = false;

            // Position at center with random scatter
            Vector2 scatter = Random.insideUnitCircle * scatterRadius;
            coinRect.position = new Vector3(
                screenCenter.x + scatter.x,
                screenCenter.y + scatter.y,
                0f
            );

            coinRect.localScale = Vector3.zero; // start invisible for pop-in
            coins.Add(coinRect);
        }

        // ═══════════════════════════════════════════════════════
        // Phase 1: Pop-in all coins together (ease-out)
        // ═══════════════════════════════════════════════════════
        float elapsed = 0f;
        while (elapsed < popInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popInDuration);
            // Ease-out: fast start, smooth stop
            float scale = 1f - (1f - t) * (1f - t);

            foreach (var coin in coins)
            {
                if (coin != null)
                    coin.localScale = Vector3.one * scale;
            }
            yield return null;
        }

        foreach (var coin in coins)
        {
            if (coin != null)
                coin.localScale = Vector3.one;
        }

        // ═══════════════════════════════════════════════════════
        // Phase 2: Pause (hold visible at center)
        // ═══════════════════════════════════════════════════════
        yield return new WaitForSeconds(pauseDuration);

        // ═══════════════════════════════════════════════════════
        // Phase 3: Stagger-fly each coin to the CoinCounter
        // ═══════════════════════════════════════════════════════
        for (int i = 0; i < coins.Count; i++)
        {
            if (coins[i] != null)
                StartCoroutine(FlyCoinToTarget(coins[i], i == coins.Count - 1));

            if (i < coins.Count - 1)
                yield return new WaitForSeconds(staggerDelay);
        }
    }

    /// <summary>Fly a single coin from its current position to the flyTarget.</summary>
    private IEnumerator FlyCoinToTarget(RectTransform coin, bool isLastCoin)
    {
        if (flyTarget == null || coin == null)
        {
            if (coin != null) Destroy(coin.gameObject);
            yield break;
        }

        Vector3 start = coin.position;
        Vector3 end = flyTarget.position;

        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            if (coin == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);

            // Ease-in-out: smooth acceleration and deceleration
            float eased = t < 0.5f
                ? 2f * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            coin.position = Vector3.Lerp(start, end, eased);

            // Scale down during flight
            float scale = Mathf.Lerp(1f, 0.4f, t);
            coin.localScale = Vector3.one * scale;

            yield return null;
        }

        // Arrived — destroy this coin
        Destroy(coin.gameObject);

        // Pulse the CoinCounter on the last coin's arrival
        if (isLastCoin)
            StartCoroutine(PunchTargetCoroutine());
    }

    // ─── Target Punch ────────────────────────────────────────

    /// <summary>Pulse the CoinCounter to signal coins arrived.</summary>
    private IEnumerator PunchTargetCoroutine()
    {
        if (flyTarget == null) yield break;

        Vector3 originalScale = flyTarget.localScale;
        float punchAmount = 1.25f;
        float punchDuration = 0.2f;
        float halfDuration = punchDuration * 0.5f;
        float elapsed = 0f;

        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            flyTarget.localScale = originalScale * Mathf.Lerp(1f, punchAmount, t);
            yield return null;
        }

        // Scale back down
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            flyTarget.localScale = originalScale * Mathf.Lerp(punchAmount, 1f, t);
            yield return null;
        }

        flyTarget.localScale = originalScale;
    }
}
