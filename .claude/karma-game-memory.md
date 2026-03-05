# Karma Project — Technical Memory

## Project Overview
- **Engine**: Unity 6000.0.62f1, Universal Render Pipeline (URP)
- **Game**: RPG themed on 6 Realms of Karma
- **Player**: Sammy (3D character, CharacterController, height 6.5, scale ~3.19)
- **Sidekick**: Cat (planned)
- **Mentor**: Ananda (planned)
- **MVP**: Chapter 1 "Serna and the Empty Cup" (Hungry Ghost Realm)
- **Reference games**: It Takes Two, Little Nightmares. Floaty, dreamy movement feel.
- **Scene**: `Assets/Scenes/Chapter1- Serna.unity`

## Branches
- `main` — stable
- `feature/UI` — teammate's UI + dialogue work (don't touch)
- `claude/flamboyant-tu` — player movement + ghost AI + dialogue systems work
- `feature/serna-level01` — current active branch

---

## Architecture Overview

```
┌──────────── PLAYER ─────────────┐   ┌──────────── MANAGERS ─────────────┐
│ PlayerController                │   │ KarmaManager (singleton)          │
│ ├── PlayerInputHandler          │   │ WalletManager (singleton)         │
│ ├── PlayerAnimationHandler      │   │ DialogueManager (singleton)       │
│ └── PlayerStateMachine          │   │ HUDManager (singleton)            │
│     ├── GroundedState           │   └─────────────────────────────────────┘
│     ├── AirborneState           │              │ events
│     ├── CrouchState             │              ▼
│     ├── CarryState              │   ┌──────────── UI LAYER ──────────────┐
│     ├── PushPullState           │   │ DialogueUI + ChoiceButtonUI        │
│     └── ClimbState              │   │ KarmaFlowerUI + CoinCounterUI     │
└─────────────────────────────────┘   │ KarmaPopupUI                      │
         │ InteractionDetector         └────────────────────────────────────┘
         ▼
┌──────────── NPC LAYER ──────────┐   ┌──────── DATA LAYER ───────────────┐
│ NPCBase (abstract)              │   │ DialogueSO (ScriptableObject)     │
│ DialogueNPC (InteractableBase)  │   │ ├── DialogueNode[]                │
│ GhostNPC (NPCBase)              │   │ │   ├── conditions[]              │
│ GhostFloatEffect                │   │ │   └── onShowActions[]           │
└─────────────────────────────────┘   │ └── DialogueChoice[]              │
                                      │     ├── conditions[]              │
┌──── EXTENSIBILITY FRAMEWORK ────┐   │     └── actions[]                │
│ IDialogueCondition (interface)  │   │ KarmaConfig (ScriptableObject)   │
│ ├── KarmaLevelCondition         │   │ VariableStore (ScriptableObject)  │
│ ├── FlagCondition               │   └────────────────────────────────────┘
│ └── CounterCondition            │
│ IDialogueAction (interface)     │   ┌──── EDITOR TOOLKIT ───────────────┐
│ ├── ModifyKarmaAction           │   │ DialogueEditorWindow              │
│ ├── ModifyCoinsAction           │   │ VariableStoreBrowser              │
│ ├── SetFlagAction               │   │ DialogueSOEditor (CustomEditor)   │
│ └── ModifyCounterAction         │   │ DialogueTypeCache (reflection)    │
└─────────────────────────────────┘   │ DialogueEditorStyles              │
                                      └────────────────────────────────────┘
```

---

## File Inventory

### Player System — `Assets/Scripts/Player/`

