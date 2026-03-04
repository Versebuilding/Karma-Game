using UnityEditor;
using UnityEngine;

/// <summary>
/// Validates the Player prefab setup and provides one-click fixes.
/// Run via: Karma > Validate Player Setup
///
/// Checks:
///   - PlayerController + PlayerInputHandler components
///   - PlayerAnimationHandler on child mesh
///   - InteractionDetector (SphereCollider trigger)
///   - CarryPoint (empty child transform for held objects)
///   - Player tag ("Player")
///   - Animator Controller assigned
///   - CharacterController settings
/// </summary>
public class PlayerSetupValidator
{
    [MenuItem("Karma/Validate Player Setup")]
    public static void ValidateSetup()
    {
        // Find the Player in scene (by tag or by component)
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            // Try finding by old script
            var oldScript = Object.FindFirstObjectByType<ThirdPersonMove>();
            if (oldScript != null) player = oldScript.gameObject;
        }
        if (player == null)
        {
            // Try finding by new script
            var newScript = Object.FindFirstObjectByType<PlayerController>();
            if (newScript != null) player = newScript.gameObject;
        }

        if (player == null)
        {
            Debug.LogError("=== Player Setup Validator ===\n" +
                "Could not find the Player object in the scene!\n" +
                "Make sure the Player prefab is in the scene.");
            return;
        }

        Debug.Log($"=== Player Setup Validator === (found: '{player.name}')");

        int issues = 0;
        int warnings = 0;

        // ─── Tag ───
        if (player.tag != "Player")
        {
            Debug.LogError($"[FIX NEEDED] Player tag is '{player.tag}' — must be 'Player'.\n" +
                "Ghost NPCs use FindWithTag(\"Player\") to detect you.\n" +
                "Fix: Select Player > Inspector > Tag dropdown > Player");
            issues++;
        }
        else Debug.Log("[OK] Player tag = 'Player'");

        // ─── CharacterController ───
        var cc = player.GetComponent<CharacterController>();
        if (cc == null)
        {
            Debug.LogError("[FIX NEEDED] No CharacterController on Player!");
            issues++;
        }
        else Debug.Log($"[OK] CharacterController (height={cc.height}, center={cc.center}, radius={cc.radius})");

        // ─── PlayerController ───
        var pc = player.GetComponent<PlayerController>();
        if (pc == null)
        {
            Debug.LogWarning("[SETUP NEEDED] PlayerController not found on Player.\n" +
                "This is the new state-machine controller that handles carry/push/climb/crouch.\n" +
                "Fix: Add Component > PlayerController (it will also add PlayerInputHandler).\n" +
                "Then remove the old ThirdPersonMove if present.");
            warnings++;

            // Check if old script is still there
            var oldMove = player.GetComponent<ThirdPersonMove>();
            if (oldMove != null)
            {
                Debug.LogWarning("[INFO] Old ThirdPersonMove is still attached.\n" +
                    "Replace with PlayerController for the full interaction system.\n" +
                    "Steps:\n" +
                    "  1. Add Component > PlayerController\n" +
                    "  2. Add Component > PlayerInputHandler\n" +
                    "  3. Assign the InputActionAsset in PlayerInputHandler\n" +
                    "  4. Remove old ThirdPersonMove\n" +
                    "  5. Set PlayerController.cameraTransform to Main Camera");
            }
        }
        else
        {
            Debug.Log("[OK] PlayerController found");

            // Check sub-components
            if (pc.cameraTransform == null)
            {
                Debug.LogWarning("[SETUP NEEDED] PlayerController.cameraTransform is not assigned.\n" +
                    "Fix: Drag Main Camera into the Camera Transform field.");
                warnings++;
            }

            if (pc.carryPoint == null)
            {
                Debug.LogWarning("[SETUP NEEDED] PlayerController.carryPoint is not assigned.\n" +
                    "This is where picked-up objects attach to the player.\n" +
                    "Fix: Create an empty child GameObject named 'CarryPoint' above the player's head,\n" +
                    "then drag it into PlayerController > Interaction > Carry Point.");
                warnings++;
            }
            else Debug.Log($"[OK] CarryPoint = '{pc.carryPoint.name}'");

            // Check boundary layer
            if (pc.boundaryLayer == 0)
            {
                Debug.LogWarning("[OPTIONAL] Boundary Layer mask is empty.\n" +
                    "Stumble animation won't trigger without boundaries.\n" +
                    "Fix: Create a 'Boundary' layer, assign it to boundary colliders,\n" +
                    "then set PlayerController > Boundary Layer to include that layer.");
                warnings++;
            }
        }

