using Karma.UI.Compass;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor tool that builds the Compass HUD child under the existing HUDCanvas.
/// Menu: Karma > Build Compass HUD
///
/// Uses Unity's built-in UI sprites (UISprite, Knob, Background) as placeholder
/// art so the compass is visually functional immediately. Drop your own sprites
/// into CompassHUDController.defaults / CompassMarkerIcon later to replace them.
/// </summary>
public static class CompassHUDSetup
{
    private const int IconPoolSize = 16;
    private const float BarWidth = 640f;
    private const float BarHeight = 48f;
    private const float ArcAmplitude = 6f;

    // Placeholder tints (used until the user assigns real sprites).
    private static readonly Color BarBgTint      = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color CardinalTint   = new Color(1f, 1f, 1f, 0.85f);
    private static readonly Color PrimaryTint    = new Color(1f, 0.85f, 0.1f, 1f);   // bright yellow
    private static readonly Color SideQuestTint  = new Color(1f, 0.7f, 0.1f, 1f);
    private static readonly Color NpcTint        = new Color(0.6f, 0.85f, 1f, 1f);
    private static readonly Color AltarTint      = new Color(0.8f, 0.55f, 1f, 1f);
    private static readonly Color ShopTint       = new Color(1f, 0.65f, 0.35f, 1f);
    private static readonly Color DiscoveryTint  = new Color(1f, 1f, 1f, 1f);
    private static readonly Color CustomTint     = Color.white;

