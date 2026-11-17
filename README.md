# Touch Sticks

Virtual on-screen joysticks for mobile, drawn with IMGUI. No Canvas, no EventSystem, no prefabs, no assets (textures are generated at runtime).

- Namespace: `Bipolar.TouchSticks`, own assembly `Bipolar.TouchSticks`.
- Input backends: legacy Input Manager and Input System (chosen at compile time, see below).

## When to use

- A mobile game needs one or more on-screen sticks (movement, aiming, steering) and a "finger is down" flag.
- The stick should appear only on touch devices, with no extra UI setup.

## When NOT to use

- You need a stick that is part of a Canvas layout, animated or skinned: build it with UI instead.
- You need dozens of sticks (IMGUI allocates a little).

## Quick start

1. Add one `VirtualJoystick` component per stick. A twin-stick game has two, for example one on the left half of the screen
   (movement) and one on the right half (aiming).
2. Read from each stick:
   - `Value` - deflection after the dead zone (length 0..1, or exactly 1 with `Normalize Output`),
   - `IsPressed` - whether a finger controls the stick (for example "is firing"),
   - events `OnPressed` / `OnReleased`.
3. From game code set `Color` (alpha is controlled by the stick state), `Visible` (false = the stick works but is not drawn)
   and `InputEnabled` (false = the stick ignores new touches, drops the current one and is not drawn, for example while paused).

```csharp
var move = gameObject.AddComponent<VirtualJoystick>();
move.Configure(new Rect(0f, 0f, 0.5f, 1f), new Vector2(0f, 0f), 0.12f, false);

var aim = gameObject.AddComponent<VirtualJoystick>();
aim.Configure(new Rect(0.5f, 0f, 0.5f, 1f), new Vector2(1f, 0f), 0.2f, true);
aim.Visible = false;

// every frame
direction = move.Value;
isFiring = aim.IsPressed;
```

`Configure(captureZone, idleAnchor, deadZone, normalizeOutput)` sets the main parameters when the component is added from code.
Sticks added in the editor are configured in the Inspector.

## Input System actions

Each stick mirrors its `Value` into a virtual Input System device `TouchStick`, so a game only needs an action binding and no
code that knows about this module:

1. Add a `VirtualJoystick` (leave `Write To Input System` on).
2. In the Input Actions editor bind an action (Vector2) to `<TouchStick>/stick`.
   With several sticks give each an `Input Usage` (for example `Left`, `Right`) and bind `<TouchStick>{Left}/stick`.
3. Read the action as usual. Rebinding and control schemes work like for any other device.

Notes:

- The device exists only while a stick is enabled and touch is in use (see Platform check); on desktop the binding is just silent.
- The dead zone is applied by the stick. Do not add a `stickDeadzone` processor to the binding, it would apply twice.
- The device is not a `Pointer` and not a `Gamepad`, so it never mixes with a real mouse or pad. Code that tells devices apart
  (for example "delta in pixels" for a `Pointer`, "rate" for everything else) treats it as a stick.
- Turn `Write To Input System` off to read `Value` from code only.

## Main API

| Type / member | Purpose |
|---|---|
| `VirtualJoystick` | One stick. One component per stick. |
| `Value`, `IsPressed` | Current deflection and "finger is down". |
| `OnPressed`, `OnReleased` | Raised when a finger takes or leaves the stick (also when the stick is disabled). |
| `Color`, `Visible`, `InputEnabled`, `DeadZoneMode` | Runtime settings. |
| `TouchStickDevice` | Virtual Input System device (`<TouchStick>/stick`) that carries the stick value to actions. |
| `Configure(...)` | Sets the main parameters from code. |
| `DeadZoneMode` | `Cut` or `Rescale`, see below. |
| `TouchPointers` | Shared per-frame pointer snapshot. `Enabled` tells whether touch is in use. |
| `IPointerSource` | Backend that reads raw input. Plug in your own with `TouchPointers.SetSource(...)`. |

## Platform check

Sticks do nothing unless `TouchPointers.Enabled` is true: on mobile platforms, or when the page address contains `touch=1`
(then the mouse emulates one finger, handy for testing a WebGL build in a desktop browser). Games can also use
`TouchPointers.Enabled` to decide whether to read touch or mouse/keyboard input.

## Inspector fields

Layout (screen fractions, origin bottom-left):

