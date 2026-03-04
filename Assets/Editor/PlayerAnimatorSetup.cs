using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Rebuilds the Player Animator Controller with proper state transitions.
/// Run via: Karma > Rebuild Player Animator
///
/// Design:
/// - Locomotion blend tree (Idle/Walk/Run) as default state
/// - Jump plays full clip then returns to Locomotion (IsGrounded)
/// - DoubleJump fires from AnyState on top of Jump (back-to-back jump feel)
/// - No separate Fall/Land states (jump clip includes the full arc)
/// - Crouch, Carry, Push, Climb use bool conditions with return transitions
/// </summary>
public class PlayerAnimatorSetup
{
    private const string AnimRoot = "Assets/3D/Character/stripes/sammy animations/";
    private const string ControllerPath = "Assets/Animation/PlayerAnimatorController.controller";

    [MenuItem("Karma/Rebuild Player Animator")]
    public static void BuildAnimator()
    {
        // ─── Load clips ───
        var idle = LoadClip(AnimRoot + "idle1.fbx", "idle");
        var walk = LoadClip(AnimRoot + "walking.fbx", "walk");
        var run = LoadClip(AnimRoot + "run.fbx", "run");
        var jump = LoadClip(AnimRoot + "jump.fbx", "jump");
        var doubleJump = LoadClip(AnimRoot + "doubleJump.fbx", "doubleJump");
        var crouchFwd = LoadClip(AnimRoot + "crouchForward.fbx", "crouchForward");
        var sitting = LoadClip(AnimRoot + "sitting.fbx", "sitting");
        var carry = LoadClip(AnimRoot + "carry.fbx", "carry");
        var push = LoadClip(AnimRoot + "pushing.fbx", "push");
        var climb = LoadClip(AnimRoot + "climbwall.fbx", "climb");
        var throwClip = LoadClip(AnimRoot + "throw.fbx", "throw");
        var stumble = LoadClip(AnimRoot + "stumble.fbx", "stumble");

        if (idle == null || walk == null)
        {
            Debug.LogError("PlayerAnimatorSetup: Could not load Idle or Walk clips. Aborting.");
            return;
        }

        // ─── Create fresh controller ───
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // ─── Parameters ───
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsCarrying", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsClimbing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsPushing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("DoubleJump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Land", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Throw", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Stumble", AnimatorControllerParameterType.Trigger);

        var rootSM = controller.layers[0].stateMachine;

        // ═══════════════════════════════════════════
        // STATES
        // ═══════════════════════════════════════════

        // --- Locomotion Blend Tree (default) ---
        BlendTree locoTree;
        var locoState = controller.CreateBlendTreeInController("Locomotion", out locoTree, 0);
        locoTree.blendParameter = "Speed";
        locoTree.blendType = BlendTreeType.Simple1D;
        locoTree.useAutomaticThresholds = false;
        locoTree.AddChild(idle, 0f);
        locoTree.AddChild(walk, 0.25f);
        if (run != null) locoTree.AddChild(run, 1.0f);
        rootSM.defaultState = locoState;

        // --- Jump (full jump arc, plays once) ---
        var jumpState = rootSM.AddState("Jump", new Vector3(500, -50, 0));
        jumpState.motion = jump;

        // --- DoubleJump (plays on top of jump, back-to-back) ---
        var djState = rootSM.AddState("DoubleJump", new Vector3(500, 50, 0));
        djState.motion = doubleJump ?? jump;

        // --- Crouch Idle ---
        var crouchIdleState = rootSM.AddState("CrouchIdle", new Vector3(-200, 150, 0));
        crouchIdleState.motion = sitting ?? idle;

        // --- Crouch Walk ---
        var crouchWalkState = rootSM.AddState("CrouchWalk", new Vector3(-200, 250, 0));
        crouchWalkState.motion = crouchFwd ?? walk;
        crouchWalkState.speed = 0.7f;

        // --- Carry ---
        var carryState = rootSM.AddState("Carry", new Vector3(-200, 400, 0));
        carryState.motion = carry ?? walk;

        // --- Push ---
        var pushState = rootSM.AddState("Push", new Vector3(0, 500, 0));
        pushState.motion = push ?? walk;

        // --- Climb ---
        var climbState = rootSM.AddState("Climb", new Vector3(200, 500, 0));
        climbState.motion = climb ?? idle;

        // --- Throw ---
        var throwState = rootSM.AddState("Throw", new Vector3(500, 400, 0));
        throwState.motion = throwClip ?? idle;

        // --- Stumble (plays at boundary limits) ---
        var stumbleState = rootSM.AddState("Stumble", new Vector3(500, 200, 0));
        stumbleState.motion = stumble ?? idle;

        // ═══════════════════════════════════════════
        // TRANSITIONS FROM LOCOMOTION
        // ═══════════════════════════════════════════

        AnimatorStateTransition t;

        // Locomotion → CrouchIdle
        t = locoState.AddTransition(crouchIdleState);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // Locomotion → CrouchWalk
        t = locoState.AddTransition(crouchWalkState);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        // Locomotion → Carry
        t = locoState.AddTransition(carryState);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.If, 0, "IsCarrying");