    [MenuItem("Karma/Build Compass HUD")]
    public static void BuildCompass()
    {
        var hudCanvas = GameObject.Find("HUDCanvas");
        if (hudCanvas == null) hudCanvas = GameObject.Find("HUDCanvas (1)");
        if (hudCanvas == null)
        {
            EditorUtility.DisplayDialog("Compass HUD Setup",
                "No HUDCanvas found in the active scene. Run 'Karma > Build UI Canvases' first, then re-run this.",
                "OK");
            return;
        }

        var existing = hudCanvas.transform.Find("CompassHUD");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Replace Compass HUD?",
                "An existing CompassHUD was found under HUDCanvas. Replace it?",
                "Replace", "Cancel"))
                return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var compass = BuildCompassRoot(hudCanvas.transform);
        Undo.RegisterCreatedObjectUndo(compass, "Create Compass HUD");

        // Wire compass into HUDManager if present.
        var hudManager = Object.FindFirstObjectByType<HUDManager>();
        if (hudManager != null)
        {
            var controller = compass.GetComponent<CompassHUDController>();
            var so = new SerializedObject(hudManager);
            var prop = so.FindProperty("compassHUD");
            if (prop != null)
            {
                prop.objectReferenceValue = controller;
                so.ApplyModifiedProperties();
                Debug.Log("CompassHUDSetup: wired CompassHUDController into HUDManager.compassHUD.");
            }
        }
        else
        {
            Debug.LogWarning("CompassHUDSetup: HUDManager not found — drag the compass into HUDManager.compassHUD manually.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("=== Compass HUD Build Complete ===");
        Debug.Log("  Placeholder icons: built-in Knob sprites tinted per marker type.");
        Debug.Log("  Replace via CompassHUDController.defaults[] in the inspector when real art is ready.");
        Debug.Log("  Next step: add CompassMarker components to NPCs, altars, quest trigger zones, etc.");
    }

    // ─── Builders ────────────────────────────────────────────────

    private static GameObject BuildCompassRoot(Transform parent)
    {
        var root = new GameObject("CompassHUD");
        root.transform.SetParent(parent, false);

        var rect = root.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot     = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -20f);
        rect.sizeDelta = new Vector2(BarWidth, BarHeight);

        // Separate sub-Canvas isolates compass rebuilds from other HUD elements.
        var canvas = root.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 6;
        var group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // Bar background (placeholder: dark translucent rounded rect).
        var bar = CreateUIChild(root.transform, "Bar", Vector2.zero, new Vector2(BarWidth, BarHeight));
        var barImg = bar.AddComponent<Image>();
        barImg.sprite = BuiltinUISprite();
        barImg.type = Image.Type.Sliced;
        barImg.color = BarBgTint;
        barImg.raycastTarget = false;

        // Cardinal labels layer.
        var cardinalRoot = CreateUIChild(root.transform, "CardinalLabels", Vector2.zero, new Vector2(BarWidth, BarHeight));
        var cardinalLabels = new[]
        {
            BuildCardinal(cardinalRoot.transform, "N",   0f),
            BuildCardinal(cardinalRoot.transform, "E",  90f),
            BuildCardinal(cardinalRoot.transform, "S", 180f),
            BuildCardinal(cardinalRoot.transform, "W", 270f)
        };

        // Marker pool root.
        var markerRoot = CreateUIChild(root.transform, "MarkerRoot", Vector2.zero, new Vector2(BarWidth, BarHeight));

        // Build pool prefab (one in-scene template; the pool will Instantiate at runtime).
        var iconPrefabGO = CreateIconTemplate(markerRoot.transform);

        // Attach controller and wire fields.
        var controller = root.AddComponent<CompassHUDController>();
        var so = new SerializedObject(controller);

        so.FindProperty("markerRoot").objectReferenceValue = markerRoot.GetComponent<RectTransform>();
        so.FindProperty("markerIconPrefab").objectReferenceValue = iconPrefabGO;
        so.FindProperty("iconPoolSize").intValue = IconPoolSize;
        so.FindProperty("compassFovDegrees").floatValue = 180f;
        so.FindProperty("arcAmplitude").floatValue = ArcAmplitude;
        so.FindProperty("defaultMaxRange").floatValue = 150f;
        so.FindProperty("fadeBand").floatValue = 25f;
        so.FindProperty("updateHz").floatValue = 25f;

        var cardinalsProp = so.FindProperty("cardinalLabels");
        cardinalsProp.arraySize = cardinalLabels.Length;
        for (int i = 0; i < cardinalLabels.Length; i++)
            cardinalsProp.GetArrayElementAtIndex(i).objectReferenceValue = cardinalLabels[i];

        // Defaults table.
        var defaultsProp = so.FindProperty("defaults");
        var entries = new[]
        {
            (CompassMarkerType.PrimaryQuest, PrimaryTint),
            (CompassMarkerType.SideQuest,    SideQuestTint),
            (CompassMarkerType.NPC,          NpcTint),
            (CompassMarkerType.Altar,        AltarTint),
            (CompassMarkerType.Shop,         ShopTint),
            (CompassMarkerType.Discovery,    DiscoveryTint),
            (CompassMarkerType.CustomIcon,   CustomTint)
        };
        defaultsProp.arraySize = entries.Length;
        var knobSprite = BuiltinKnobSprite();
        for (int i = 0; i < entries.Length; i++)
        {
            var elem = defaultsProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("type").enumValueIndex = (int)entries[i].Item1;
            elem.FindPropertyRelative("sprite").objectReferenceValue = knobSprite;
            elem.FindPropertyRelative("tint").colorValue = entries[i].Item2;
        }

        so.FindProperty("edgeArrowLeft").objectReferenceValue = knobSprite;
        so.FindProperty("edgeArrowRight").objectReferenceValue = knobSprite;

        so.ApplyModifiedProperties();

        return root;
    }

    private static CompassCardinalLabel BuildCardinal(Transform parent, string letter, float yaw)
    {
        var go = new GameObject(letter);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(24f, 24f);

        go.AddComponent<CanvasGroup>();

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = letter;
        text.fontSize = 18;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = CardinalTint;
        text.raycastTarget = false;

        var label = go.AddComponent<CompassCardinalLabel>();
        label.worldYaw = yaw;
        return label;
    }

    private static GameObject CreateIconTemplate(Transform parent)
    {
        var go = new GameObject("CompassIconTemplate");
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(28f, 28f);

        var img = go.AddComponent<Image>();
        img.sprite = BuiltinKnobSprite();
        img.preserveAspect = true;
        img.raycastTarget = false;

        go.AddComponent<CanvasGroup>();
        go.SetActive(false);   // pool copies will be activated on demand

        return go;
    }

    private static GameObject CreateUIChild(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        return go;
    }

    // ─── Built-in Sprite Loaders ─────────────────────────────────

    private static Sprite BuiltinUISprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static Sprite BuiltinKnobSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }
}