| File | Class | Purpose |
|------|-------|---------|
| `PlayerController.cs` | PlayerController | Main player manager. Owns CharacterController, input, animation, state machine. Manages velocity/gravity/jump/stumble. All speed fields: moveSpeed, sprintSpeed, crouchSpeed, pushPullSpeed, climbSpeed (=4f). |
| `PlayerInputHandler.cs` | PlayerInputHandler | New Input System wrapper. Polled input state: MoveInput, LookInput, JumpPressed, SprintHeld, CrouchPressed, InteractPressed. |
| `PlayerAnimationHandler.cs` | PlayerAnimationHandler | Centralized animator param management. HashSet<int> validParams cache (built in Awake from animator.parameters) — O(1) zero-allocation lookup replaces try-catch. Hashed params: Speed, IsGrounded, VerticalVelocity, IsCrouching, IsCarrying, IsClimbing, IsPushing, IsSprinting. Triggers: Jump, DoubleJump, Land, Throw, Stumble. |
| `StateMachine/PlayerStateMachine.cs` | PlayerStateMachine | Generic state machine with type-safe registration/transitions. |
| `StateMachine/PlayerState.cs` | PlayerState | Abstract base. Helpers: GetCameraRelativeDirection(), RotateToward(), ApplyGravity(). |
| `StateMachine/States/GroundedState.cs` | GroundedState | Idle/walk/sprint. Handles crouch/jump/interact transitions. |
| `StateMachine/States/AirborneState.cs` | AirborneState | Jump/fall with coyote time & jump buffer. |
| `StateMachine/States/CrouchState.cs` | CrouchState | Reduced height/speed, stand-up clearance check. |
| `StateMachine/States/CarryState.cs` | CarryState | Hold physics objects, reduced speed. Safe drop: FindSafeDropPosition() SphereCasts 4 directions + feet fallback. Weight-affected throw: effectiveForce = throwForce / Max(weight, 0.1f). Pickup audio via PickupObject.PlaySound(). |
| `StateMachine/States/PushPullState.cs` | PushPullState | Push/pull rigidbody objects. AlignToNearestFace() snaps player to nearest ±X/±Z face on Enter. canPull enforcement (blocks backward). Friction scaling: speed * 1/(1+friction). Push audio: calls target.PlayPushLoop()/StopPushLoop(). Fall-off-edge → AirborneState. |
| `StateMachine/States/ClimbState.cs` | ClimbState | Climb on ClimbSurface objects. climbHeight enforcement (blocks upward at max). Lateral boundary: IsOnSurface() raycast + nudge back. Drop off bottom past startY → AirborneState. Uses player.climbSpeed (configurable in Inspector). |

### NPC System — `Assets/Scripts/NPC/`

