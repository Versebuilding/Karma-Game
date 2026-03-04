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
| `PlayerController.cs` | PlayerController | Main player manager. Owns CharacterController, input, animation, state machine. Manages velocity/gravity/jump/stumble. |
| `PlayerInputHandler.cs` | PlayerInputHandler | New Input System wrapper. Polled input state: MoveInput, LookInput, JumpPressed, SprintHeld, CrouchPressed, InteractPressed. |
| `PlayerAnimationHandler.cs` | PlayerAnimationHandler | Centralized animator param management. Hashed params: Speed, IsGrounded, VerticalVelocity, IsCrouching, IsCarrying, IsClimbing, IsPushing, IsSprinting. Triggers: Jump, DoubleJump, Land, Throw, Stumble. |
| `StateMachine/PlayerStateMachine.cs` | PlayerStateMachine | Generic state machine with type-safe registration/transitions. |
| `StateMachine/PlayerState.cs` | PlayerState | Abstract base. Helpers: GetCameraRelativeDirection(), RotateToward(), ApplyGravity(). |
| `StateMachine/States/GroundedState.cs` | GroundedState | Idle/walk/sprint. Handles crouch/jump/interact transitions. |
| `StateMachine/States/AirborneState.cs` | AirborneState | Jump/fall with coyote time & jump buffer. |
| `StateMachine/States/CrouchState.cs` | CrouchState | Reduced height/speed, stand-up clearance check. |
| `StateMachine/States/CarryState.cs` | CarryState | Hold physics objects, reduced speed, throw. |
| `StateMachine/States/PushPullState.cs` | PushPullState | Push/pull rigidbody objects. |
| `StateMachine/States/ClimbState.cs` | ClimbState | Climb on ClimbSurface objects. |

### NPC System — `Assets/Scripts/NPC/`

