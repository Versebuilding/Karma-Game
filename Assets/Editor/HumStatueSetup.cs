using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Karma > Build Hum Statue UI
///
/// Run in EDIT MODE with the target chapter scene open, then SAVE (Cmd+S).
/// Builds the fullscreen hum statue screen:
///   HumCanvas (overlay, sort 15 — always active, hosts the controller)
///     └── HumScreen (CanvasGroup — hidden until a session starts)
///         ├── DarkOverlay (fullscreen black, 80% during session)
///         ├── Hum (colleague's prefab; Hud_Controller swapped for HumStatueUI)
///         ├── HoldRing (circular hold progress, re-parented to stars at runtime)
///         └── InstructionText (bottom-center cue text)
/// plus a HumMicInput singleton, and HumStatue on the "gargoyle statue".
/// </summary>
public static class HumStatueSetup
{
    [MenuItem("Karma/Build Hum Statue UI")]
    public static void Build()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Hum Statue", "Run this in Edit Mode (not Play Mode), then save the scene.", "OK");
            return;
        }

        // ── Canvas root (always active; hosts HumSessionController) ─
        var canvasGo = GameObject.Find("HumCanvas");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("HumCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15; // above HUD (5) and dialogue (10)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        // ── HumScreen child panel (this is what fades/toggles) ──────
        var screenGo = FindOrCreateChild(canvasGo.transform, "HumScreen");
        Stretch((RectTransform)screenGo.transform);
        var group = screenGo.GetComponent<CanvasGroup>();
        if (group == null) group = screenGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        // ── Dark overlay ────────────────────────────────────────────
        var overlay = FindOrCreateChild(screenGo.transform, "DarkOverlay");
        var overlayImg = overlay.GetComponent<Image>();
        if (overlayImg == null) overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0f);
        Stretch((RectTransform)overlay.transform);

        // ── Hum statue prefab ───────────────────────────────────────
        var humPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Art/Prefab/Hum.prefab");
        if (humPrefab == null)
        {
            EditorUtility.DisplayDialog("Hum Statue", "Assets/UI/Art/Prefab/Hum.prefab not found.", "OK");
            return;
        }
        var humGo = screenGo.transform.Find("Hum")?.gameObject;
        if (humGo == null)
        {
            humGo = (GameObject)PrefabUtility.InstantiatePrefab(humPrefab, screenGo.transform);
            humGo.name = "Hum";
            // Unpack so we can remove the prefab's test controller.
            PrefabUtility.UnpackPrefabInstance(humGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var humRt = (RectTransform)humGo.transform;
            humRt.anchorMin = humRt.anchorMax = new Vector2(0.5f, 0.5f);
            humRt.anchoredPosition = Vector2.zero;
        }

        // Swap the test controller for the real view.
        var testCtrl = humGo.GetComponent<Hud_Controller>();
        if (testCtrl != null) Object.DestroyImmediate(testCtrl);

        var statueUI = humGo.GetComponent<HumStatueUI>();
        if (statueUI == null) statueUI = humGo.AddComponent<HumStatueUI>();
        var red = humGo.transform.Find("Red (1)")?.gameObject;
        var blue = humGo.transform.Find("Blue (1)")?.gameObject;
        var purple = humGo.transform.Find("Purple (1)")?.gameObject;
        if (red == null || blue == null || purple == null)
        {
            EditorUtility.DisplayDialog("Hum Statue",
                "Couldn't find orb children 'Red (1)' / 'Blue (1)' / 'Purple (1)' under the Hum object.", "OK");
            return;
        }
        // Session order = rising pitch: Red (low), Blue (mid), Purple (high).
        statueUI.EditorSetOrbs(new[] { red, blue, purple });

        // ── Hold ring ───────────────────────────────────────────────
        var ringGo = screenGo.transform.Find("HoldRing")?.gameObject;
        if (ringGo == null)
        {
            ringGo = new GameObject("HoldRing", typeof(RectTransform));
            ringGo.transform.SetParent(screenGo.transform, false);
            ((RectTransform)ringGo.transform).sizeDelta = new Vector2(150, 150);
        }
        var ring = ringGo.GetComponent<HumHoldRing>();
        if (ring == null) ring = ringGo.AddComponent<HumHoldRing>();
        ringGo.SetActive(false);

        // ── Instruction text ────────────────────────────────────────
        var textGo = FindOrCreateChild(screenGo.transform, "InstructionText");
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 34;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.97f, 0.9f, 1f);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = new Vector2(0.5f, 0f);
        trt.anchorMax = new Vector2(0.5f, 0f);
        trt.anchoredPosition = new Vector2(0, 110);
        trt.sizeDelta = new Vector2(1200, 80);

        // ── Audio ───────────────────────────────────────────────────
        var sfx = canvasGo.GetComponent<AudioSource>();
        if (sfx == null) sfx = canvasGo.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        var stepClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/hum_step.wav");
        var completeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/piece_unlock.mp3");

        // ── Mic input singleton ─────────────────────────────────────
        if (Object.FindFirstObjectByType<HumMicInput>(FindObjectsInactive.Include) == null)
            new GameObject("HumMicInput").AddComponent<HumMicInput>();

        // ── Session controller (on the always-active canvas root) ──
        var session = canvasGo.GetComponent<HumSessionController>();
        if (session == null) session = canvasGo.AddComponent<HumSessionController>();
        var so = new SerializedObject(session);
        so.FindProperty("rootGroup").objectReferenceValue = group;
        so.FindProperty("darkOverlay").objectReferenceValue = overlayImg;
        so.FindProperty("statueUI").objectReferenceValue = statueUI;
        so.FindProperty("holdRing").objectReferenceValue = ring;
        so.FindProperty("instructionText").objectReferenceValue = tmp;
        so.FindProperty("sfxSource").objectReferenceValue = sfx;
        so.FindProperty("stepClip").objectReferenceValue = stepClip;
        so.FindProperty("completeClip").objectReferenceValue = completeClip;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ── Statue in the world ─────────────────────────────────────
        var statueObj = GameObject.Find("gargoyle statue");
        string statueMsg;
        if (statueObj != null)
        {
            if (statueObj.GetComponent<HumStatue>() == null)
            {
                var hs = statueObj.AddComponent<HumStatue>();
                var promptSo = new SerializedObject(hs);
                promptSo.FindProperty("prompt").stringValue = "Hum";
                promptSo.ApplyModifiedPropertiesWithoutUndo();
            }
            if (statueObj.GetComponent<Collider>() == null)
            {
                var box = statueObj.AddComponent<BoxCollider>();
                box.size = new Vector3(2f, 4f, 2f);
                box.center = new Vector3(0f, 2f, 0f);
            }
            statueMsg = "HumStatue attached to 'gargoyle statue'.";
        }
        else
        {
            statueMsg = "No 'gargoyle statue' found — add the HumStatue component to any statue object manually.";
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        EditorUtility.DisplayDialog("Hum Statue",
            $"Hum statue UI built.\n{statueMsg}\n\nNow SAVE THE SCENE (Cmd+S).", "OK");
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
