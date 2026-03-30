# Karma Project — Technical Memory

## Project Overview
- **Engine**: Unity 6000.0.62f1, Universal Render Pipeline (URP)
- **Game**: RPG themed on 6 Realms of Karma
- **Player**: Sammy (3D character, CharacterController, height 6.5, scale ~3.19)
- **Sidekick**: Cat (planned)
- **Mentor**: Ananda (model + animations imported, dialogue created)
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
│ └── PlayerStateMachine          │   │ QuestManager (singleton)          │
│     ├── GroundedState           │   │ HUDManager (singleton)            │
│     ├── AirborneState           │   └─────────────────────────────────────┘
│     ├── CrouchState             │              │ events
│     ├── CarryState              │              ▼
│     ├── PushPullState           │   ┌──────────── UI LAYER ──────────────┐
│     └── ClimbState              │   │ DialogueUI + ChoiceButtonUI        │
└─────────────────────────────────┘   │ KarmaFlowerUI + CoinCounterUI     │
         │ InteractionDetector         │ KarmaPopupUI + QuestLogUI         │
         ▼                             └────────────────────────────────────┘
┌──────────── NPC LAYER ──────────┐   ┌──────── DATA LAYER ───────────────┐
│ NPCBase (abstract)              │   │ DialogueSO (ScriptableObject)     │
│ DialogueNPC (InteractableBase)  │   │ ├── DialogueNode[]                │
│ GhostNPC (NPCBase)              │   │ │   ├── conditions[]              │
│ GhostFloatEffect                │   │ │   └── onShowActions[]           │
└─────────────────────────────────┘   │ └── DialogueChoice[]              │
                                      │     ├── conditions[]              │
┌──── QUEST SYSTEM ──────────────┐    │     └── actions[]                │
│ QuestSO (ScriptableObject)     │    │ QuestSO (ScriptableObject)       │
│ ├── QuestObjective[]           │    │ ├── QuestObjective[]             │
│ ├── QuestRewards               │    │ ├── QuestRewards                 │
│ └── prerequisites[]            │    │ └── tags[]                       │
│ QuestRuntimeState (serializable)│   │ KarmaConfig (ScriptableObject)   │
│ QuestTriggerZone (GoTo)        │    │ VariableStore (ScriptableObject)  │
│ QuestItemPickup (Gather)       │    └────────────────────────────────────┘
└─────────────────────────────────┘
                                      ┌──── EDITOR TOOLKIT ───────────────┐