| File | Class | Purpose |
|------|-------|---------|
| `NPCBase.cs` | NPCBase | Abstract base for all NPCs. Detection radius, FacePlayer(), audio helpers, proximity events. |
| `DialogueNPC.cs` | DialogueNPC | Extends InteractableBase (NOT NPCBase — required by InteractionDetector's GetComponent). QuickOutline pulsing, SernaAnimCycler, audio fade, ends dialogue on walk-away. |
| `GhostNPC.cs` | GhostNPC | NavMesh roaming ghosts with 3 states (Roaming/Paused/Reacting). Karma evaluator for behavior. Greet/Scream reactions. |
| `GhostFloatEffect.cs` | GhostFloatEffect | Visual-only sine-wave floating/bobbing. Independent of GhostNPC movement. |

### Managers — `Assets/Scripts/Managers/`

| File | Class | Singleton | Events |
|------|-------|-----------|--------|
| `KarmaManager.cs` | KarmaManager | Yes (DontDestroyOnLoad) | OnKarmaChanged(delta), OnKarmaLevelUp(level) |
| `DialogueManager.cs` | DialogueManager | Yes (DontDestroyOnLoad) | OnDialogueStarted, OnNodeChanged, OnChoiceMade, OnDialogueEnded |
| `WalletManager.cs` | WalletManager | Yes (DontDestroyOnLoad) | OnCoinsChanged(total, delta) |
| `HUDManager.cs` | HUDManager | Yes | Bridges manager events to UI |

### Data Models — `Assets/Scripts/Data/`

| File | Class(es) | Purpose |
|------|-----------|---------|
| `DialogueSO.cs` | DialogueSO, DialogueNode, DialogueChoice, ChoiceStyle | Dialogue tree ScriptableObject. Nodes have [SerializeReference] conditions/onShowActions. Choices have conditions/actions + legacy karmaChange/coinChange. |
| `KarmaConfig.cs` | KarmaConfig | Karma level config: maxLevel, xpPerLevel, sprites, audio clips. |
| `VariableStore.cs` | VariableStore, StringBoolEntry, StringIntEntry | Game state store: flags, counters, relationships. Singleton via Resources.Load("GameVariables"). |
| `Conditions/IDialogueCondition.cs` | IDialogueCondition, ComparisonOp, KarmaLevelCondition, FlagCondition, CounterCondition | Extensible condition interface. [Serializable] classes + [SerializeReference] for polymorphic serialization. |
| `Actions/IDialogueAction.cs` | IDialogueAction, ModifyKarmaAction, ModifyCoinsAction, SetFlagAction, ModifyCounterAction | Extensible action interface. Same pattern as conditions. |

### Interaction System — `Assets/Scripts/Interaction/`

| File | Class | Purpose |
|------|-------|---------|
| `IInteractable.cs` | IInteractable | Interface: InteractionPrompt, CanInteract(), Interact(). |
| `InteractableBase.cs` | InteractableBase | Abstract base. Prompt, OnTargeted/OnUntargeted for highlights. |
| `InteractionDetector.cs` | InteractionDetector | Trigger-based, angle-weighted target selection. Events: OnPromptChanged, OnPromptHidden. Uses `GetComponent<InteractableBase>()`. |

### World Objects — `Assets/Scripts/Objects/`

| File | Class | Purpose |
|------|-------|---------|
| `PickupObject.cs` | PickupObject | Carriable items. Weight, stackable, throw. |
| `PushableObject.cs` | PushableObject | Push/pull physics objects. Friction, canPull. |
| `ClimbSurface.cs` | ClimbSurface | Climbable surfaces. |

### Environment — `Assets/Scripts/Environment/`

| File | Class | Purpose |
|------|-------|---------|
| `Lever.cs` | Lever | Pullable lever triggers. |
| `PressurePlate.cs` | PressurePlate | Weight-triggered plates. |
| `TriggerZone.cs` | TriggerZone | General-purpose trigger zones. |

### UI System — `Assets/Scripts/UI/`

| File | Class | Purpose |
|------|-------|---------|
| `DialogueUI.cs` | DialogueUI | Dialogue panel with typewriter effect. Spawns ChoiceButtonUI from prefab. Handles Z/X/C and E/Space input. |
| `ChoiceButtonUI.cs` | ChoiceButtonUI | Choice button with style colors (Empathetic=orange, Selfish=dark, Neutral=white). Shows lock reasons from both legacy and extensible conditions. |
| `KarmaFlowerUI.cs` | KarmaFlowerUI | Flower of Life display. Petal bloom on level-up. Progress bar. |
| `CoinCounterUI.cs` | CoinCounterUI | Coin icon + text. Delta popup animations (+green, -red). |
| `KarmaPopupUI.cs` | KarmaPopupUI | Floating "+50 Karma" text that fades out. |
| `HUDManager.cs` | HUDManager | Top-level HUD coordination. |

### Other Scripts — `Assets/Scripts/`

| File | Class | Purpose |
|------|-------|---------|
| `ThirdPersonCamera.cs` | ThirdPersonCamera | Camera follow. |
| `SkyboxController.cs` | SkyboxController | Day/night skybox cycle. |
| `SernaAnimCycler.cs` | SernaAnimCycler | Serna idle/talk animation variant cycling (3 idles, 3 talks). |
| `SernaInteraction.cs` | SernaInteraction | **LEGACY** — replaced by DialogueNPC. |
| `GhostFloat.cs` | GhostFloat | **LEGACY** — replaced by GhostFloatEffect. |
| `GhostSpawner.cs` | GhostSpawner | Runtime ghost spawner with randomization. |

### Editor Tools — `Assets/Editor/`

| File | Class | Menu Item | Purpose |
|------|-------|-----------|---------|
| `GameSystemsSetup.cs` | GameSystemsSetup | Karma > Setup Game Systems, Karma > Quick Setup Checklist | Auto-creates GameManagers with all singletons. Validates Player/Serna. |
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
1. Player makes choice → triggers ModifyKarmaAction
2. KarmaManager.AddKarma(amount) → plays audio feedback
3. OnKarmaChanged event → KarmaFlowerUI updates progress bar, KarmaPopupUI shows "+50"
4. If level crosses threshold → OnKarmaLevelUp → petal bloom animation
5. KarmaManager.GetNormalizedKarma() (0.0-1.0) available for NPC behavior

### Dialogue Flow
1. Player approaches NPC → InteractionDetector targets DialogueNPC
2. E key → GroundedState calls InteractionDetector → DialogueNPC.Interact()
3. DialogueManager.StartDialogue(dialogueSO) → disables player input
4. ShowNode(): evaluates conditions → skips if fail → executes onShowActions → fires OnNodeChanged
5. DialogueUI shows speaker + text (typewriter) + spawns ChoiceButtonUI if choices exist
6. Player presses Z/X/C → SelectChoice() → executes legacy karma/coins + extensible actions[] → advances
7. EndDialogue() → re-enables player input → fires OnDialogueEnded

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
- **QuickOutline** (NPC highlighting, `Assets/Animation/QuickOutline/`)
- **NavMesh** (GhostNPC movement)
- **URP** (rendering, skybox)
- **Boxophobic Utils** (styled inspectors)
- **Bitgem StylisedWater** (water effects)

---

## Unity Menu Items (All under "Karma" menu)
- Setup Game Systems — auto-creates GameManagers with all singletons
- Quick Setup Checklist — prints full setup guide
- Create Karma Config — creates KarmaConfig asset
- Create Variable Store — creates VariableStore asset
- Create Serna Intro Dialogue — creates Serna_Intro.asset with extensible actions
- Create Serna Return Dialogue — creates Serna_Return.asset demonstrating conditions
- Rebuild Player Animator — rebuilds animator controller with all states
- Validate Player Setup — checks player components
- Dialogue Editor — visual dialogue tree editor window
- Variable Store — game variable browser/inspector

---

## Teammate Note
UI integration and visual polish for the dialogue system is handled by a teammate on `feature/UI` branch — don't touch files they're working on.

## What's Next (Planned)
- Reflection Card system (IDialogueAction extension)
- IRL Prompt system (IDialogueAction extension)
- Quest system (QuestSO + QuestManager + IDialogueCondition/IDialogueAction extensions)
- NPC Relationship system (uses VariableStore.relationships)
- Cat sidekick companion AI
- Chapter 1 level design and NPC population