| File | Class | Purpose |
|------|-------|---------|
| `NPCBase.cs` | NPCBase | Abstract base for all NPCs. Detection radius, FacePlayer(), audio helpers, proximity events. |
| `DialogueNPC.cs` | DialogueNPC | Extends InteractableBase (NOT NPCBase — required by InteractionDetector's GetComponent). QuickOutline pulsing, SernaAnimCycler, audio fade, ends dialogue on walk-away. Sets DialogueManager.ActiveNPCTransform. FacePlayerTowardNPC() rotates player toward NPC during dialogue. Per-node voiceClip + nodeAnimation playback via HandleNodeChanged. Default animation Inspector fields: defaultIdleClips[], defaultTalkClips[], defaultAnimChangeInterval — overrides SernaAnimCycler when populated. |
| `GhostNPC.cs` | GhostNPC | NavMesh roaming ghosts with 3 states (Roaming/Paused/Reacting). Karma evaluator for behavior. Greet/Scream reactions. |
| `GhostFloatEffect.cs` | GhostFloatEffect | Visual-only sine-wave floating/bobbing. Independent of GhostNPC movement. |

### Managers — `Assets/Scripts/Managers/`

| File | Class | Singleton | Events |
|------|-------|-----------|--------|
| `KarmaManager.cs` | KarmaManager | Yes (DontDestroyOnLoad) | OnKarmaChanged(delta), OnKarmaLevelUp(level) |
| `DialogueManager.cs` | DialogueManager | Yes (DontDestroyOnLoad) | OnDialogueStarted, OnNodeChanged, OnChoiceMade, OnDialogueEnded. Properties: ActiveNPCSpeakerName, ActiveNPCTransform, CurrentNode, IsDialogueActive. One-time rewards: runtime HashSet<string> check per choice (`rewarded_{dialogueId}_{nodeId}_{choiceIndex}`) — resets each Play session (avoids ScriptableObject persistence bug). ClearRewardedChoices() for mid-session reset. |
| `WalletManager.cs` | WalletManager | Yes (DontDestroyOnLoad) | OnCoinsChanged(total, delta). StartingCoins property for reset. |
| `HUDManager.cs` | HUDManager | Yes | Bridges manager events to UI |

### Data Models — `Assets/Scripts/Data/`

| File | Class(es) | Purpose |
|------|-----------|---------|
| `DialogueSO.cs` | DialogueSO, DialogueNode, DialogueChoice, ChoiceStyle | Dialogue tree ScriptableObject. Nodes have voiceClip + nodeAnimation (optional per-node animation override), [SerializeReference] conditions/onShowActions. Choices have conditions/actions + legacy karmaChange/coinChange. |
| `KarmaConfig.cs` | KarmaConfig | Karma level config: maxLevel, xpPerLevel, sprites, audio clips. |
| `VariableStore.cs` | VariableStore, StringBoolEntry, StringIntEntry | Game state store: flags, counters, relationships. Singleton via Resources.Load("GameVariables"). |
| `Conditions/IDialogueCondition.cs` | IDialogueCondition, ComparisonOp, KarmaLevelCondition, FlagCondition, CounterCondition | Extensible condition interface. [Serializable] classes + [SerializeReference] for polymorphic serialization. |
| `Actions/IDialogueAction.cs` | IDialogueAction, ModifyKarmaAction, ModifyCoinsAction, SetFlagAction, ModifyCounterAction | Extensible action interface. Same pattern as conditions. |

### Interaction System — `Assets/Scripts/Interaction/`

| File | Class | Purpose |
|------|-------|---------|
| `IInteractable.cs` | IInteractable | Interface: InteractionPrompt, CanInteract(), Interact(). |
| `InteractableBase.cs` | InteractableBase | Abstract base. Prompt, OnTargeted/OnUntargeted with default QuickOutline toggle (lazy-cached lookup via outlineLookedUp flag). Any InteractableBase with QuickOutline component auto-highlights when targeted. Subclasses can override. |
| `InteractionDetector.cs` | InteractionDetector | Trigger-based, angle-weighted target selection. Allocation-free null cleanup (manual backward for-loop). MinDotThreshold=0.3f (~72°) prevents targeting behind player. Debug.Log wrapped in #if UNITY_EDITOR. Events: OnPromptChanged, OnPromptHidden. Uses `GetComponentInParent<InteractableBase>()`. |

### World Objects — `Assets/Scripts/Objects/`

| File | Class | Purpose |
|------|-------|---------|
| `PickupObject.cs` | PickupObject | Carriable items. Smooth lerp pickup (0.2s ease-out LerpToCarryPoint coroutine). Weight affects throw distance. Optional audio: pickupSound, dropSound, throwSound via AudioSource.PlayClipAtPoint(). CancelLerp() on Drop/Throw. |
| `PushableObject.cs` | PushableObject | Push/pull physics objects. Friction scales push speed (1/(1+friction)). canPull flag enforced by PushPullState. Dynamic prompt ("Push / Pull" vs "Push"). Optional push loop audio (3D spatial, loops during push). PlayPushLoop()/StopPushLoop() public API. RigidbodyConstraints.FreezeRotation. |
| `ClimbSurface.cs` | ClimbSurface | Climbable surfaces. climbHeight field enforced by ClimbState (caps upward movement). SurfaceNormal for lateral boundary checking. |

### VFX — `Assets/Scripts/VFX/`

| File | Class | Purpose |
|------|-------|---------|
| `SparkleVFX.cs` | SparkleVFX | Code-configured ParticleSystem burst. Faded yellow orbs (25 particles, 0.8s, alpha fade + shrink). Auto-deactivates for pooling. Play()/Play(Vector3). |
| `SparkleVFXManager.cs` | SparkleVFXManager | Singleton pool/spawner (default 5 instances). PlayAt(pos), PlayAtTransform(target), PlayAtPlayer(). Auto-expands pool. |

### Environment — `Assets/Scripts/Environment/`

| File | Class | Purpose |
|------|-------|---------|
| `Lever.cs` | Lever | Pullable lever triggers. |
| `PressurePlate.cs` | PressurePlate | Weight-triggered plates. |
| `TriggerZone.cs` | TriggerZone | General-purpose trigger zones. |

### UI System — `Assets/Scripts/UI/`

| File | Class | Purpose |
|------|-------|---------|
| `DialogueUI.cs` | DialogueUI | Player bottom panel (700px fixed-width centered). Three modes: NPC-no-choices (panel hidden), NPC-with-choices (ghost panel — visuals hidden, only floating choice buttons), Player speaking (full panel). Typewriter effect. Z/X/C choices + Enter/E/Space advance. Player choice flow: highlight → player panel with "Sammy" badge → confirm → NPC response. Caches panelBorderImage + innerPanelObj for ghost panel mode. TrySubscribe + Start() fallback. |
| `ChoiceButtonUI.cs` | ChoiceButtonUI | Choice button with style colors (Empathetic=orange, Selfish=dark, Neutral=white). Shows lock reasons. SetSelected() for visual highlight on choice selection. |
| `KarmaFlowerUI.cs` | KarmaFlowerUI | Flower of Life display. Petal bloom on level-up. LevelUpFlash overlay. Animated progress bar fill with green flash (barGainColor) + ease-out curve. Runtime Image.Type.Filled enforcement in OnEnable. TrySubscribe + Start() fallback for KarmaManager subscription timing. |
| `CoinCounterUI.cs` | CoinCounterUI | Coin icon + text. Delta popup animations (+green, -red). AudioSource for coin sounds. |
| `KarmaPopupUI.cs` | KarmaPopupUI | 3-phase fly-to-target animation: pop-in (scale 0→1.3→1) → pause → fly to KarmaFlower (ease-in). Green color for gains, red for losses. PunchTargetCoroutine on landing. TrySubscribe + Start() fallback. |
| `CoinFlyUI.cs` | CoinFlyUI | Spawns 3 gold circle Images at screen center. Pop-in → pause → stagger-fly to CoinCounter (ease-in-out). Only for positive deltas. PunchTargetCoroutine on last coin. TrySubscribe + Start() fallback. |
| `NPCSpeechBubble.cs` | NPCSpeechBubble | World-space speech bubble above NPCs at worldOffset (0,5,0). Dynamic height from renderer bounds + heightPadding=0.2. Brown name badge + speech text. Auto-billboards to camera. Typewriter effect (useTypewriter=true, 35 chars/sec) with IsTypewriting/SkipTypewriter() API for DialogueUI coordination. Continue prompt "Press Enter >>" shown after typewriter finishes. Fade in/out animation. Auto-migration for worldOffset, prompt text, heightPadding. TrySubscribe + Start() fallback. |
| `ResetGameButton.cs` | ResetGameButton | Resets karma/coins/VariableStore, clears DialogueManager.rewardedChoices, and reloads scene. Managers survive via DontDestroyOnLoad, reset state before reload. |
| `FPSCounter.cs` | FPSCounter | Debug FPS display (top-right corner). |
| `HUDManager.cs` | HUDManager | Top-level HUD coordination. Bridges manager events to UI. Subscribes to InteractionDetector for "Press E" prompt. |

### Other Scripts — `Assets/Scripts/`

| File | Class | Purpose |
|------|-------|---------|
| `ThirdPersonCamera.cs` | ThirdPersonCamera | CameraRig parent with child Main Camera. Two modes: Follow (rig at player, rotation matches player Y) + Dialogue (rig at player, rotates toward NPC — child offset creates over-the-shoulder shot). Subscribes to DialogueManager events via TrySubscribe. |
| `SkyboxController.cs` | SkyboxController | Day/night skybox cycle. |
| `SernaAnimCycler.cs` | SernaAnimCycler | Serna idle/talk animation variant cycling (3 idles, 3 talks). |
| `SernaInteraction.cs` | SernaInteraction | **LEGACY** — replaced by DialogueNPC. |
| `GhostFloat.cs` | GhostFloat | **LEGACY** — replaced by GhostFloatEffect. |
| `GhostSpawner.cs` | GhostSpawner | Runtime ghost spawner with randomization. |

### Editor Tools — `Assets/Editor/`

| File | Class | Menu Item | Purpose |
|------|-------|-----------|---------|
| `GameSystemsSetup.cs` | GameSystemsSetup | Karma > Setup Game Systems, Karma > Quick Setup Checklist | Auto-creates GameManagers with all singletons (incl. SparkleVFXManager). Validates Player/Serna. Auto-assigns SparkleVFX prefab. |
| `VFXPrefabCreator.cs` | VFXPrefabCreator | Karma > Create Sparkle VFX Prefab | Creates SparkleVFX prefab at Assets/Prefab/VFX/. Auto-assigns to SparkleVFXManager in scene. |
| `UISetupTool.cs` | UISetupTool | Karma > Build UI Canvases, Build HUD/Dialogue Canvas Only | Programmatic Canvas builder matching HUDCanvas.prefab layout. Creates HUDCanvas (sort 5) + DialogueCanvas (sort 10) + ChoiceButton prefab. GUID-based sprite/audio loading. BuildHUDCanvasOnly re-wires HUDManager via WireHUDManagerToHUD. Auto-wires all references. |
| `PlayerAnimatorSetup.cs` | PlayerAnimatorSetup | Karma > Rebuild Player Animator | Rebuilds PlayerAnimatorController with all states/transitions. |
| `PlayerSetupValidator.cs` | PlayerSetupValidator | Karma > Validate Player Setup | Checks player components, tag, animator. |
| `DialogueDataCreator.cs` | DialogueDataCreator | Karma > Create Serna Intro/Return Dialogue, Karma > Create Karma Config, Karma > Create Variable Store | Creates sample dialogue assets with extensible actions. |
| `DialogueEditorWindow.cs` | DialogueEditorWindow | Karma > Dialogue Editor | 3-panel visual editor: node list with arrows, detail panel, embedded preview. |
| `VariableStoreBrowser.cs` | VariableStoreBrowser | Karma > Variable Store | Inspect/edit flags, counters, relationships. Add/remove/search. |
| `Drawers/DialogueSOEditor.cs` | DialogueSOEditor | (CustomEditor) | Color-coded node cards, inline condition/action tags, expandable choices, node ID dropdowns. |
| `Helpers/DialogueTypeCache.cs` | DialogueTypeCache | — | Reflection-based auto-discovery of all IDialogueCondition/IDialogueAction types. |
| `Helpers/DialogueEditorStyles.cs` | DialogueEditorStyles | — | Shared colors, GUIStyles, drawing helpers for editor toolkit. |

---

## Design Patterns

1. **State Machine** — PlayerStateMachine (generic) → 6 concrete states; GhostNPC uses local switch-based SM
2. **Singleton** — KarmaManager, DialogueManager, WalletManager, VariableStore (all DontDestroyOnLoad)
3. **ScriptableObject Data** — DialogueSO, KarmaConfig, VariableStore via CreateAssetMenu
4. **Event-Driven** — Action delegates for all system events; UI subscribes to manager events
5. **Interface Extensibility** — IDialogueCondition/IDialogueAction with [SerializeReference] for polymorphic serialization; auto-discovered via reflection
6. **Component Composition** — PlayerController owns sub-components; Managers on shared GameManagers object
7. **Backward Compatibility** — Legacy fields (karmaChange, coinChange, requiredKarmaLevel) kept alongside new extensible lists

---

## Key System Flows

### Karma Flow
1. Player makes choice → triggers ModifyKarmaAction (one-time per choice via runtime HashSet)
2. KarmaManager.AddKarma(amount) → plays audio feedback
3. OnKarmaChanged event → KarmaFlowerUI updates progress bar (animated fill with green flash), KarmaPopupUI shows "+50" then flies to KarmaFlower
4. If coins also awarded → CoinFlyUI spawns 3 coins that fly to CoinCounter
5. If level crosses threshold → OnKarmaLevelUp → petal bloom + LevelUpFlash overlay
6. KarmaManager.GetNormalizedKarma() (0.0-1.0) available for NPC behavior

### Dialogue Flow
1. Player approaches NPC → InteractionDetector targets DialogueNPC
2. E key → GroundedState calls InteractionDetector → DialogueNPC.Interact()
3. DialogueNPC sets ActiveNPCTransform + ActiveNPCSpeakerName, both characters face each other
4. DialogueManager.StartDialogue(dialogueSO) → disables player input → fires OnDialogueStarted
5. ThirdPersonCamera enters dialogue mode (rig rotates toward NPC → over-the-shoulder shot)
6. ShowNode(): evaluates conditions → skips if fail → executes onShowActions → fires OnNodeChanged
7. DialogueNPC.HandleNodeChanged: plays per-node voiceClip + nodeAnimation (if set, pauses SernaAnimCycler and CrossFades to clip)
8. **NPC line (no choices)**: NPCSpeechBubble typewriters text in bubble above NPC. "Press Enter >>" shown after typewriter finishes. Bottom panel hidden.
9. **NPC line (with choices)**: NPCSpeechBubble typewriters question text. DialogueUI enters "ghost panel" mode — panel active but border/inner panel hidden, only floating ChoiceButtonUI instances visible.
10. Player presses Z/X/C → highlight delay → choice text typewriters in player bottom panel with "Sammy" badge → Enter to confirm → SelectChoice() fires → karma/coins applied → if Empathetic choice → SparkleVFX auto-bursts at player → NPC response in bubble
11. Player presses Enter/E/Space to advance non-choice nodes (first press skips bubble typewriter, second press advances)
12. EndDialogue() → re-enables player input → camera returns to follow mode → fires OnDialogueEnded → DialogueNPC re-enables SernaAnimCycler

### World Object Interaction Flows

**Pickup/Carry/Throw:**
1. Player approaches PickupObject → InteractionDetector targets it (QuickOutline auto-highlights)
2. E key → PickupObject.Interact() → sets interactionTarget → CarryState
3. CarryState.Enter() → starts LerpToCarryPoint() coroutine (0.2s ease-out smooth pickup) → plays pickupSound
4. While carrying: reduced speed, can interact with other objects
5. E to drop → FindSafeDropPosition() SphereCasts forward/right/left/behind/feet → plays dropSound
6. Left-click to throw → effectiveForce = throwForce / Max(weight, 0.1f) → plays throwSound

**Push/Pull:**
1. Player approaches PushableObject → QuickOutline highlights, prompt shows "Push / Pull" or "Push"
2. E key → PushPullState.Enter() → AlignToNearestFace() snaps player to nearest ±X/±Z face
3. Forward input pushes, backward pulls (if canPull=true, else blocked)
4. Speed scaled by friction: effectiveSpeed = pushPullSpeed * 1/(1+friction)
5. Push audio loops while input active, stops when idle
6. Fall off edge → AirborneState. E to release → GroundedState

**Climb:**
1. Player approaches ClimbSurface → QuickOutline highlights
2. E key → ClimbState.Enter() → zeroes velocity, tracks startY
3. Vertical input moves up/down at player.climbSpeed
4. climbHeight enforced: blocks upward movement at max height above startY
5. Lateral boundary: IsOnSurface() raycast check, nudges back if off-surface
6. Jump off (Space) → push away from wall. Crouch to drop. Down past startY → let go
7. ReachedLedgeTop() → vault over with push

### Adding Future Systems (Zero Core Changes)
```csharp
// Just create one class:
[Serializable]
public class ShowReflectionCardAction : IDialogueAction
{
    public string cardId;
    public string Label => $"Show Card: {cardId}";
    public void Execute() => ReflectionCardManager.Instance.ShowCard(cardId);
}
// Auto-appears in editor dropdown. No other files touched.
```

---

## Animator Parameters

| Parameter | Type | Purpose |
|-----------|------|---------|
| Speed | float (0-1) | Normalized movement speed for blend tree |
| IsGrounded | bool | Jump → Locomotion transition |
| VerticalVelocity | float | Falling detection |
| IsCrouching | bool | → CrouchIdle/CrouchWalk states |
| IsCarrying | bool | → Carry state |
| IsClimbing | bool | → Climb state |
| IsPushing | bool | → Push state |
| IsSprinting | bool | Cosmetic flag |
| Jump | trigger | → Jump state |
| DoubleJump | trigger | → DoubleJump state |
| Land | trigger | Not actively used |
| Throw | trigger | → Throw state |
| Stumble | trigger | → Stumble state |

## Available Animations (`Assets/3D/Character/stripes/sammy animations/`)
Idle_11, Walking, Running, Jump_Over_Obstacle, Happy_Jump_f (double jump), Sit_Cross_Legged (crouch idle), Carry_Heavy_Object_Walk, Victory_Cheer, Big_Wave_Hello, Angry_Stomp, Angry_Ground_Stomp, Angry_To_Tantrum_Sit, Confused_Scratch, Headache_Relief, Happy_jump_m, and 35+ more clips.

---

## Asset Structure

```
Assets/
├── Data/
│   ├── KarmaConfig.asset
│   ├── Dialogues/
│   │   ├── Serna_Intro.asset
│   │   └── Serna_Return.asset
├── Resources/
│   └── GameVariables.asset (VariableStore)
├── Prefab/
│   ├── Player.prefab
│   └── UI/ChoiceButton.prefab
├── Scripts/ (see file inventory above)
├── Editor/ (see file inventory above)
├── 3D/Character/stripes/sammy animations/ (50+ FBX clips)
├── Animation/
│   ├── QuickOutline/ (NPC highlighting)
│   └── PlayerAnimatorController.controller
├── Scenes/
│   └── Chapter1- Serna.unity
└── Settings/
    └── DefaultVolumeProfile.asset (URP)
```

---

## Dependencies
- **New Input System** (PlayerInputHandler)
- **TextMesh Pro** (all UI text)
- **QuickOutline** (NPC + world object highlighting, `Assets/Animation/QuickOutline/`, class: `QuickOutline`)
- **NavMesh** (GhostNPC movement)
- **URP** (rendering, skybox)
- **Boxophobic Utils** (styled inspectors)
- **Bitgem StylisedWater** (water effects)

---

## Unity Menu Items (All under "Karma" menu)
- Setup Game Systems — auto-creates GameManagers with all singletons
- Build UI Canvases — builds HUDCanvas + DialogueCanvas + ChoiceButton prefab from Figma mockups
- Build HUD Canvas Only — just the HUD canvas
- Build Dialogue Canvas Only — just the dialogue canvas
- Quick Setup Checklist — prints full setup guide
- Create Karma Config — creates KarmaConfig asset
- Create Variable Store — creates VariableStore asset
- Create Serna Intro Dialogue — creates Serna_Intro.asset with extensible actions
- Create Serna Return Dialogue — creates Serna_Return.asset demonstrating conditions
- Rebuild Player Animator — rebuilds animator controller with all states
- Validate Player Setup — checks player components
- Dialogue Editor — visual dialogue tree editor window
- Variable Store — game variable browser/inspector

## UI Canvas Hierarchy (built by UISetupTool)

```
HUDCanvas (Screen Space Overlay, Sort Order 5)
├── InteractionPrompt (bottom-center, hidden)
│   └── PromptText ("Press E to pick up")
├── KarmaFlower (top-left, 273.9×80, matches HUDCanvas.prefab)
│   ├── FlowerIcon (sprite by GUID, center-left anchor)
│   ├── ProgressBarBg (sprite, bottom-anchored, 27.1px tall)
│   │   └── ProgressBarFill (sprite, 6px inset, Filled horizontal)
│   ├── LevelText ("Lv.0", left of center)
│   ├── KarmaScoreText ("167", below center)
│   ├── LevelUpFlash (fullscreen overlay, hidden)
│   └── KarmaFlowerUI (auto-wired, audio clips by GUID)
├── CoinCounter (top-left, below KarmaFlower, anchoredPos 155,-141.8)
│   ├── CoinIcon (sprite by GUID) + CoinText + DeltaPopup
│   ├── AudioSource (for coin sounds)
│   └── CoinCounterUI (auto-wired)
├── KarmaPopup (center, flies to KarmaFlower)
│   └── KarmaPopupText + KarmaPopupUI (3-phase fly animation)
├── CoinFlyUI (center, 3 coins fly to CoinCounter)
│   └── CoinFlyUI component (stagger-fly animation)
├── FPSCounter (top-right corner)
└── ResetButton (bottom-right, "Reset Game")

DialogueCanvas (Screen Space Overlay, Sort Order 10)
└── DialoguePanel (bottom-center, 700px fixed-width, orange border + cream bg, inactive)
    ├── InnerPanel (cream bg with 3px border inset)
    │   ├── DialogueTextArea → DialogueText (typewriter narration)
    │   └── ContinuePrompt ("Press Enter to continue ▶", hidden)
    ├── SpeakerBadge (brown, top-left, overlapping border)
    │   └── SpeakerNameText ("Serna" or "Sammy")
    └── ChoiceContainer (vertical layout, fixed-width centered ABOVE panel, hidden)
        └── [spawned ChoiceButton instances]

    Ghost Panel Mode: panel active but panelBorderImage.enabled=false + InnerPanel hidden
                      Only ChoiceContainer renders as floating choice buttons

NPC Speech Bubble (World Space Canvas, child of NPC)
└── NPCSpeechBubble component on canvas
    └── BubblePanel (auto-built if not pre-assigned)
        ├── NameBadge (brown) → SpeakerName
        ├── SpeechText (white, max 80 chars)
        └── ContinuePrompt ("Press Enter ▶", italic, right-aligned)

ChoiceButton Prefab (Assets/Prefab/UI/ChoiceButton.prefab)
├── Background (Image, neutral cream default)
├── InputBadge (orange circle)
│   └── BadgeText ("Z"/"X"/"C")
├── ChoiceText ("Choice description here")
└── ChoiceButtonUI + Button (auto-wired)
```

---

## Teammate Note
Teammate's `feature/UI` branch work is NOT being used. Fresh UI implementation built from Figma mockups using UISetupTool.

## Critical Architecture Notes

### Camera Rig Architecture
```
CameraRig (ThirdPersonCamera script here)
  └── Main Camera (Camera + AudioListener + URP — child with local offset)
```
- ThirdPersonCamera manipulates the **CameraRig** transform (position + rotation)
- The child Main Camera's local offset creates the 3rd-person / over-the-shoulder view
- **NEVER** directly reposition the rig to a custom position during dialogue — just rotate it toward the NPC. The child offset handles everything.

### Event Subscription Timing Pattern
All UI/camera scripts that depend on singleton managers use this pattern:
```csharp
private bool isSubscribed;
void OnEnable() { TrySubscribe(); }
void Start() { TrySubscribe(); } // Fallback: singleton may be null during OnEnable
private void TrySubscribe() {
    if (isSubscribed) return;
    if (Manager.Instance == null) return;
    Manager.Instance.OnEvent += Handler;
    isSubscribed = true;
}
```
Used in: DialogueUI, NPCSpeechBubble, KarmaFlowerUI, ThirdPersonCamera

### Unity Serialization Gotcha
Changing a field's default value in code does NOT update already-serialized Inspector values on scene instances. Fix: add auto-migration in Awake():
```csharp
if (worldOffset.y <= 3f) worldOffset = new Vector3(0f, 5f, 0f);
if (heightPadding >= 0.5f) heightPadding = 0.2f;
```

### BuildHUDCanvasOnly Must Re-Wire HUDManager
Rebuilding HUDCanvas destroys old GameObjects, breaking HUDManager's serialized references (interactionPromptPanel, karmaFlowerUI, etc.). Fix: `BuildHUDCanvasOnly` calls `WireHUDManagerToHUD(hudCanvas)` which re-wires all HUD-related fields on HUDManager.

## Performance Optimizations Applied
- **PlayerAnimationHandler**: HashSet<int> validParams replaces try-catch on every SetBool/SetFloat/SetTrigger call (~10+/frame). Cached once in Awake().
- **InteractionDetector**: Manual backward for-loop replaces `RemoveAll(lambda)` — zero per-frame delegate allocation. MinDotThreshold filtering.
- **InteractableBase**: Lazy QuickOutline lookup with `outlineLookedUp` flag — avoids repeated GetComponentInChildren calls.
- **DialogueManager**: Runtime HashSet<string> for one-time rewards instead of ScriptableObject persistence (VariableStore changes in Play Mode persisted in editor).
- **All Debug.Log calls** in InteractionDetector wrapped in `#if UNITY_EDITOR`.

## What's Next (Planned)
- Reflection Card system (IDialogueAction extension)
- IRL Prompt system (IDialogueAction extension)
- Quest system (QuestSO + QuestManager + IDialogueCondition/IDialogueAction extensions)
- NPC Relationship system (uses VariableStore.relationships)
- Cat sidekick companion AI
- Chapter 1 level design and NPC population
