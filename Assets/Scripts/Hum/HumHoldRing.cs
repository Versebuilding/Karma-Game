using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Circular hold-progress ring, ported from Riley's HumDialHoldRing.
/// Draws its own ring mesh (no sprite needed): fill starts at the top and
/// sweeps clockwise. Locks in with a scale-up past 90%, flashes white on
/// completion, and gently pulses while idle.
/// Drive it by setting <see cref="fillAmount"/> and <see cref="RingColor"/>.
/// </summary>
public class HumHoldRing : Image
{
    [Range(0f, 1f)] public float fillAmount;

    /// <summary>Target color (usually the active band color).</summary>
    public Color RingColor = Color.white;

    private const float InnerRadius = 0.85f;
    private const int Segments = 96;

    private float _smoothed;
    private float _flashTimer;
    private float _scale = 1f;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        float prev = _smoothed;
        _smoothed = Mathf.Lerp(_smoothed, fillAmount, dt * 12f);
        if (Mathf.Abs(_smoothed - prev) > 0.003f) SetVerticesDirty();

        color = Color.Lerp(color, RingColor, dt * 6f);

        float targetScale = 1f;
        if (_smoothed >= 0.999f)
        {
            if (_flashTimer <= 0f) _flashTimer = 0.18f;
        }
        if (_flashTimer > 0f)
        {
            _flashTimer -= dt;
            color = Color.Lerp(color, Color.white, 0.6f);
            targetScale = 1.4f + Mathf.Sin(Time.time * Mathf.PI * 2f) * 0.18f;
        }
        else if (_smoothed > 0.9f) targetScale = 1.2f;

        if (_smoothed < 0.01f) { _flashTimer = 0f; targetScale = 1f; }

        _scale = Mathf.Lerp(_scale, targetScale, dt * 12f);
        transform.localScale = Vector3.one * _scale;
    }

    public void ResetRing()
    {
        fillAmount = 0f;
        _smoothed = 0f;
        _flashTimer = 0f;
        _scale = 1f;
        transform.localScale = Vector3.one;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_smoothed <= 0.001f) return;

        Rect r = GetPixelAdjustedRect();
        float outerR = Mathf.Min(r.width, r.height) * 0.5f;
        float innerR = outerR * InnerRadius;
        Vector2 center = r.center;
        float startRad = Mathf.PI * 0.5f; // top
        int segs = Mathf.Max(2, Mathf.CeilToInt(Segments * _smoothed));

        var vert = UIVertex.simpleVert;
        vert.color = color;
        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            float a = startRad - t * _smoothed * Mathf.PI * 2f; // clockwise
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            vert.position = center + dir * outerR; vh.AddVert(vert);
            vert.position = center + dir * innerR; vh.AddVert(vert);
            if (i > 0)
            {
                int o = i * 2;
                vh.AddTriangle(o - 2, o - 1, o);
                vh.AddTriangle(o, o - 1, o + 1);
            }
        }
    }
}