| Field | Meaning |
|---|---|
| Capture Zone | Area where a new touch can grab this stick. |
| Idle Anchor | Resting position inside the screen reduced by the margin: (0,0) bottom-left corner, (1,1) top-right. |
| Radius Screen Height / Min Radius Pixels | Stick radius as a fraction of the screen height, with a pixel minimum. |
| Margin Screen Height / Min Margin Pixels | Distance of the resting stick from the screen edge. |

Behaviour:

| Field | Meaning |
|---|---|
| Dead Zone | Deflection below this value gives `Value == (0,0)`. |
| Dead Zone Mode | How deflection above the dead zone is treated, see below. Ignored when `Normalize Output` is on. |
| Normalize Output | `Value` always has length 1 (or 0 inside the dead zone). |
| Floating | The stick base appears where the finger lands. Otherwise it stays at the resting position. |
| Follow Finger | When the finger leaves the radius, the stick base follows it. |

Look: `Color`, `Visible`. The `States` header shows the id of the pointer currently held (debugging only).

### Dead zone modes

With a dead zone of 0.12 and a finger at 0.56 of the radius:

| Mode | Value length | Behaviour |
|---|---|---|
| `Cut` (default) | 0.56 | Zero below the dead zone, the raw deflection above it. The value jumps from 0 to 0.12 at the edge. |
| `Rescale` | 0.50 | Zero below the dead zone, then grows smoothly from 0 at the edge to 1 at full deflection. Better for analog control (steering). |

With `Normalize Output` the value is always length 1 outside the dead zone, so the mode has no effect.

### Fixed stick vs. neutral point at the first touch

Where "no deflection" is depends on `Floating`:

- **Floating on:** the neutral point is where the finger first lands. The base is drawn there and moves nothing else.
- **Floating off:** the neutral point is the resting position (`Idle Anchor`), wherever the finger lands in the capture zone.
  A touch away from the stick gives an immediate deflection towards the finger. With `Follow Finger` on, the base is dragged
  once the finger is outside the radius, with it off the deflection stays at length 1.
  Size the `Capture Zone` close to the stick so touches do not start with a big deflection.

There is no mode that draws the stick at a fixed place but treats the first touch as neutral: what you see is always what is measured.

## How it works

- `TouchPointers` reads input at most once per frame (compares `Time.frameCount`), no matter how many sticks exist.
  It also keeps the set of claimed pointers, so two sticks with overlapping zones never grab the same finger.
- A hidden `Updater` (private, nested in `VirtualJoystick`, so it cannot be added in the editor) is created at runtime when
  the first stick is enabled. It gathers input, updates every stick and draws all visible ones (on `Repaint` only).
  Sticks themselves have no `Update` or `OnGUI`.
- The `Updater` runs at execution order -100. Code that reads `Value` in a normal `Update` always sees the current frame.
  Code that sets `InputEnabled` should run earlier if it must take effect in the same frame (for example order -200).
- Static state is reset on `SubsystemRegistration`, so the module works with Domain Reload disabled.

## Input backends

`IPointerSource` abstracts raw input (`Collect(List<Pointer>)`). The default is chosen at compile time:

- `InputSystemPointerSource` when Active Input Handling includes the Input System (`ENABLE_INPUT_SYSTEM`) and the
  `com.unity.inputsystem` package is installed (the asmdef sets `BIPOLAR_TOUCHSTICKS_INPUT_SYSTEM` through `versionDefines`),
- otherwise `LegacyPointerSource` (`ENABLE_LEGACY_INPUT_MANAGER`).

With both handlers active the Input System wins. Both sources use the mouse as one finger when there is no touchscreen.
Call `TouchPointers.SetSource(...)` to plug in your own source.

## Don't

- Don't read `Value` in `Update` code that runs before the `Updater` (order below -100) and expect the current frame.
- Don't add a second input path for the same finger in the game (for example a swipe binding like
  `<Touchscreen>/primaryTouch/delta` in the same action next to a stick binding): the finger would steer twice.
- Don't rely on `TouchPointers.Enabled` on desktop without `touch=1`: the sticks are intentionally inactive there.

## Dependencies

- Unity Input System package (`com.unity.inputsystem`) for `InputSystemPointerSource`. The asmdef references `Unity.InputSystem`.
- The type `Pointer` in this namespace has the same name as `UnityEngine.InputSystem.Pointer`. Qualify it or use an alias
  when both namespaces are imported in one file.

## Known limits

- `InputSystemPointerSource` compiles, but has not been tested with a real touchscreen yet.
- No preview of the sticks outside Play Mode.
- Rendering uses IMGUI, which allocates a little; fine for two sticks, not meant for dozens.