        // ─── PlayerInputHandler ───
        var input = player.GetComponent<PlayerInputHandler>();
        if (input == null)
        {
            Debug.LogWarning("[SETUP NEEDED] PlayerInputHandler not found.\n" +
                "Fix: Add Component > PlayerInputHandler, then assign your InputActionAsset.");
            warnings++;
        }
        else Debug.Log("[OK] PlayerInputHandler found");

        // ─── PlayerAnimationHandler (on child mesh) ───
        var animHandler = player.GetComponentInChildren<PlayerAnimationHandler>();
        if (animHandler == null)
        {
            Debug.LogWarning("[SETUP NEEDED] PlayerAnimationHandler not found on child mesh.\n" +
                "Fix: Select the child object with the Animator (e.g. 'sammy rigged'),\n" +
                "then Add Component > PlayerAnimationHandler.");
            warnings++;
        }
        else Debug.Log($"[OK] PlayerAnimationHandler on '{animHandler.gameObject.name}'");

        // ─── Animator + Controller ───
        var animator = player.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError("[FIX NEEDED] No Animator on Player or child objects!");
            issues++;
        }
        else
        {
            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("[SETUP NEEDED] Animator has no controller assigned.\n" +
                    "Fix: Run Karma > Rebuild Player Animator, then assign\n" +
                    "Assets/Animation/PlayerAnimatorController.controller to the Animator.");
                warnings++;
            }
            else Debug.Log($"[OK] Animator controller = '{animator.runtimeAnimatorController.name}'");
        }

        // ─── InteractionDetector ───
        var detector = player.GetComponentInChildren<InteractionDetector>();
        if (detector == null)
        {
            Debug.LogWarning("[SETUP NEEDED] InteractionDetector not found.\n" +
                "This detects nearby PickupObjects, PushableObjects, and ClimbSurfaces.\n" +
                "Fix: Create an empty child GameObject named 'InteractionDetector',\n" +
                "then Add Component > InteractionDetector.\n" +
                "It auto-creates a trigger SphereCollider for detection range.");
            warnings++;
        }
        else Debug.Log($"[OK] InteractionDetector on '{detector.gameObject.name}'");

        // ─── Summary ───
        Debug.Log($"\n=== Validation Summary ===");
        if (issues == 0 && warnings == 0)
            Debug.Log("All checks passed! Player is fully set up.");
        else
        {
            if (issues > 0) Debug.LogError($"{issues} critical issue(s) need fixing.");
            if (warnings > 0) Debug.LogWarning($"{warnings} setup step(s) needed. See messages above.");
        }

        Debug.Log("\n=== Interaction System Quick Reference ===\n" +
            "CARRY/PICK/DROP/THROW:\n" +
            "  - Add PickupObject component to any object with Rigidbody + Collider\n" +
            "  - Inspector fields: weight, isStackable, stackOffset\n" +
            "  - Player picks up with E, drops with E, throws with Left Click\n" +
            "  - Speeds: PlayerController > carrySpeed, throwForce, throwUpAngle\n\n" +
            "PUSH/PULL:\n" +
            "  - Add PushableObject component to any object with Rigidbody + Collider\n" +
            "  - Inspector fields: friction, canPull\n" +
            "  - Player pushes with W, pulls with S, releases with E\n" +
            "  - Speed: PlayerController > pushPullSpeed\n\n" +
            "CLIMB:\n" +
            "  - Add ClimbSurface component to wall/ledge objects\n" +
            "  - Inspector fields: climbHeight\n" +
            "  - Object's forward direction should face AWAY from the wall\n" +
            "  - Player climbs with WASD, jumps off wall, crouches to drop\n\n" +
            "STUMBLE:\n" +
            "  - Create a 'Boundary' layer in Unity (Edit > Project Settings > Tags & Layers)\n" +
            "  - Set boundary collider objects to this layer\n" +
            "  - Set PlayerController > Boundary Layer to include this layer\n" +
            "  - Stumble plays automatically when player walks into a boundary\n" +
            "  - Inspector fields: stumbleCooldown, stumbleDuration");
    }
}
