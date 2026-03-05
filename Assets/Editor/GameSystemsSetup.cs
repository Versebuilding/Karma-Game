using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool to help set up the dialogue/karma/wallet systems in the scene.
/// Menu: Karma > Setup Game Systems
///
/// Creates the GameManagers object with all required singletons,
/// validates the scene, and provides setup instructions.
/// </summary>
public class GameSystemsSetup
{
    [MenuItem("Karma/Setup Game Systems")]
    public static void SetupGameSystems()
    {
        Debug.Log("═══════════════════════════════════════════════════════");
        Debug.Log("  KARMA GAME SYSTEMS SETUP");
        Debug.Log("═══════════════════════════════════════════════════════");

        // Step 1: Create or find GameManagers object
        SetupGameManagers();

        // Step 2: Validate Player
        ValidatePlayer();

        // Step 3: Check Serna NPC
        ValidateSernaNPC();

        // Step 4: Print Canvas setup instructions
        PrintCanvasInstructions();

        Debug.Log("");
        Debug.Log("═══════════════════════════════════════════════════════");
        Debug.Log("  SETUP COMPLETE — See instructions above!");
        Debug.Log("═══════════════════════════════════════════════════════");
    }

    private static void SetupGameManagers()
    {
        Debug.Log("\n── Step 1: Game Managers ──────────────────────────");

        GameObject managers = GameObject.Find("GameManagers");
        if (managers == null)
        {
            managers = new GameObject("GameManagers");
            Undo.RegisterCreatedObjectUndo(managers, "Create GameManagers");
            Debug.Log("  Created 'GameManagers' GameObject");
        }
        else
        {
            Debug.Log("  Found existing 'GameManagers' GameObject");
        }

        // Add KarmaManager
        if (managers.GetComponent<KarmaManager>() == null)
        {
            Undo.AddComponent<KarmaManager>(managers);
            Debug.Log("  + Added KarmaManager");
        }
        else
        {
            Debug.Log("  KarmaManager already attached");
        }

        // Add WalletManager
        if (managers.GetComponent<WalletManager>() == null)
        {
            Undo.AddComponent<WalletManager>(managers);
            Debug.Log("  + Added WalletManager");
        }
        else
        {
            Debug.Log("  WalletManager already attached");
        }

        // Add DialogueManager
        if (managers.GetComponent<DialogueManager>() == null)
        {
            Undo.AddComponent<DialogueManager>(managers);
            Debug.Log("  + Added DialogueManager");
        }
        else
        {
            Debug.Log("  DialogueManager already attached");
        }

        // Add HUDManager
        if (managers.GetComponent<HUDManager>() == null)
        {
            Undo.AddComponent<HUDManager>(managers);
            Debug.Log("  + Added HUDManager");
        }
        else
        {
            Debug.Log("  HUDManager already attached");
        }

        // Add GhostAtmosphere
        if (managers.GetComponent<GhostAtmosphere>() == null)
        {
            Undo.AddComponent<GhostAtmosphere>(managers);
            Debug.Log("  + Added GhostAtmosphere");
        }
        else
        {
            Debug.Log("  GhostAtmosphere already attached");
        }

        // Add SparkleVFXManager
        if (managers.GetComponent<SparkleVFXManager>() == null)
        {
            Undo.AddComponent<SparkleVFXManager>(managers);
            Debug.Log("  + Added SparkleVFXManager");
        }
        else
        {
            Debug.Log("  SparkleVFXManager already attached");
        }

        // Auto-assign SparkleVFX prefab if it exists
        var sparkleManager = managers.GetComponent<SparkleVFXManager>();
        if (sparkleManager != null)
        {
            var prefabGuids = AssetDatabase.FindAssets("SparkleVFX t:Prefab");
            if (prefabGuids.Length > 0)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(prefabGuids[0]));
                if (prefab != null)
                {
                    var smSo = new SerializedObject(sparkleManager);
                    var prefabProp = smSo.FindProperty("sparklePrefab");
                    if (prefabProp != null)
                    {
                        prefabProp.objectReferenceValue = prefab;
                        smSo.ApplyModifiedProperties();
                        Debug.Log($"  Assigned SparkleVFX prefab: {prefab.name}");
                    }
                }
            }
            else
            {
                Debug.Log("  No SparkleVFX prefab found. Run: Karma > Create Sparkle VFX Prefab");
            }
        }

        // Try to assign KarmaConfig
        var karmaManager = managers.GetComponent<KarmaManager>();
        if (karmaManager != null)
        {
            var configGuids = AssetDatabase.FindAssets("t:KarmaConfig");
            if (configGuids.Length > 0)
            {
                var config = AssetDatabase.LoadAssetAtPath<KarmaConfig>(
                    AssetDatabase.GUIDToAssetPath(configGuids[0]));
                if (config != null)
                {
                    var so = new SerializedObject(karmaManager);
                    var configProp = so.FindProperty("config");
                    if (configProp != null)
                    {
                        configProp.objectReferenceValue = config;
                        so.ApplyModifiedProperties();
                        Debug.Log($"  Assigned KarmaConfig: {config.name}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("  ! No KarmaConfig asset found. Run: Karma > Create Karma Config");
            }
        }

        Selection.activeGameObject = managers;
    }

    private static void ValidatePlayer()
    {
        Debug.Log("\n── Step 2: Player Validation ──────────────────────");

        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("  ! No GameObject with tag 'Player' found!");
            Debug.LogWarning("    → Select your player object → Inspector → Tag → Player");
            return;
        }

        Debug.Log($"  Found player: {player.name}");

        // Check PlayerController
        var pc = player.GetComponent<PlayerController>();
        if (pc == null)
        {
            Debug.LogWarning("  ! Player is missing PlayerController component!");
            Debug.LogWarning("    → If using ThirdPersonMove, replace with PlayerController");
        }
        else
        {
            Debug.Log("  PlayerController: OK");
        }

        // Check InteractionDetector
        var detector = player.GetComponentInChildren<InteractionDetector>();
        if (detector == null)
        {
            Debug.LogWarning("  ! Player is missing InteractionDetector!");
            Debug.LogWarning("    → Create child object 'InteractionZone' → Add InteractionDetector");
        }
        else
        {
            Debug.Log($"  InteractionDetector: OK (on {detector.gameObject.name})");

            // Verify Rigidbody (needed for trigger events to fire)
            var detectorRb = detector.GetComponent<Rigidbody>();
            if (detectorRb == null)
            {
                Debug.LogWarning("  ! InteractionDetector has no Rigidbody!");
                Debug.LogWarning("    → InteractionDetector auto-adds one at runtime, but if missing in editor check the script");
            }
            else if (!detectorRb.isKinematic)
            {
                Debug.LogWarning("  ! InteractionDetector Rigidbody should be kinematic!");
            }
            else
            {
                Debug.Log("  InteractionDetector Rigidbody: OK (kinematic)");
            }

            // Verify SphereCollider (trigger)
            var detectorCol = detector.GetComponent<SphereCollider>();
            if (detectorCol == null)
            {
                Debug.Log("  InteractionDetector SphereCollider: auto-created at runtime");
            }
            else if (!detectorCol.isTrigger)
            {
                Debug.LogWarning("  ! InteractionDetector SphereCollider must be a trigger!");
            }
            else
            {
                Debug.Log($"  InteractionDetector SphereCollider: OK (radius={detectorCol.radius})");
            }
        }

        // Check PlayerInputHandler
        var input = player.GetComponent<PlayerInputHandler>();
        if (input == null)
        {
            Debug.LogWarning("  ! Player is missing PlayerInputHandler!");
        }
        else
        {
            Debug.Log("  PlayerInputHandler: OK");
        }
    }

    private static void ValidateSernaNPC()
    {
        Debug.Log("\n── Step 3: Serna NPC ──────────────────────────────");

        // Find Serna by name
        GameObject serna = null;
        var allObjects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in allObjects)
        {
            if (t.name.ToLower().Contains("serna"))
            {
                serna = t.gameObject;
                break;
            }
        }

        if (serna == null)
        {
            Debug.LogWarning("  ! No 'Serna' object found in scene.");
            Debug.LogWarning("    → Ensure Serna is in the scene hierarchy");
            return;
        }

        Debug.Log($"  Found Serna: {serna.name}");

        // Check for old SernaInteraction
        var oldInteraction = serna.GetComponent<SernaInteraction>();
        if (oldInteraction != null)
        {
            Debug.LogWarning("  ! Serna has old SernaInteraction component.");
            Debug.LogWarning("    → Replace with DialogueNPC component:");
            Debug.LogWarning("      1. Note down the current settings");
            Debug.LogWarning("      2. Remove SernaInteraction");
            Debug.LogWarning("      3. Add DialogueNPC");
            Debug.LogWarning("      4. Assign Serna_Intro dialogue asset");
        }

        // Check for DialogueNPC
        var dialogueNPC = serna.GetComponent<DialogueNPC>();
        if (dialogueNPC != null)
        {
            Debug.Log("  DialogueNPC: OK");
        }
        else if (oldInteraction == null)
        {
            Debug.LogWarning("  ! Serna has neither DialogueNPC nor SernaInteraction!");
            Debug.LogWarning("    → Add DialogueNPC component and assign a dialogue asset");
        }

        // Check for Collider (needed for InteractionDetector)
        var collider = serna.GetComponent<Collider>();
        if (collider == null)
        {
            collider = serna.GetComponentInChildren<Collider>();
        }
        if (collider == null)
        {
            Debug.LogWarning("  ! Serna has no Collider! InteractionDetector won't detect her.");
            Debug.LogWarning("    → Add a CapsuleCollider or BoxCollider to Serna");
        }
        else
        {
            Debug.Log($"  Collider: OK ({collider.GetType().Name})");
        }

        // Check for SernaAnimCycler
        var animCycler = serna.GetComponentInChildren<SernaAnimCycler>();
        if (animCycler != null)
        {
            Debug.Log("  SernaAnimCycler: OK (will auto-detect by DialogueNPC)");
        }

        // Check for QuickOutline
        var outline = serna.GetComponentInChildren<QuickOutline>();
        if (outline != null)
        {
            Debug.Log("  QuickOutline: OK (will auto-detect by DialogueNPC)");
        }
        else
        {
            Debug.Log("  No QuickOutline found (optional — add for highlight effect)");
        }
    }

    private static void PrintCanvasInstructions()
    {
        Debug.Log("\n── Step 4: UI Canvas Setup ────────────────────────");
        Debug.Log("  RECOMMENDED: Run 'Karma > Build UI Canvases' to auto-create everything!");
        Debug.Log("");
        Debug.Log("  This builds two canvases + ChoiceButton prefab automatically:");
        Debug.Log("");
        Debug.Log("  A) HUDCanvas (Screen Space - Overlay, Sort Order 5):");
        Debug.Log("     ├── InteractionPrompt (bottom-center, starts hidden)");
        Debug.Log("     ├── KarmaPopup (center, '↑ Karma Score' style)");
        Debug.Log("     ├── KarmaFlower (top-left, icon + progress bar)");
        Debug.Log("     └── CoinCounter (top-right, coin icon + count text)");
        Debug.Log("");
        Debug.Log("  B) DialogueCanvas (Screen Space - Overlay, Sort Order 10):");
        Debug.Log("     └── DialoguePanel (bottom, orange border + cream bg)");
        Debug.Log("         ├── SpeakerBadge (brown badge with name)");
        Debug.Log("         ├── DialogueText (narration text area)");
        Debug.Log("         ├── ChoiceContainer (vertical, above panel)");
        Debug.Log("         └── ContinuePrompt ('Press E to continue')");
        Debug.Log("");
        Debug.Log("  C) ChoiceButton Prefab (auto-saved to Assets/Prefab/UI/):");
        Debug.Log("     ├── InputBadge (orange circle with Z/X/C)");
        Debug.Log("     ├── ChoiceText (choice description)");
        Debug.Log("     └── ChoiceButtonUI (auto-wired)");
        Debug.Log("");
        Debug.Log("  D) NPC Speech Bubble (optional, on each NPC):");
        Debug.Log("     └── Add NPCSpeechBubble component to NPC child object");
        Debug.Log("         (auto-creates world-space canvas with name badge + text)");
        Debug.Log("");
        Debug.Log("  All references auto-wired to HUDManager and DialogueUI!");
    }

    [MenuItem("Karma/Quick Setup Checklist")]
    public static void PrintChecklist()
    {
        Debug.Log("═══════════════════════════════════════════════════════");
        Debug.Log("  KARMA QUICK SETUP CHECKLIST");
        Debug.Log("═══════════════════════════════════════════════════════");
        Debug.Log("");
        Debug.Log("  1. [ ] Run: Karma > Create Karma Config");
        Debug.Log("  2. [ ] Run: Karma > Create Serna Intro Dialogue");
        Debug.Log("  3. [ ] Run: Karma > Create Variable Store");
        Debug.Log("  4. [ ] Run: Karma > Setup Game Systems");
        Debug.Log("  5. [ ] Run: Karma > Build UI Canvases  ← builds everything!");
        Debug.Log("  6. [ ] Run: Karma > Rebuild Player Animator");
        Debug.Log("  7. [ ] Assign KarmaConfig asset to KarmaManager");
        Debug.Log("  8. [ ] On Serna: Replace SernaInteraction → DialogueNPC");
        Debug.Log("  9. [ ] On Serna: Assign Serna_Intro dialogue asset");
        Debug.Log("  10.[ ] On Serna: Ensure she has a Collider (CapsuleCollider)");
        Debug.Log("  11.[ ] (Optional) Add NPCSpeechBubble to Serna child object");
        Debug.Log("  12.[ ] Set Player tag = 'Player' on player object");
        Debug.Log("  13.[ ] Ensure InteractionDetector is on player child object");
        Debug.Log("  14.[ ] Play and test: approach Serna → E → choices → karma/coins");
        Debug.Log("");
        Debug.Log("  Menu shortcuts:");
        Debug.Log("    Karma > Setup Game Systems     — auto-creates managers");
        Debug.Log("    Karma > Build UI Canvases      — builds all UI from Figma mockups");
        Debug.Log("    Karma > Create Karma Config    — creates KarmaConfig asset");
        Debug.Log("    Karma > Create Variable Store  — creates VariableStore asset");
        Debug.Log("    Karma > Create Serna Intro Dialogue — creates dialogue asset");
        Debug.Log("    Karma > Create Serna Return Dialogue — creates return dialogue");
        Debug.Log("    Karma > Rebuild Player Animator — rebuilds animator with stumble");
        Debug.Log("    Karma > Validate Player Setup  — checks player components");
        Debug.Log("");
        Debug.Log("  Content Designer Toolkit:");
        Debug.Log("    Karma > Dialogue Editor        — visual dialogue tree editor");
        Debug.Log("    Karma > Variable Store         — inspect/edit game variables");
        Debug.Log("");
        Debug.Log("  UI Build (individual):");
        Debug.Log("    Karma > Build HUD Canvas Only   — just the HUD canvas");
        Debug.Log("    Karma > Build Dialogue Canvas Only — just the dialogue canvas");
        Debug.Log("═══════════════════════════════════════════════════════");
    }
}
