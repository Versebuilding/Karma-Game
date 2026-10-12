using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Code-only UI star burst, ported from Riley's UIBurst. Spawns sparks
/// (alternating the sparkle_star sprite and a soft generated dot) that fly
/// out radially with an expanding flash ring, on a dedicated top overlay
/// canvas. Call HumUIBurst.PlayAt(rectTransform, color, count).
/// </summary>
public static class HumUIBurst
{
    private static Canvas _canvas;
    private static Sprite _starSprite;
    private static Sprite _dotSprite;

    public static void PlayAt(RectTransform target, Color color, int count = 22)
    {
        EnsureCanvas();
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, target.position);
        PlayAtScreen(screenPos, color, count);
    }

    public static void PlayAtScreen(Vector2 screenPos, Color color, int count = 22)
    {
        EnsureCanvas();
        var host = new GameObject("Burst");
        host.transform.SetParent(_canvas.transform, false);
        var rt = host.AddComponent<RectTransform>();
        rt.position = screenPos;
        _canvas.GetComponent<MonoBehaviour>().StartCoroutine(BurstRoutine(rt, color, count));
    }

    private static void EnsureCanvas()
    {
        if (_canvas != null) return;
        var go = new GameObject("HumBurstCanvas");
        Object.DontDestroyOnLoad(go);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500;
        go.AddComponent<BurstHost>();
        _starSprite = Resources.Load<Sprite>("sparkle_star");
        _dotSprite = MakeDotSprite();
    }

    // Canvas needs any MonoBehaviour to run coroutines on.
    private class BurstHost : MonoBehaviour { }

    private static IEnumerator BurstRoutine(RectTransform root, Color color, int count)
    {
        float scale = Screen.height / 1080f;
        float life = 1.0f;

        // Flash ring
        var ring = NewSpark(root, _dotSprite, new Color(1f, 1f, 1f, 0.55f), 60f * scale);

        // Sparks
        var sparks = new RectTransform[count];
        var dirs = new Vector2[count];
        var dists = new float[count];
        var spins = new float[count];
        for (int i = 0; i < count; i++)
        {
            Sprite s = (i % 2 == 0 && _starSprite != null) ? _starSprite : _dotSprite;
            float size = Random.Range(22f, 46f) * scale;
            sparks[i] = NewSpark(root, s, color, size);
            float a = Random.value * Mathf.PI * 2f;
            dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            dists[i] = 260f * Random.Range(0.6f, 1.15f) * scale;
            spins[i] = Random.Range(-360f, 360f);
        }

        float t = 0f;
        while (t < life)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / life);
            float ease = 1f - Mathf.Pow(1f - p, 3f);

            float ringSize = Mathf.Lerp(60f, 520f, ease) * scale;
            ring.sizeDelta = new Vector2(ringSize, ringSize);
            SetAlpha(ring, 0.55f * (1f - p));

            float fade = p > 0.6f ? 1f - (p - 0.6f) / 0.4f : 1f;
            for (int i = 0; i < count; i++)
            {
                sparks[i].anchoredPosition = dirs[i] * (dists[i] * ease);
                sparks[i].localRotation = Quaternion.Euler(0, 0, spins[i] * p);
                SetAlpha(sparks[i], fade);
            }
            yield return null;
        }
        Object.Destroy(root.gameObject);
    }

    private static RectTransform NewSpark(RectTransform parent, Sprite sprite, Color color, float size)
    {
        var go = new GameObject("spark");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(size, size);
        return rt;
    }

    private static void SetAlpha(RectTransform rt, float a)
    {
        var img = rt.GetComponent<Image>();
        var c = img.color; c.a = a; img.color = c;
    }

    /// <summary>64px soft dot with a smoothstep falloff, generated in code.</summary>
    private static Sprite MakeDotSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
