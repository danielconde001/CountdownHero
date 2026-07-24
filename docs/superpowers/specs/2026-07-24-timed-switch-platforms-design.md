# Timed Switch & Platform Design Doc

## Overview

A switch-and-platform system for the platforming layer of Countdown Hero.
The player activates a switch, which starts a readable countdown. After the
countdown elapses, one or more platforms activate: appear, move, rotate, scale,
or fire custom events. After a hold duration, the platforms return to their
starting state.

## Components

### TimedSwitch

One component on the switch GameObject. It owns how the player triggers the
switch, then fans out to linked `TimedPlatform` targets.

**Fields:**

| Field | Type | Description |
|---|---|---|
| `mode` | `ActivationMode` enum | How the player triggers the switch |
| `targets` | `TimedPlatform[]` | Platforms to activate |
| `cooldown` | `float` | Seconds before the switch can be re-triggered |
| `interactPrompt` | `GameObject` | Optional prompt shown only while the player can press interact |

**ActivationMode enum:**

| Value | Implementation |
|---|---|
| `OnTriggerEnter` | Trigger collider. On `OnTriggerEnter2D` with `PlayerController2D`, activate. |
| `OnInteractPress` | Trigger collider. Player inside and E key pressed, activate. Shows the optional prompt. |
| `OnAttacked` | Public `OnHit()` method called by a future player attack, activate. |
| `OnJumpedOn` | Non-trigger collider. Player lands from above, detected with contact normals, activate. |

Input uses `Keyboard.current` from the new Input System, matching the existing
codebase.

**Runtime behavior:**

- Tracks whether the switch is on cooldown.
- On activation, iterates `targets` and calls `Activate()` on each non-null target.
- Uses `TryGetComponent<PlayerController2D>()` for player detection, matching `CombatEncounterTrigger`.
- Only `OnInteractPress` tracks player proximity and prompt visibility.
- Collider setup happens in `Awake` for trigger/jump modes. `OnAttacked` does not disable colliders, so future attack hitboxes/raycasts can still find the object.

### TimedPlatform

One component on the platform GameObject. It owns the countdown, active duration,
initial state, and behavior mode.

**Fields:**

| Field | Type | Description |
|---|---|---|
| `countdownDuration` | `float` | Delay before the transition takes effect |
| `activeDuration` | `float` | How long the platform stays in the triggered state after the transition completes |
| `startActive` | `bool` | Initial state toggle |
| `mode` | `BehaviorMode` enum | What the platform does when activated |
| `moveTarget` | `Vector3` | World-space target position for Move mode |
| `moveDuration` | `float` | Tween duration for Move mode |
| `rotateTarget` | `Vector3` | Target world euler angles for Rotate mode |
| `rotateDuration` | `float` | Tween duration for Rotate mode |
| `scaleTarget` | `Vector3` | Target local scale for Scale mode |
| `scaleDuration` | `float` | Tween duration for Scale mode |
| `countdownDisplay` | `TextMesh` | Optional text display for readable platform countdown feedback |
| `onActivated` | `UnityEvent` | Fired when the platform reaches the active state |
| `onDeactivated` | `UnityEvent` | Fired when the platform reaches the inactive state |

**BehaviorMode enum:**

| Value | What happens |
|---|---|
| `ToggleVisibility` | Enable/disable child `Renderer` and `Collider2D` components |
| `Move` | Lerp from the original position to `moveTarget`, then back |
| `Rotate` | Lerp from the original rotation to `rotateTarget`, then back |
| `Scale` | Lerp from the original scale to `scaleTarget`, then back |
| `Custom` | Fire `onActivated` / `onDeactivated` only |

## Sequence

`startActive` is an initial-state toggle, not a command to skip the countdown.

```text
Activate() called
  -> if a sequence is already running, ignore
  -> show/update optional countdown display
  -> if startActive is false:
       start inactive
       wait countdownDuration
       transition active
       fire onActivated
       wait activeDuration
       transition inactive
       fire onDeactivated
  -> if startActive is true:
       start active
       wait countdownDuration
       transition inactive
       fire onDeactivated
       wait activeDuration
       transition active
       fire onActivated
```

For transform modes, `activeDuration` starts after the transition finishes. This
keeps "active for 4 seconds" readable to designers.

## Data Flow

```text
Player triggers switch
  -> TimedSwitch checks cooldown
  -> TimedSwitch calls TimedPlatform.Activate() on each target
  -> Each TimedPlatform runs its own sequence coroutine independently
  -> Transform tweens complete inside that platform's sequence
  -> Platform fires UnityEvents at completed state transitions
```

## Edge Cases

- Re-trigger while active: switch cooldown plus platform idempotent guard.
- Multiple switches, same platform: supported; duplicate `Activate()` calls are ignored while running.
- Player leaves trigger mid-sequence: sequence continues.
- Platform disabled/destroyed mid-tween: `OnDisable` stops its sequence coroutine.
- Zero durations: transitions resolve immediately without divide-by-zero.
- Missing references: log warnings and fail gracefully.
- Nested platform art: visibility mode affects child renderers/colliders for 2.5D setups.

## File Plan

Two new runtime MonoBehaviour scripts under `Assets/Scripts/`:

- `TimedSwitch.cs`
- `TimedPlatform.cs`

No new ScriptableObject assets, no new scenes, and no new prefabs are required
for the component implementation.

## Non-Goals

- No tweening library dependency.
- No runtime mode switching.
- No save/load state.
- No networked or multiplayer support.