        // Locomotion → Push
        t = locoState.AddTransition(pushState);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.If, 0, "IsPushing");

        // Locomotion → Climb
        t = locoState.AddTransition(climbState);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.If, 0, "IsClimbing");

        // ═══════════════════════════════════════════
        // TRANSITIONS BACK TO LOCOMOTION
        // ═══════════════════════════════════════════

        // Jump → Locomotion (when grounded again)
        t = jumpState.AddTransition(locoState);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

        // DoubleJump → Locomotion (when grounded again)
        t = djState.AddTransition(locoState);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

        // CrouchIdle → Locomotion
        t = crouchIdleState.AddTransition(locoState);
        t.hasExitTime = false; t.duration = 0.2f;
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

        // CrouchIdle ↔ CrouchWalk
        t = crouchIdleState.AddTransition(crouchWalkState);
        t.hasExitTime = false; t.duration = 0.1f;
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        t = crouchWalkState.AddTransition(crouchIdleState);
        t.hasExitTime = false; t.duration = 0.1f;
        t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // CrouchWalk → Locomotion
        t = crouchWalkState.AddTransition(locoState);
        t.hasExitTime = false; t.duration = 0.2f;
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

        // Carry → Locomotion
        t = carryState.AddTransition(locoState);
        t.hasExitTime = false; t.duration = 0.2f;
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCarrying");

        // Push → Locomotion
        t = pushState.AddTransition(locoState);
        t.hasExitTime = false; t.duration = 0.2f;
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsPushing");

        // Climb → Locomotion
        t = climbState.AddTransition(locoState);
        t.hasExitTime = false; t.duration = 0.2f;
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsClimbing");

        // Throw → Locomotion (after clip finishes)
        t = throwState.AddTransition(locoState);
        t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.15f;

        // Stumble → Locomotion (after clip finishes)
        t = stumbleState.AddTransition(locoState);
        t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.15f;

        // ═══════════════════════════════════════════
        // ANYSTATE TRANSITIONS (Triggers)
        // ═══════════════════════════════════════════

        // AnyState → Jump (first jump)
        var anyT = rootSM.AddAnyStateTransition(jumpState);
        anyT.hasExitTime = false; anyT.duration = 0.05f;
        anyT.canTransitionToSelf = false;
        anyT.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        // AnyState → DoubleJump (fires from Jump state = back-to-back)
        anyT = rootSM.AddAnyStateTransition(djState);
        anyT.hasExitTime = false; anyT.duration = 0.05f;
        anyT.canTransitionToSelf = false;
        anyT.AddCondition(AnimatorConditionMode.If, 0, "DoubleJump");

        // AnyState → Throw
        anyT = rootSM.AddAnyStateTransition(throwState);
        anyT.hasExitTime = false; anyT.duration = 0.05f;
        anyT.canTransitionToSelf = false;
        anyT.AddCondition(AnimatorConditionMode.If, 0, "Throw");

        // AnyState → Stumble (boundary hit)
        anyT = rootSM.AddAnyStateTransition(stumbleState);
        anyT.hasExitTime = false; anyT.duration = 0.1f;
        anyT.canTransitionToSelf = false;
        anyT.AddCondition(AnimatorConditionMode.If, 0, "Stumble");

        // ═══════════════════════════════════════════
        // SAVE
        // ═══════════════════════════════════════════

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== PlayerAnimatorSetup: Rebuild complete! ===");
        Debug.Log("States: Locomotion (BlendTree: Idle/Walk/Run), Jump, DoubleJump, " +
                  "CrouchIdle, CrouchWalk, Carry, Push, Climb, Throw, Stumble");
        Debug.Log("No Fall/Land states — Jump clip plays the full arc.");
        Debug.Log("DoubleJump fires via AnyState on top of Jump (back-to-back).");
        Debug.Log("Stumble fires via AnyState when hitting boundary colliders.");
    }

    private static AnimationClip LoadClip(string path, string debugName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning($"PlayerAnimatorSetup: No asset at '{path}' for [{debugName}]");
            return null;
        }

        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        Debug.LogWarning($"PlayerAnimatorSetup: No AnimationClip in '{path}' for [{debugName}]");
        return null;
    }
}
