using System.Collections;
using UnityEngine;

/// <summary>
/// Microphone capture + hum pitch detection for the hum statues.
/// Ported from My Friend Riley's HumTunnelController (StableHumTracker
/// algorithm): RMS amplitude with an adaptive noise floor, soft-knee
/// compression, asymmetric attack/release envelope, voicing hysteresis,
/// and time-domain autocorrelation pitch with parabolic refinement and
/// band snapping.
///
/// Public per-frame outputs:
///   Amplitude01       — perceptual loudness 0..1
///   PitchHz           — smoothed hum pitch (valid while IsPitchValid)
///   VoiceConfidence01 — 1 = clear tonal hum, 0 = breath/noise/silence
///
/// If no microphone exists (or it fails), holding Space simulates a hum
/// at DebugPitchHz so the statue can be tested in the editor.
/// </summary>
public class HumMicInput : MonoBehaviour
{
    public static HumMicInput Instance { get; private set; }

    [Header("Amplitude")]
    [SerializeField] private float micGain = 9f;
    [SerializeField] private float micNoiseFloor = 0.003f;
    [SerializeField] private float noiseFloorMargin = 1.6f;
    [SerializeField] private float noiseLearnRate = 1.0f;
    [SerializeField] private float softKnee = 0.6f;
    [SerializeField] private float ampAttackRate = 18f;
    [SerializeField] private float ampReleaseRate = 1.5f;
    [SerializeField] private float voiceOnThreshold = 0.28f;
    [SerializeField] private float voiceOffThreshold = 0.18f;

    [Header("Pitch")]
    [SerializeField] private float stableHumConfidence = 0.30f;
    [SerializeField] private float pitchSmoothingRate = 8f;
    [SerializeField] private float pitchZoneSnapHz = 35f;
    [SerializeField] private float pitchZoneSnapStrength = 0.20f;
    [SerializeField] private float pitchValiditySeconds = 0.5f;

    public float Amplitude01 { get; private set; }
    public float PitchHz { get; private set; }
    public float VoiceConfidence01 { get; private set; }
    public bool IsPitchValid => Time.time - _lastPitchTime < pitchValiditySeconds;
    public bool MicAvailable => _micClip != null;
    public bool UsingDebugInput { get; private set; }

    /// <summary>Pitch the Space-bar debug hum pretends to sing.</summary>
    public float DebugPitchHz = 165f;

    private const int RmsSampleCount = 2048;

    private AudioClip _micClip;
    private string _micDevice;
    private int _micSampleRate = 44100;
    private readonly float[] _rmsBuffer = new float[RmsSampleCount];

    private float _learnedNoiseRms = 0.003f;
    private bool _voiced;
    private float _lastYinBest = 1f;
    private float _lastPitchTime = -999f;
    private float _micDeadTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() => StartCoroutine(InitMicrophone());

