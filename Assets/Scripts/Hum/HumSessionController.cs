using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Orchestrates a hum statue session:
///
///   E at a statue → screen fades in (80% dark overlay, statue UI)
///   → per orb: hum in that orb's pitch band — the liquid rises and the
///     side dots climb with it; stop humming and both fall back
///   → liquid full → star lights → HOLD the pitch while the circular
///     progress ring fills (Riley hum-dial style)
///   → ring complete → step chime → BREATH-OUT break: every light on the
///     statue flickers ("breathe out…"), then all lights fade off
///   → next orb activates
///   → after the last orb: star burst + completion chime + karma reward,
///     screen fades out, player control returns.
///
/// Orb → band order: Low, Mid, High (one tier per orb, rising pitch).
/// </summary>
public class HumSessionController : MonoBehaviour
{
    public static HumSessionController Instance { get; private set; }

    [Header("Wiring (set by Karma > Build Hum Statue UI)")]
    [SerializeField] private CanvasGroup rootGroup;     // whole hum screen
    [SerializeField] private Image darkOverlay;         // fullscreen black
    [SerializeField] private HumStatueUI statueUI;
    [SerializeField] private HumHoldRing holdRing;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip stepClip;        // ring complete (per orb)
    [SerializeField] private AudioClip completeClip;    // session complete

    [Header("Tuning")]
    [SerializeField] private float overlayAlpha = 0.8f;
    [SerializeField] private float fadeSeconds = 0.4f;
    [SerializeField] private float fillSeconds = 2.5f;      // hum time to fill an orb
    [SerializeField] private float drainSeconds = 1.6f;     // fall when silent
    [SerializeField] private float holdSeconds = 1.25f;     // ring hold (Riley value)
    [SerializeField] private float breathOutSeconds = 3.0f; // flicker phase
    [SerializeField] private float lightsOffSeconds = 1.2f; // fade-to-dark after flicker
    [SerializeField] private int karmaReward = 20;

    [Tooltip("Band each orb listens for, in session order.")]
    [SerializeField] private HumBand[] orbBands = { HumBand.Low, HumBand.Mid, HumBand.High };

    public bool SessionActive { get; private set; }

    private PlayerController _player;
    private Coroutine _session;

