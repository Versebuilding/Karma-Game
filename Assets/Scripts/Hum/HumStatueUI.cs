using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// View layer for the hum statue screen. Wraps the colleague's Hum prefab
/// (3 orbs: each with a liquid Fill driven by OrbFillUI, 8–12 "Lightset"
/// dot pairs flanking the orb, and a Star on top).
///
/// Replaces the prefab's test-mode Hud_Controller. Light sets are
/// discovered by name ("Lightset*"), bottom-to-top in hierarchy order, so
/// the dots rise and fall with the liquid level.
/// </summary>
public class HumStatueUI : MonoBehaviour
{
    [Tooltip("Orb roots in SESSION ORDER (first orb to play first). Each needs an OrbFillUI.")]
    [SerializeField] private GameObject[] orbs;

    private OrbFillUI[] _fills;
    private List<CanvasGroup>[] _lightSets;
    private CanvasGroup[] _stars;
    private CanvasGroup[] _orbGroups;

    public int OrbCount => orbs?.Length ?? 0;

    public void Initialize()
    {
        int n = orbs.Length;
        _fills = new OrbFillUI[n];
        _lightSets = new List<CanvasGroup>[n];
        _stars = new CanvasGroup[n];
        _orbGroups = new CanvasGroup[n];

        for (int i = 0; i < n; i++)
        {
            _fills[i] = orbs[i].GetComponent<OrbFillUI>();
            _orbGroups[i] = orbs[i].GetComponent<CanvasGroup>();
            if (_orbGroups[i] == null) _orbGroups[i] = orbs[i].AddComponent<CanvasGroup>();

            _lightSets[i] = new List<CanvasGroup>();
            foreach (Transform child in orbs[i].transform)
            {
                if (child.name.StartsWith("Lightset"))
                {
                    var cg = child.GetComponent<CanvasGroup>();
                    if (cg != null) _lightSets[i].Add(cg);
                }
                else if (child.name == "Star" && child.childCount > 0)
                {
                    _stars[i] = child.GetChild(0).GetComponent<CanvasGroup>();
                }
            }
        }
        ResetAll();
    }

    public void ResetAll()
    {
        for (int i = 0; i < orbs.Length; i++)
        {
            SetFill(i, 0f);
            SetDots(i, 0f);
            SetStarLit(i, false);
            SetOrbDimmed(i, true);
        }
    }

    /// <summary>Liquid level 0..1 on one orb.</summary>
    public void SetFill(int orb, float fill01)
    {
        if (_fills[orb] != null) _fills[orb].SetFill(fill01);
    }

    /// <summary>
    /// Light the side dots in step with the liquid: the bottom
    /// (fill * count) dot pairs are on, the rest off — so dots rise as the
    /// liquid rises and fall as it drops.
    /// </summary>
    public void SetDots(int orb, float fill01)
    {
        var sets = _lightSets[orb];
        float lit = fill01 * sets.Count;
        for (int i = 0; i < sets.Count; i++)
        {
            // Fractional edge so the topmost active dot fades in instead
            // of popping — reads as the liquid "reaching" it.
            sets[i].alpha = Mathf.Clamp01(lit - i);
        }
    }

    public void SetStarLit(int orb, bool lit)
    {
        if (_stars[orb] != null) _stars[orb].alpha = lit ? 1f : 0f;
    }

    public void SetStarAlpha(int orb, float a)
    {
        if (_stars[orb] != null) _stars[orb].alpha = Mathf.Clamp01(a);
    }

    /// <summary>Inactive orbs sit dimmed so the active one reads clearly.</summary>
    public void SetOrbDimmed(int orb, bool dimmed)
    {
        _orbGroups[orb].alpha = dimmed ? 0.35f : 1f;
    }

    /// <summary>
    /// Breath-out flicker: every dot on every orb shimmers (alpha follows
    /// a per-dot offset sine) — the whole statue "breathes".
    /// </summary>
    public void FlickerAllLights(float time, float intensity01)
    {
        for (int o = 0; o < orbs.Length; o++)
        {
            var sets = _lightSets[o];
            for (int i = 0; i < sets.Count; i++)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(time * 9f + i * 0.9f + o * 2.1f);
                sets[i].alpha = Mathf.Lerp(sets[i].alpha, wave, intensity01);
            }
        }
    }

    /// <summary>Fade every light toward 0 (end of the breath phase).</summary>
    public void FadeAllLights(float dt, float rate = 3f)
    {
        for (int o = 0; o < orbs.Length; o++)
            foreach (var cg in _lightSets[o])
                cg.alpha = Mathf.Lerp(cg.alpha, 0f, dt * rate);
    }

    public RectTransform GetStarRect(int orb)
    {
        return _stars[orb] != null ? (RectTransform)_stars[orb].transform : (RectTransform)orbs[orb].transform;
    }

    public RectTransform GetOrbRect(int orb) => (RectTransform)orbs[orb].transform;

#if UNITY_EDITOR
    public void EditorSetOrbs(GameObject[] o) => orbs = o;
#endif
}