┌──── EXTENSIBILITY FRAMEWORK ────┐   │ DialogueEditorWindow              │
│ IDialogueCondition (interface)  │   │ VariableStoreBrowser              │
│ ├── KarmaLevelCondition         │   │ QuestDebugWindow (Play Mode)      │
│ ├── FlagCondition               │   │ DialogueSOEditor (CustomEditor)   │
│ ├── CounterCondition            │   │ DialogueTypeCache (reflection)    │
│ ├── QuestStateCondition         │   │ DialogueEditorStyles              │
│ └── QuestObjectiveCondition     │   └────────────────────────────────────┘
│ IDialogueAction (interface)     │
│ ├── ModifyKarmaAction           │
│ ├── ModifyCoinsAction           │
│ ├── SetFlagAction               │
│ ├── ModifyCounterAction         │
│ ├── StartQuestAction            │
│ ├── AdvanceQuestAction          │
│ ├── CompleteQuestAction         │
│ └── GiveItemAction              │
└─────────────────────────────────┘
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
| `DialogueNPC.cs` | DialogueNPC | Extends InteractableBase (NOT NPCBase — required by InteractionDetector's GetComponent). QuickOutline pulsing, SernaAnimCycler, audio fade, ends dialogue on walk-away. Sets DialogueManager.ActiveNPCTransform. FacePlayerTowardNPC() rotates player toward NPC during dialogue. Per-node voiceClip + nodeAnimation playback via HandleNodeChanged. TryCrossFade() helper checks Animator.HasState() before CrossFade (prevents errors for missing states). Default animation Inspector fields: defaultIdleClips[], defaultTalkClips[], defaultAnimChangeInterval — overrides SernaAnimCycler when populated. |
| `GhostNPC.cs` | GhostNPC | NavMesh roaming ghosts with 3 states (Roaming/Paused/Reacting). Karma evaluator for behavior. Greet/Scream reactions. hasSpeedParam flag caches whether animator has Speed parameter (prevents warning spam). |
| `GhostFloatEffect.cs` | GhostFloatEffect | Visual-only sine-wave floating/bobbing. Independent of GhostNPC movement. |

### Managers — `Assets/Scripts/Managers/`

| File | Class | Singleton | Events |
|------|-------|-----------|--------|
| `KarmaManager.cs` | KarmaManager | Yes (DontDestroyOnLoad) | OnKarmaChanged(delta), OnKarmaLevelUp(level) |
| `DialogueManager.cs` | DialogueManager | Yes (DontDestroyOnLoad) | OnDialogueStarted, OnNodeChanged, OnChoiceMade, OnDialogueEnded. Properties: ActiveNPCSpeakerName, ActiveNPCTransform, CurrentNode, IsDialogueActive. One-time rewards: runtime HashSet<string> check per choice (`rewarded_{dialogueId}_{nodeId}_{choiceIndex}`) — resets each Play session (avoids ScriptableObject persistence bug). ClearRewardedChoices() for mid-session reset. |
| `WalletManager.cs` | WalletManager | Yes (DontDestroyOnLoad) | OnCoinsChanged(total, delta). StartingCoins property for reset. |
| `HUDManager.cs` | HUDManager | Yes | Bridges manager events to UI |
| `BackgroundMusicManager.cs` | BackgroundMusicManager | Yes (DontDestroyOnLoad) | Plays GameBackgroundTrack.mp3 on loop. Auto-ducks volume when any other AudioSource plays (coroutine scans every 0.15s, cache refreshes every 2s). Smooth fade via MoveTowards in Update. normalVolume=0.4, duckedVolume=0.15, duckFadeSpeed=2. Public API: ForceDuck/ForceUnduck, PauseMusic/ResumeMusic, SetNormalVolume. |
| `QuestManager.cs` | QuestManager | Yes (DontDestroyOnLoad) | OnQuestStarted, OnObjectiveUpdated, OnQuestCompleted, OnQuestFailed, OnObjectiveFailed. Registry: Dictionary<string,QuestSO> from serialized QuestSO[]. Runtime: Dictionary<string,QuestRuntimeState>. API: StartQuest(), AdvanceObjective(), CompleteQuest(), FailQuest(), FailObjective(), GetQuestState(), GetObjectiveProgress(), GetActiveQuests(), GetQuestsByTag(), ResetAllQuests(). Auto-initializes: sets Available for quests with met prerequisites, auto-starts if configured. AwardRewards distributes to KarmaManager, WalletManager, VariableStore. UnlockFollowUpQuests on completion. |
| `InventoryManager.cs` | InventoryManager | Yes (DontDestroyOnLoad) | OnItemAdded(ItemSO), OnItemRemoved(ItemSO). Simple item list. API: AddItem(), RemoveItem(), HasItem(name), GetItems(), GetItemCount(name), ClearItems(). Auto-syncs VariableStore "has_{itemName}" flags on add/remove. |

### Data Models — `Assets/Scripts/Data/`

| File | Class(es) | Purpose |
|------|-----------|---------|
| `DialogueSO.cs` | DialogueSO, DialogueNode, DialogueChoice, ChoiceStyle | Dialogue tree ScriptableObject. Nodes have voiceClip + nodeAnimation (optional per-node animation override), [SerializeReference] conditions/onShowActions. Choices have conditions/actions + legacy karmaChange/coinChange. |
| `KarmaConfig.cs` | KarmaConfig | Karma level config: maxLevel, xpPerLevel, sprites, audio clips. |
| `VariableStore.cs` | VariableStore, StringBoolEntry, StringIntEntry | Game state store: flags, counters, relationships. Singleton via Resources.Load("GameVariables"). |
| `QuestSO.cs` | QuestSO, QuestObjective, QuestRewards, QuestType, QuestState, ObjectiveType, ObjectiveVisibility | Quest definition ScriptableObject. CreateAssetMenu: Karma/Quest. Fields: questId, displayName, description, questType, objectives[], rewards, prerequisites[], autoStart, isRepeatable, tags[]. QuestObjective: objectiveId, description, type (Talk/Gather/GoTo/Activate/Kill/Minigame/Custom), targetId, requiredCount, isOptional, visibility (Hidden/SoftHint/JournalVisible/MapMarkerVisible), canFail, retryAllowed, fallbackDialogueId. QuestRewards: karmaAmount, coinAmount, items[], flagsToSet[], questsToUnlock[]. QuestState FSM: Locked→Available→Active→Completed→Done, Failed. |
| `QuestRuntimeState.cs` | QuestRuntimeState, SerializableDictionary | Plain C# [Serializable] class (NOT MonoBehaviour). Mutable runtime state: questId, state (QuestState), objectiveProgress (SerializableDictionary). SerializableDictionary: parallel List<string> keys + List<int> values (Unity can't serialize Dictionary). Get/Set/ContainsKey. |
| `Conditions/IDialogueCondition.cs` | IDialogueCondition, ComparisonOp, KarmaLevelCondition, FlagCondition, CounterCondition, QuestStateCondition, QuestObjectiveCondition | Extensible condition interface. [Serializable] classes + [SerializeReference] for polymorphic serialization. QuestStateCondition checks quest state (e.g., Active, Done). QuestObjectiveCondition checks objective progress with ComparisonOp. |
| `Actions/IDialogueAction.cs` | IDialogueAction, ModifyKarmaAction, ModifyCoinsAction, SetFlagAction, ModifyCounterAction, StartQuestAction, AdvanceQuestAction, CompleteQuestAction | Extensible action interface. Same pattern as conditions. StartQuestAction starts a quest. AdvanceQuestAction progresses an objective. CompleteQuestAction force-completes a quest. All auto-appear in editor dropdown via reflection. |
| `Actions/GiveItemAction.cs` | GiveItemAction | IDialogueAction that adds ItemSO to InventoryManager. Fallback: sets VariableStore flag directly if InventoryManager unavailable. Auto-discovered by DialogueTypeCache reflection. |
| `ItemSO.cs` | ItemSO, ItemCategory | Item definition ScriptableObject (Karma/Item). Fields: itemName, description, icon, detailImage, category (Collectible/QuestItem/ReflectionCard/KeyItem/Consumable), isQuestItem, questId, karmaOnCollect, coinValue, flavorText. |

### Interaction System — `Assets/Scripts/Interaction/`

| File | Class | Purpose |
|------|-------|---------|
| `IInteractable.cs` | IInteractable | Interface: InteractionPrompt, CanInteract(), Interact(). |
| `InteractableBase.cs` | InteractableBase | Abstract base. Prompt, OnTargeted/OnUntargeted with default QuickOutline toggle (lazy-cached lookup via outlineLookedUp flag). Any InteractableBase with QuickOutline component auto-highlights when targeted. Subclasses can override. |
| `InteractionDetector.cs` | InteractionDetector | Trigger-based, angle-weighted target selection. Allocation-free null cleanup (manual backward for-loop). MinDotThreshold=0.3f (~72°) prevents targeting behind player. Debug.Log wrapped in #if UNITY_EDITOR. Events: OnPromptChanged, OnPromptHidden. Uses `GetComponentInParent<InteractableBase>()`. |
| `QuestItemPickup.cs` | QuestItemPickup | Extends InteractableBase. Collectible that advances a quest objective on pickup. Fields: questId, objectiveId, amount, item (ItemSO), requireQuestActive, pickupSound. On interact: AdvanceObjective, play sound, award item karma/coins, deactivate. Auto-sets prompt from item.itemName. |

### World Objects — `Assets/Scripts/Objects/`

| File | Class | Purpose |
|------|-------|---------|
| `PickupObject.cs` | PickupObject | Carriable items. Smooth lerp pickup (0.2s ease-out LerpToCarryPoint coroutine). Weight affects throw distance. Optional audio: pickupSound, dropSound, throwSound via AudioSource.PlayClipAtPoint(). CancelLerp() on Drop/Throw. |
| `PushableObject.cs` | PushableObject | Push/pull physics objects. Friction scales push speed (1/(1+friction)). canPull flag enforced by PushPullState. Dynamic prompt ("Push / Pull" vs "Push"). Optional push loop audio (3D spatial, loops during push). PlayPushLoop()/StopPushLoop() public API. RigidbodyConstraints.FreezeRotation. |
| `ClimbSurface.cs` | ClimbSurface | Climbable surfaces. climbHeight field enforced by ClimbState (caps upward movement). SurfaceNormal for lateral boundary checking. |

### VFX — `Assets/Scripts/VFX/`

| File | Class | Purpose |
|------|-------|---------|
| `SparkleVFX.cs` | SparkleVFX | Code-configured ParticleSystem burst. Faded yellow orbs (25 particles, 0.8s, alpha fade + shrink). Auto-deactivates for pooling. Play()/Play(Vector3). URP-safe material loading: tries URP Particles/Unlit shader first, falls back to Particles/Standard Unlit. |
| `SparkleVFXManager.cs` | SparkleVFXManager | Singleton pool/spawner (default 5 instances). PlayAt(pos), PlayAtTransform(target), PlayAtPlayer(). Auto-expands pool. |

### Environment — `Assets/Scripts/Environment/`

| File | Class | Purpose |
|------|-------|---------|
| `Lever.cs` | Lever | Pullable lever triggers. |
| `PressurePlate.cs` | PressurePlate | Weight-triggered plates. |
| `TriggerZone.cs` | TriggerZone | General-purpose trigger zones. |
| `FoodRainManager.cs` | FoodRainManager | "Cloudy with a Chance of Meatballs" food rain. Object-pooled 3D food falling from sky. StartRain()/StopRain() API. Auto-loads prefabs from `Assets/Prefab/Environment/Food`. Auto-detects tiny models (mesh bounds check) and applies scale multiplier. Auto-assigns fallback URP material if renderers have null materials. Creates placeholder cubes if no meshes found. Preserves FBX -90° X rotation on prefab variants. Gizmos for spawn area/ground/vanish zone. |
| `FallingFood.cs` | FallingFood | Per-food behavior (fall, tumble, shrink near ground, deactivate at groundLevel). Managed by FoodRainManager pool — do NOT add manually. |
| `GhostBlocker.cs` | GhostBlocker | Invisible wall that blocks ghosts (NavMesh obstacle). |
| `QuestTriggerZone.cs` | QuestTriggerZone | Trigger zone that advances a quest objective when player enters. RequireComponent(Collider), auto-sets isTrigger in OnValidate. Fields: questId, objectiveId, amount, singleUse, requireQuestActive. Checks for PlayerController on enter. ResetTrigger() for repeatable quests. |

### UI System — `Assets/Scripts/UI/`

| File | Class | Purpose |
|------|-------|---------|
| `DialogueUI.cs` | DialogueUI | Player bottom panel (700px fixed-width centered). Three modes: NPC-no-choices (panel hidden), NPC-with-choices (ghost panel — visuals hidden, only floating choice buttons), Player speaking (full panel). Typewriter effect. Z/X/C choices + Enter/E/Space advance. Player choice flow: highlight → player panel with "Sammy" badge → confirm → NPC response. Caches panelBorderImage + innerPanelObj for ghost panel mode. TrySubscribe + Start() fallback. |
| `ChoiceButtonUI.cs` | ChoiceButtonUI | Choice button with style colors (Empathetic=orange, Selfish=dark, Neutral=white). Shows lock reasons. SetSelected() for visual highlight on choice selection. |
| `KarmaFlowerUI.cs` | KarmaFlowerUI | Flower of Life display. Petal bloom on level-up. LevelUpFlash overlay. Animated progress bar fill with green flash (barGainColor) + ease-out curve. Runtime Image.Type.Filled enforcement in OnEnable. TrySubscribe + Start() fallback for KarmaManager subscription timing. |
| `CoinCounterUI.cs` | CoinCounterUI | Coin icon + text. Delta popup animations (+green, -red). AudioSource for coin sounds. |
| `KarmaPopupUI.cs` | KarmaPopupUI | 3-phase fly-to-target animation: pop-in (scale 0→1.3→1) → pause → fly to KarmaFlower (ease-in). Green color for gains, red for losses. PunchTargetCoroutine on landing. TrySubscribe + Start() fallback. |
| `CoinFlyUI.cs` | CoinFlyUI | Spawns 3 gold circle Images at screen center. Pop-in → pause → stagger-fly to CoinCounter (ease-in-out). Only for positive deltas. PunchTargetCoroutine on last coin. TrySubscribe + Start() fallback. |
| `NPCSpeechBubble.cs` | NPCSpeechBubble | World-space speech bubble above NPCs at worldOffset (0,5,0). Dynamic height from renderer bounds + heightPadding=0.2. Brown name badge + speech text. Auto-billboards to camera. maxDisplayChars=500 (was 80 — increased to avoid truncation; bubble grows vertically via ContentSizeFitter). Canvas sizeDelta (300,400) for room to expand. Typewriter effect (useTypewriter=true, 35 chars/sec) with IsTypewriting/SkipTypewriter() API for DialogueUI coordination. Continue prompt "Press Enter >>" shown after typewriter finishes. Fade in/out animation. Auto-migration for worldOffset, prompt text, heightPadding. TrySubscribe + Start() fallback. |
| `ResetGameButton.cs` | ResetGameButton | Resets karma/coins/VariableStore/quests/inventory, clears DialogueManager.rewardedChoices, ends active dialogue, and reloads scene. Managers survive via DontDestroyOnLoad, reset state before reload. |
| `FPSCounter.cs` | FPSCounter | Debug FPS display (top-right corner). |
| `QuestLogUI.cs` | QuestLogUI | HUD quest tracker + toast notifications. Auto-creates UI if not assigned (tracker panel top-right, toast panel top-center). Subscribes to QuestManager events. UpdateTracker() skips Hidden objectives, shows SoftHint without progress counter, shows JournalVisible with progress. Toast notifications with fade-out coroutine (3s display + 0.5s fade). Handles OnObjectiveFailed for retry/fail messages. TrySubscribe/Unsubscribe pattern. |
| `HUDManager.cs` | HUDManager | Top-level HUD coordination. Bridges manager events to UI. Subscribes to InteractionDetector for "Press E" prompt. |

### Other Scripts — `Assets/Scripts/`

| File | Class | Purpose |
|------|-------|---------|
| `ThirdPersonCamera.cs` | ThirdPersonCamera | CameraRig parent with child Main Camera. Two modes: Follow (rig at player, rotation matches player Y) + Dialogue (rig offset right via shoulderOffset=2.5, looks AT NPC from rig position for proper framing, dialogueFOV=50). Subscribes to DialogueManager events via TrySubscribe. |
| `GhostAtmosphere.cs` | GhostAtmosphere | URP Volume controller — stripped to Bloom only (all color filters removed: ColorAdjustments, WhiteBalance, Vignette, FilmGrain, ChromaticAberration). |
| `SkyboxController.cs` | SkyboxController | Day/night skybox cycle. |
| `SernaAnimCycler.cs` | SernaAnimCycler | Serna idle/talk animation variant cycling (3 idles, 3 talks). |
| `SernaInteraction.cs` | SernaInteraction | **LEGACY** — replaced by DialogueNPC. |
| `GhostFloat.cs` | GhostFloat | **LEGACY** — replaced by GhostFloatEffect. |
| `GhostSpawner.cs` | GhostSpawner | Runtime ghost spawner with randomization. |

### Editor Tools — `Assets/Editor/`

| File | Class | Menu Item | Purpose |
|------|-------|-----------|---------|
| `GameSystemsSetup.cs` | GameSystemsSetup | Karma > Setup Game Systems, Karma > Quick Setup Checklist | Auto-creates GameManagers with all singletons (incl. SparkleVFXManager, BackgroundMusicManager, InventoryManager). Validates Player/Serna. Auto-assigns SparkleVFX prefab + GameBackgroundTrack audio clip. |
| `VFXPrefabCreator.cs` | VFXPrefabCreator | Karma > Create Sparkle VFX Prefab | Creates SparkleVFX prefab at Assets/Prefab/VFX/. Auto-assigns to SparkleVFXManager in scene. |
| `UISetupTool.cs` | UISetupTool | Karma > Build UI Canvases, Build HUD/Dialogue Canvas Only | Programmatic Canvas builder matching HUDCanvas.prefab layout. Creates HUDCanvas (sort 5) + DialogueCanvas (sort 10) + ChoiceButton prefab. GUID-based sprite/audio loading. BuildHUDCanvasOnly re-wires HUDManager via WireHUDManagerToHUD. Auto-wires all references. |
| `PlayerAnimatorSetup.cs` | PlayerAnimatorSetup | Karma > Rebuild Player Animator | Rebuilds PlayerAnimatorController with all states/transitions. |
| `PlayerSetupValidator.cs` | PlayerSetupValidator | Karma > Validate Player Setup | Checks player components, tag, animator. |
| `DialogueDataCreator.cs` | DialogueDataCreator | Karma > Create Serna Intro/Return Dialogue, Karma > Create Karma Config, Karma > Create Variable Store | Creates sample dialogue assets with extensible actions. |
| `NPCDialogueCreator.cs` | NPCDialogueCreator | Karma > Chapter 1 > Setup All/Create Old Man Ghost Dialogue/Create Ananda Intro Dialogue/Create Chapter 1 Quests/Create Bread Item | Creates all Chapter 1 NPC assets: Old Man Ghost dialogue (22 nodes, 3 choices), Ananda dialogue (29 nodes, 3 choices + callback waterfall), Q1/Q2/Q3 quests, Bread item. |
| `DialogueEditorWindow.cs` | DialogueEditorWindow | Karma > Dialogue Editor | 3-panel visual editor: node list with arrows, detail panel, embedded preview. |
| `VariableStoreBrowser.cs` | VariableStoreBrowser | Karma > Variable Store | Inspect/edit flags, counters, relationships. Add/remove/search. |
| `Drawers/DialogueSOEditor.cs` | DialogueSOEditor | (CustomEditor) | Color-coded node cards, inline condition/action tags, expandable choices, node ID dropdowns. |
| `Helpers/DialogueTypeCache.cs` | DialogueTypeCache | — | Reflection-based auto-discovery of all IDialogueCondition/IDialogueAction types. |
| `QuestDebugWindow.cs` | QuestDebugWindow | Karma > Quest Debug Console | Play-mode debug window. Tab-filtered quest list (All/Active/Locked/Done+Failed). Search filter by quest ID, name, tags. Per-quest: color-coded state badge, type, tags, objectives with progress bars, +1/Max buttons. Force Start/Complete/Fail. Progression Blocker Finder (shows missing prerequisites for Locked quests). Event History log (last 20 events). VariableStore Quick View (flags with toggle, counters with +1/-1). Auto-refreshes via Repaint() in Update(). |
| `AnimatorStateSync.cs` | AnimatorStateSync | Karma > Sync NPC Animator States, Karma > Sync Selected NPC Animator | Auto-adds missing animation states to NPC AnimatorController from DialogueNPC's defaultIdleClips[], defaultTalkClips[], and DialogueSO nodeAnimation fields. |
| `FoodRainSetup.cs` | FoodRainSetup, FoodRainManagerEditor | Karma > Setup Food Rain, Karma > Fix Food FBX Materials | Creates FoodRainManager in scene, auto-loads prefabs. Fix FBX Materials diagnoses each FBX model (renderers, materials, mesh bounds, shader) and fixes materialImportMode to ImportViaMaterialDescription + reimports. Custom Inspector adds "Fix FBX Materials" + "Load Prefabs" buttons + runtime Start/Stop/Clear controls. |
| `Helpers/DialogueEditorStyles.cs` | DialogueEditorStyles | — | Shared colors, GUIStyles, drawing helpers for editor toolkit. |

---

## Design Patterns

1. **State Machine** — PlayerStateMachine (generic) → 6 concrete states; GhostNPC uses local switch-based SM
2. **Singleton** — KarmaManager, DialogueManager, WalletManager, QuestManager, InventoryManager, VariableStore (all DontDestroyOnLoad)
3. **ScriptableObject Data** — DialogueSO, QuestSO, KarmaConfig, VariableStore via CreateAssetMenu
3b. **Immutable Definition + Mutable State** — QuestSO (immutable at runtime) + QuestRuntimeState (plain C# [Serializable], save-friendly)
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

### Quest Flow
1. Quest definitions created as QuestSO ScriptableObjects (Karma/Quest in Create menu)
2. QuestManager registers all QuestSO[] on Awake, builds questRegistry Dictionary
3. On Start, InitializeQuestStates() sets Available for quests with no/met prerequisites, auto-starts if configured
4. Dialogue: StartQuestAction fires → QuestManager.StartQuest() → quest becomes Active, initializes objective progress
5. World objects: QuestTriggerZone (player enters zone) / QuestItemPickup (player interacts) → AdvanceObjective()
6. Dialogue nodes: AdvanceQuestAction fires for Talk-type objectives
7. OnObjectiveUpdated event → QuestLogUI updates tracker (skips Hidden, shows SoftHint as text-only, JournalVisible with progress)
8. Toast notifications: "New Quest:", "Objective Complete:", "Quest Complete:", "Quest Failed:", "Try Again:"
9. When all required objectives met → auto-transitions to Completed → CompleteQuest() awards rewards
10. AwardRewards: karma → KarmaManager, coins → WalletManager, flags → VariableStore, questsToUnlock → UnlockFollowUpQuests
11. Quest chaining: UnlockFollowUpQuests checks prerequisites → sets Available → auto-starts if configured
12. Fail-soft: FailObjective() cascade: canFail=false→ignore, retryAllowed→reset progress, optional→skip, fallbackDialogueId→set VariableStore flag, else→FailQuest

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
│   │   ├── Serna_Return.asset
│   │   ├── OldManGhost_Intro.asset
│   │   └── Ananda_Intro.asset
│   ├── Items/
│   │   └── Bread.asset
│   └── Quests/
│       ├── Q1_FindAnanda.asset
│       ├── Q2_ObserveSelf.asset
│       └── Q3_HungerTest.asset
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
- Setup Food Rain — creates FoodRainManager, auto-loads food prefabs
- Fix Food FBX Materials — diagnoses and fixes FBX material import (for invisible food models)
- Create Sparkle VFX Prefab — creates SparkleVFX prefab
- Sync NPC Animator States — auto-adds missing animation states to NPC AnimatorControllers
- Quest Debug Console — play-mode quest state inspector, force-advance, event history, VariableStore viewer
- Chapter 1 > Setup All Chapter 1 Assets — creates all Ch1 NPC dialogues, quests, items
- Chapter 1 > Create Old Man Ghost Dialogue — 22-node dialogue with 3 choice points
- Chapter 1 > Create Ananda Intro Dialogue — 29-node dialogue with callback waterfall + GiveItemAction
- Chapter 1 > Create Chapter 1 Quests — Q1_find_ananda, Q2_observe_self, Q3_hunger_test
- Chapter 1 > Create Bread Item — quest item for moral choice

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
        ├── SpeechText (white, max 500 chars, auto-height via ContentSizeFitter)
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
- **Dialogue mode**: Rig positioned at player + right*shoulderOffset + up*verticalOffset. Rig looks AT the NPC from its offset position (not parallel to dirToNPC). The shoulder offset allows the camera to see around the player's body. NPC appears center-right of frame.
- Key: rig rotation uses `LookRotation(npcCenter - transform.position)` — ensuring proper framing regardless of offset amount.

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
Used in: DialogueUI, NPCSpeechBubble, KarmaFlowerUI, ThirdPersonCamera, QuestLogUI, HUDManager

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
- Minigame system (dialogue → minigame → quest objective completion flow)
- Quest Journal full-screen panel (Tab key toggle, full quest details + history)
- Map markers for MapMarkerVisible objectives
- NPC Relationship system (uses VariableStore.relationships)
- Cat sidekick companion AI
- Chapter 1 level design: place Old Man Ghost + Ananda NPCs, wire dialogues, add QuestTriggerZone at temple
- Moral choice encounter: hungry NPC on path after Ananda gives bread (Q3_hunger_test)