    private static readonly string[] BandHint =
    {
        "Hum a LOW, deep note…",
        "Hum a MIDDLE note…",
        "Hum a HIGH note…",
        "Hum your LOUDEST note…",
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.gameObject.SetActive(false);
        }
    }

    public void BeginSession(PlayerController player)
    {
        if (SessionActive) return;
        _player = player;
        _session = StartCoroutine(SessionRoutine());
    }

    private IEnumerator SessionRoutine()
    {
        SessionActive = true;

        // ── Lock the world ───────────────────────────────────────────
        if (_player != null && _player.input != null)
        {
            _player.input.enabled = false;
            _player.velocity = Vector3.zero;
        }
        if (HUDManager.Instance != null) HUDManager.Instance.HideInteractionPrompt();
        if (BackgroundMusicManager.Instance != null) BackgroundMusicManager.Instance.ForceDuck();

        // ── Fade in ──────────────────────────────────────────────────
        rootGroup.gameObject.SetActive(true);
        statueUI.Initialize();
        holdRing.ResetRing();
        holdRing.gameObject.SetActive(false);
        SetOverlay(0f);
        yield return Fade(0f, 1f);

        // ── Orbs, one at a time ──────────────────────────────────────
        int orbCount = statueUI.OrbCount;
        for (int orb = 0; orb < orbCount; orb++)
        {
            HumBand band = orbBands[Mathf.Clamp(orb, 0, orbBands.Length - 1)];
            yield return OrbPhase(orb, band);

            // Breath-out break between orbs AND after the last one —
            // the exhale is part of the practice.
            yield return BreathOutPhase(orb, isLast: orb == orbCount - 1);
        }

        // ── Session complete ─────────────────────────────────────────
        ShowInstruction("");
        HumUIBurst.PlayAtScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                                new Color(1f, 0.86f, 0.45f), 26);
        PlaySfx(completeClip);
        if (KarmaManager.Instance != null) KarmaManager.Instance.AddKarma(karmaReward);
        yield return new WaitForSeconds(1.2f);

        // ── Fade out, unlock ─────────────────────────────────────────
        yield return Fade(1f, 0f);
        rootGroup.gameObject.SetActive(false);

        if (BackgroundMusicManager.Instance != null) BackgroundMusicManager.Instance.ForceUnduck();
        if (_player != null && _player.input != null) _player.input.enabled = true;

        SessionActive = false;
        _session = null;
        // The InteractionDetector re-shows "Press E" on its own next frame.
    }

    /// <summary>Fill the orb by humming its band, then hold for the ring.</summary>
    private IEnumerator OrbPhase(int orb, HumBand band)
    {
        var mic = HumMicInput.Instance;
        if (mic != null) mic.DebugPitchHz = HumBands.Center(band);

        statueUI.SetOrbDimmed(orb, false);
        ShowInstruction(BandHint[(int)band] + (mic != null && mic.UsingDebugInput ? "  (no mic: hold SPACE)" : ""));

        float fill = 0f;
        bool starLit = false;
        float holdProgress = 0f;

        // Position the ring around this orb's star.
        holdRing.rectTransform.SetParent(statueUI.GetStarRect(orb), false);
        holdRing.rectTransform.anchoredPosition = Vector2.zero;
        holdRing.RingColor = HumBands.ColorOf(band);

        while (true)
        {
            float dt = Time.deltaTime;
            bool matched = mic != null
                && mic.IsPitchValid
                && mic.VoiceConfidence01 > 0.4f
                && HumBands.IsMatched(band, mic.PitchHz, mic.Amplitude01);

            if (!starLit)
            {
                // Liquid + dots climb while the hum matches, drain otherwise.
                fill += (matched ? dt / fillSeconds : -dt / drainSeconds);
                fill = Mathf.Clamp01(fill);
                statueUI.SetFill(orb, fill);
                statueUI.SetDots(orb, fill);

                // Star previews faintly as the dots approach it.
                statueUI.SetStarAlpha(orb, Mathf.InverseLerp(0.75f, 1f, fill) * 0.5f);

                if (fill >= 1f)
                {
                    // Dots reached the star → it lights. Now hold the note.
                    starLit = true;
                    statueUI.SetStarLit(orb, true);
                    holdRing.gameObject.SetActive(true);
                    holdRing.ResetRing();
                    ShowInstruction("Hold it…");
                }
            }
            else
            {
                // Hold phase: ring climbs while the hum stays on pitch.
                holdProgress += (matched ? dt : -dt) / holdSeconds;
                holdProgress = Mathf.Clamp01(holdProgress);
                holdRing.fillAmount = holdProgress;

                // If the player drops the note completely, the star unwinds
                // and we return to filling (liquid partially drains).
                if (holdProgress <= 0f && !matched)
                {
                    fill = 0.8f;
                    starLit = false;
                    statueUI.SetStarLit(orb, false);
                    holdRing.gameObject.SetActive(false);
                    ShowInstruction(BandHint[(int)band]);
                    continue;
                }

                if (holdProgress >= 1f)
                {
                    PlaySfx(stepClip);
                    yield return new WaitForSeconds(0.35f); // let the flash land
                    holdRing.gameObject.SetActive(false);
                    yield break;
                }
            }
            yield return null;
        }
    }

    /// <summary>
    /// Breath-out break: all statue lights flicker (cue to exhale), then
    /// every light fades off; the next orb lights up after.
    /// </summary>
    private IEnumerator BreathOutPhase(int completedOrb, bool isLast)
    {
        ShowInstruction("Breathe out…");
        float t = 0f;
        while (t < breathOutSeconds)
        {
            t += Time.deltaTime;
            statueUI.FlickerAllLights(Time.time, 0.25f);
            yield return null;
        }

        // Lights off.
        float f = 0f;
        while (f < lightsOffSeconds)
        {
            f += Time.deltaTime;
            statueUI.FadeAllLights(Time.deltaTime, 4f);
            yield return null;
        }

        if (!isLast)
        {
            // Completed orb keeps its star but dims; stage goes dark for
            // a beat so the next orb "waking up" reads clearly.
            statueUI.SetOrbDimmed(completedOrb, true);
            statueUI.SetFill(completedOrb, 1f);
            yield return new WaitForSeconds(0.4f);
        }
    }

    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / fadeSeconds);
            rootGroup.alpha = a;
            SetOverlay(a * overlayAlpha);
            yield return null;
        }
        rootGroup.alpha = to;
        SetOverlay(to * overlayAlpha);
    }

    private void SetOverlay(float alpha)
    {
        if (darkOverlay != null)
        {
            var c = darkOverlay.color; c.a = alpha; darkOverlay.color = c;
        }
    }

    private void ShowInstruction(string msg)
    {
        if (instructionText != null) instructionText.text = msg;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
    }
}