    private IEnumerator InitMicrophone()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("HumMicInput: No microphone found — Space-bar debug hum enabled.");
            UsingDebugInput = true;
            yield break;
        }

        _micDevice = null; // default device
        Microphone.GetDeviceCaps(_micDevice, out int minFreq, out int maxFreq);
        _micSampleRate = (maxFreq >= 16000 && maxFreq <= 48000) ? maxFreq : 44100;

        _micClip = Microphone.Start(_micDevice, true, 1, _micSampleRate);
        if (_micClip == null)
        {
            Debug.LogWarning("HumMicInput: Microphone.Start failed — Space-bar debug hum enabled.");
            UsingDebugInput = true;
            yield break;
        }
        Debug.Log($"HumMicInput: Mic started at {_micSampleRate} Hz.");
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (UsingDebugInput || _micClip == null) { UpdateDebug(dt); return; }

        int writePos = Microphone.GetPosition(_micDevice);
        if (writePos <= 0)
        {
            // Mic not advancing — first-frame latency or a dead session.
            _micDeadTimer += dt;
            if (_micDeadTimer > 4f)
            {
                Debug.LogWarning("HumMicInput: Mic stalled — restarting once.");
                Microphone.End(_micDevice);
                _micClip = Microphone.Start(_micDevice, true, 1, _micSampleRate);
                _micDeadTimer = -10f; // one retry, then give up to debug input
            }
            else if (_micDeadTimer < -5f && _micDeadTimer > -9f)
            {
                UsingDebugInput = true;
            }
            Amplitude01 = Mathf.Lerp(Amplitude01, 0f, dt * ampReleaseRate);
            return;
        }
        _micDeadTimer = 0f;

        int readStart = writePos - RmsSampleCount;
        if (readStart < 0) readStart += _micClip.samples;
        _micClip.GetData(_rmsBuffer, readStart);

        // ── Amplitude ────────────────────────────────────────────────
        float sumSq = 0f;
        for (int i = 0; i < _rmsBuffer.Length; i++) sumSq += _rmsBuffer[i] * _rmsBuffer[i];
        float rms = Mathf.Sqrt(sumSq / _rmsBuffer.Length);

        // Adaptive noise floor: learn ambient quickly on quiet frames,
        // creep slowly otherwise so the player's hum doesn't pollute it.
        _learnedNoiseRms = Mathf.Lerp(_learnedNoiseRms, rms,
            dt * (rms < _learnedNoiseRms * 1.5f ? noiseLearnRate : 0.05f));

        float floor = Mathf.Max(micNoiseFloor, _learnedNoiseRms * noiseFloorMargin);
        float ampLinear = Mathf.Clamp01(Mathf.Sqrt(Mathf.Max(0f, rms - floor)) * micGain);
        float ampTarget = softKnee < 1f ? Mathf.Pow(ampLinear, softKnee) : ampLinear;

        float envRate = ampTarget > Amplitude01 ? ampAttackRate : ampReleaseRate;
        Amplitude01 = Mathf.Lerp(Amplitude01, ampTarget, dt * envRate);

        // ── Voicing gate with hysteresis ─────────────────────────────
        if (_voiced) { if (ampTarget < voiceOffThreshold) _voiced = false; }
        else         { if (ampTarget > voiceOnThreshold)  _voiced = true;  }

        float rawConf = _voiced ? Mathf.Clamp01(1f - _lastYinBest) : 0f;
        VoiceConfidence01 = Mathf.Lerp(VoiceConfidence01, rawConf,
            dt * (rawConf > VoiceConfidence01 ? 14f : 8f));

        // ── Pitch ────────────────────────────────────────────────────
        if (_voiced && TryDetectPitch(_rmsBuffer, _micSampleRate, out float hz))
        {
            // Snap toward the nearest band center so the orbs lock on
            // cleanly instead of wobbling at the edge of a tier.
            float closest = HumBands.CenterHz[0];
            float minDiff = Mathf.Abs(hz - closest);
            for (int i = 1; i < HumBands.CenterHz.Length; i++)
            {
                float d = Mathf.Abs(hz - HumBands.CenterHz[i]);
                if (d < minDiff) { minDiff = d; closest = HumBands.CenterHz[i]; }
            }
            if (minDiff < pitchZoneSnapHz)
                hz = Mathf.Lerp(hz, closest, pitchZoneSnapStrength);

            PitchHz = Mathf.Lerp(PitchHz, hz, dt * pitchSmoothingRate);
            _lastPitchTime = Time.time;
        }
    }

    private void UpdateDebug(float dt)
    {
        bool humming = Input.GetKey(KeyCode.Space);
        float target = humming ? 0.85f : 0f;
        Amplitude01 = Mathf.Lerp(Amplitude01, target, dt * (humming ? ampAttackRate : ampReleaseRate));
        VoiceConfidence01 = Mathf.Lerp(VoiceConfidence01, humming ? 1f : 0f, dt * 10f);
        if (humming)
        {
            PitchHz = Mathf.Lerp(PitchHz, DebugPitchHz, dt * pitchSmoothingRate);
            _lastPitchTime = Time.time;
        }
    }

    /// <summary>
    /// Time-domain autocorrelation ("StableHumTracker"): best correlation
    /// over the 80–400 Hz lag range, confidence-gated, parabolic-refined.
    /// </summary>
    private bool TryDetectPitch(float[] samples, int sampleRate, out float hz)
    {
        hz = 0f;
        int n = samples.Length;
        int minLag = Mathf.Max(2, sampleRate / 400);
        int maxLag = Mathf.Min(n - 2, sampleRate / 80);
        if (maxLag <= minLag) return false;

        float energy = 0f;
        for (int i = 0; i < n; i++) energy += samples[i] * samples[i];
        if (energy < 1e-7f) return false;

        float bestCorr = 0f;
        int bestLag = -1;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            float sum = 0f;
            int upper = n - lag;
            for (int i = 0; i < upper; i++) sum += samples[i] * samples[i + lag];
            if (sum > bestCorr) { bestCorr = sum; bestLag = lag; }
        }
        if (bestLag < 0) return false;

        float confidence = bestCorr / energy;
        _lastYinBest = Mathf.Clamp01(1f - confidence);
        if (confidence < stableHumConfidence) return false;

        float refinedLag = bestLag;
        if (bestLag > minLag && bestLag < maxLag)
        {
            float cM = CorrelationAtLag(samples, bestLag - 1);
            float cP = CorrelationAtLag(samples, bestLag + 1);
            float denom = cM - 2f * bestCorr + cP;
            float offset = Mathf.Approximately(denom, 0f) ? 0f : 0.5f * (cM - cP) / denom;
            refinedLag = bestLag + Mathf.Clamp(offset, -1f, 1f);
        }

        hz = sampleRate / refinedLag;
        return true;
    }

    private static float CorrelationAtLag(float[] samples, int lag)
    {
        float sum = 0f;
        int upper = samples.Length - lag;
        for (int i = 0; i < upper; i++) sum += samples[i] * samples[i + lag];
        return sum;
    }

    private void OnDestroy()
    {
        if (_micClip != null) Microphone.End(_micDevice);
        if (Instance == this) Instance = null;
    }
}
