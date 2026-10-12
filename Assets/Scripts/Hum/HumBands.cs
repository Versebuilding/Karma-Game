using UnityEngine;

/// <summary>
/// The four hum tiers (same tuning as the My Friend Riley hum dial).
/// A band is matched when the detected pitch is within tolerance of the
/// band center AND the amplitude clears the band's minimum.
/// </summary>
public enum HumBand { Low, Mid, High, Loud }

public static class HumBands
{
    // centerHz, toleranceHz, minAmplitude01 — tuned values from Riley's
    // HumDialBandSet_Default.asset.
    public static readonly float[] CenterHz     = { 165f, 230f, 320f, 420f };
    public static readonly float[] ToleranceHz  = {  30f,  30f,  35f,  45f };
    public static readonly float[] MinAmplitude = { 0.08f, 0.10f, 0.12f, 0.20f };

    public static readonly Color[] BandColor =
    {
        new Color(1f,   0.55f, 0.20f), // Low  — orange
        new Color(0.30f, 0.80f, 0.65f), // Mid  — teal
        new Color(0.30f, 0.55f, 0.95f), // High — blue
        new Color(0.65f, 0.35f, 0.90f), // Loud — purple
    };

    public static float Center(HumBand b)    => CenterHz[(int)b];
    public static Color ColorOf(HumBand b)   => BandColor[(int)b];

    public static bool IsMatched(HumBand band, float pitchHz, float amp01)
    {
        int i = (int)band;
        return amp01 >= MinAmplitude[i]
            && Mathf.Abs(pitchHz - CenterHz[i]) <= ToleranceHz[i];
    }

    /// <summary>0..1 closeness of pitch to the band center (1 = dead on).</summary>
    public static float Proximity01(HumBand band, float pitchHz)
    {
        int i = (int)band;
        float d = Mathf.Abs(pitchHz - CenterHz[i]);
        return Mathf.Clamp01(1f - d / (ToleranceHz[i] * 2f));
    }
}
