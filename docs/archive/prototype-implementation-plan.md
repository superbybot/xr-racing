# Racing Prototype — Core Driving (VR + PC), using Karting_Reference as source

## Context

`xr-racing-app` has the bootstrap assets in place (`Assets/References/Karting Reference`,
`Assets/References/Input Reference`, Photon Fusion 2 fully resolved) but **no gameplay
code exists yet** — the bootstrap plan explicitly deferred scripts, and no prior plan
document or memory records a gameplay design. This plan starts that work: get a kart
actually driving, on a real track, testable both in VR (grabbable wheel + controller
buttons) and on PC (keyboard, playable straight in the Editor Game view) — no laps, AI,
or multiplayer yet. Everything after this (laps/UI, AI opponents, Fusion netcode) builds
on top of this driving foundation.

## Confirmed decisions (from this session)

- **PC input**: keyboard, driven in the Editor Game view without a headset.
- **Scope**: driving mechanics only — kart spawns, drives convincingly, nothing else.
- **New dependencies**: bring in UniTask + R3 (matches xr-sandbox's approach) rather than
  writing a dependency-light seat/exit flow.
- **Track**: an existing prefab from `Assets/References/Karting Reference/Prefabs/...`,
  not a hand-built layout.
- **VR controls**: the grabbable steering wheel for steering; **either controller's
  thumb-reachable face button = accelerate**, **either controller's index trigger =
  brake/reverse**. No physical throttle/joystick prop — `XRJoystick.cs` and
  `CarInputManager.cs` are not used.
- **Interaction framework**: prefer Meta XR SDK interaction components over Unity XR
  Interaction Toolkit content/scripts wherever Meta's SDK covers the need — this is
  now a standing project rule (see below), and is why the wheel is built from Meta's
  own grab/rotate components rather than a ported XRI-based script.

## What I found in the reference project that changes the original bootstrap assumption

The bootstrap plan assumed we'd port `Car Demo`'s `CarController.cs` (a generic
`WheelCollider`-driven car). That script has no relationship to the actual kart models —
the drivable karts (`Assets/References/Karting Reference/Prefabs/KartClassic/...`) are
built for Unity Karting Microgame's own **`ArcadeKart.cs`** (raycast suspension +
`WheelCollider`s for surface friction/visuals, driven by an `IInput`/`InputData`
abstraction already used for exactly this purpose:
`KartSystems/Inputs/BaseInput.cs` defines `IInput { InputData GenerateInput(); }`, and
`KartClassic_Player`'s base prefab already carries a `KeyboardInput : BaseInput`
component (confirmed by matching script GUIDs against `BaseKartClassic.prefab`).

So the plan is: **port `ArcadeKart.cs` + its `IInput` pattern (this is the actual kart
physics, already art-matched to the kart models)**, and write two new `IInput`
implementations — one for VR, one for PC — instead of reinventing car physics. This is a
much smaller, more faithful use of "the kart assets as reference" than the original
Car Demo route.

One incompatibility to flag: xr-sandbox's `KeyboardInput.cs` uses the legacy
`UnityEngine.Input` API. `xr-racing-app`'s `ProjectSettings/ProjectSettings.asset` has
`activeInputHandler: 1` (**Input System package only** — legacy Input Manager calls
throw/no-op). So the PC input script must be written fresh against the new Input System,
not ported verbatim.

## Files to port verbatim (Claude does this directly — plain text copy, no logic changes)

From `/Users/byronbautista/xr-sandbox/xr-sandbox-app/Assets/App/References/Karting_Reference/Scripts/`:
- `KartSystems/Inputs/BaseInput.cs` → `Assets/Gameplay/Scripts/KartSystems/Inputs/BaseInput.cs`
- `KartSystems/ArcadeKart.cs` → `Assets/Gameplay/Scripts/KartSystems/ArcadeKart.cs`
- `KartSystems/KartAnimation/KartAnimation.cs` → `Assets/Gameplay/Scripts/KartSystems/KartAnimation/KartAnimation.cs`
- `KartSystems/KartAnimation/KartPlayerAnimator.cs` → `Assets/Gameplay/Scripts/KartSystems/KartAnimation/KartPlayerAnimator.cs`
- `Utilities/MinMaxParameters.cs` → `Assets/Gameplay/Scripts/KartSystems/Utilities/MinMaxParameters.cs`

These four (plus `MinMaxParameters`) are the confirmed dependency set for
`KartClassic_Player` (verified by cross-referencing script GUIDs against
`BaseKartClassic.prefab`). **Do a headless Editor compile after this pass** (per your
global Unity-verification workflow) — if `ArcadeKart.cs` references any other
`Utilities/*.cs` helper not listed here, the compiler will name it; port that file too
rather than guessing further ones up front.

From `/Users/byronbautista/xr-sandbox/xr-sandbox-app/Assets/App/Demos/Car Demo/Scripts/`:
- `CarTeleportAnchor.cs` → `Assets/Gameplay/Scripts/Vehicle/CarTeleportAnchor.cs`

(`XRJoystick.cs` and `CarInputManager.cs` are **not** ported — superseded by the
controller-button VR input described below.)

## Local rule change: prefer Meta XR SDK for interaction, not XR Interaction Toolkit content

You asked to use Meta XR SDK stuff for placeholder assets/interaction as much as
possible, and to record this as a standing project rule. This reverses the previous
plan for the wheel:

- **`XRSteeringWheel.cs` is no longer ported.** It's a custom script built on Unity's
  **XR Interaction Toolkit** (`XRBaseInteractable`, `IXRInteractor`, `NearFarInteractor`)
  — that's exactly the kind of thing you asked to avoid. It was also what pulled in the
  missing-mesh gap I found last turn (`Primitive_Torus.fbx` from XRI's Starter Assets
  sample) — dropping the script removes that gap entirely, no sample import needed.
- **Instead, the wheel is built from Meta XR Interaction SDK's own grab-and-rotate
  components**, which are already resolved in this project
  (`com.meta.xr.sdk.interaction`, confirmed present in `Library/PackageCache`):
  `Grabbable` + `HandGrabInteractable` + **`TwoGrabRotateTransformer`**
  (`Runtime/Scripts/Interaction/Grabbable/TwoGrabRotateTransformer.cs`) — Meta's
  built-in component for exactly this "grab with both hands and twist" interaction,
  with an axis constraint and angle limits configurable in the Inspector (no custom
  grab-tracking code needed at all).
- The wheel's **visual mesh** is a plain Unity built-in primitive (a flattened Cylinder
  standing in for a disc/ring) rather than any imported sample FBX — zero new asset
  dependencies for the placeholder look.
- **`CarTeleportAnchor.cs` stays XRI-based** (`TeleportationAnchor`,
  `LocomotionMediator`, `ContinuousMoveProvider`) — this is locomotion/scene-transition
  plumbing, not a grabbable "interaction," and Meta's SDK doesn't provide a competing
  locomotion system; it interoperates with XRI's Locomotion System for teleport-style
  movement even in Meta-first projects. The "prefer Meta" rule applies to
  interactable/grabbable objects, not this.
- **New standing project rule** (add to `xr-racing/CLAUDE.md` guardrails once this plan
  is approved — Claude does this directly, see Step 2): *"Prefer Meta XR SDK
  interaction components (`Grabbable`, `HandGrabInteractable`, transformers) over Unity
  XR Interaction Toolkit equivalents for new grabbable/interactable objects. XR
  Interaction Toolkit remains a required dependency for locomotion (Teleportation/
  Locomotion System), since Meta's SDK doesn't provide an equivalent for that."*

This lands with its original namespace intact at copy time; the namespace rename to fit
this project happens in the aider pass below, alongside real code changes, so it's one
round-trip instead of two.

## New/adapted content (aider does this — real code, not a mechanical copy)

1. **Namespace fixup** on the one remaining ported Vehicle script
   (`Assets/Gameplay/Scripts/Vehicle/CarTeleportAnchor.cs`): rename its namespace from
   `App.Demos.CarDemo.Scripts` to `XrRacing.Gameplay.Vehicle`. No other logic changes.

2. **`Assets/Gameplay/Scripts/Input/KartKeyboardInput.cs`** — new `IInput`
   implementation (`: KartGame.KartSystems.BaseInput`) for PC, using the new Input
   System (`Keyboard.current`), no `.inputactions` asset needed: A/D or Left/Right →
   `TurnInput`, W → `Accelerate`, S → `Brake`.

3. **`Assets/Gameplay/Scripts/Input/XRWheelInput.cs`** — new `IInput` implementation for
   VR:
   - `TurnInput` ← read directly off the wheel's `Transform` (see Scene setup below —
     the wheel is a plain primitive with Meta's `TwoGrabRotateTransformer` doing the
     actual grab/rotate physics, not a custom script), via a signed angle between the
     wheel's current and starting local rotation around its spin axis, normalized to
     -1..1 against a configurable max-angle field.
   - `Accelerate` ← `OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.Touch)` (A/X
     face button, either hand — matches "thumb" and "either" from your description).
   - `Brake` ← `OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.Touch) > threshold`
     OR the same for `SecondaryIndexTrigger`, whichever controller enum exposes "either
     hand" most directly — take the max of both hands' trigger axis.
   - Uses `OVRInput` (Meta XR SDK Core, already installed) rather than raw Input System
     XR device paths — more reliable for Quest Touch controllers and avoids fighting
     OpenXR input layout bindings for a prototype.

## Package additions (Claude does this directly — manifest edit + GUID-preserving asset copy, same pattern as the bootstrap plan)

R3 in xr-sandbox is installed via NuGetForUnity (not a UPM package), so it's a folder
copy, not a manifest line:

- Copy (with `.meta`s) from `xr-sandbox-app/Assets/Packages/` into
  `xr-racing-app/Assets/Packages/`:
  `R3.1.3.0`, `ObservableCollections.3.3.4`, `Microsoft.Bcl.AsyncInterfaces.6.0.0`,
  `Microsoft.Bcl.TimeProvider.8.0.0`, `System.ComponentModel.Annotations.5.0.0`,
  `System.Runtime.CompilerServices.Unsafe.6.0.0`, `System.Threading.Channels.8.0.0`
  (confirmed via xr-sandbox's `packages.config` — R3's full dependency set, all 7 needed
  together or R3 won't resolve).
- Add to `xr-racing-app/Packages/manifest.json` `dependencies`:
  - `"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"`
  - `"com.github-glitchenzo.nugetforunity": "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity"`
    (keeps NuGet package management available for future additions; not strictly
    required for these 7 folders to compile, but matches xr-sandbox's setup).
  - `"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity"`
    (already used in xr-sandbox — gives Claude live, verified tool access into the open
    Editor for the scene-setup step below, instead of a blind headless script).

## Scene setup — Claude, via Unity MCP (was manual GUI work)

Scene composition and prefab wiring are not safely text-editable (fragile YAML, and
editing scenes outside the Editor risks breaking GUID/component references per this
project's own guardrails), so this was originally scoped as work for you to do by hand
in the Editor. With Unity MCP connected, Claude does it instead via live, verified MCP
tool calls (create GameObject, instantiate prefab, add/set component, screenshot) —
same end result, no blind batch-mode scripting, and no hand-dragging:

1. Create `Assets/Gameplay/Scenes/DrivingPrototype_PC.unity`:
   - Instantiate one track prefab — recommend
     `Assets/References/Karting Reference/Prefabs/TrainingTracks/OvalTrack_Training.prefab`
     (simplest loop; swap later for `Prefabs/Tracks/*` if you want the full-detail
     version).
   - Instantiate `Prefabs/KartClassic/KartClassic_Player.prefab`, remove/disable its
     existing `KeyboardInput` component, add `KartKeyboardInput`.
   - Plain `Camera` + Cinemachine 3rd-person follow vcam targeting the kart (check via
     MCP whether `KartClassic_Player` already ships its own Cinemachine camera child
     before building one from scratch).
   - No XR rig in this scene.
2. Create `Assets/Gameplay/Scenes/DrivingPrototype_VR.unity`:
   - Same track prefab.
   - Same kart prefab, but add `XRWheelInput` instead (remove/disable `KeyboardInput`).
   - Meta XR Camera Rig (Building Block) positioned in the kart's driver seat.
   - Build the wheel at the wheel mount: a plain Cylinder primitive (flattened into a
     disc) with Meta's `Grabbable` + `HandGrabInteractable` + `TwoGrabRotateTransformer`
     (constrained to spin about one axis, angle-limited similar to the original
     ±450° range), wired to `XRWheelInput`'s wheel-transform reference field via MCP.
   - `CarTeleportAnchor` needs an XRI 3 Locomotion System + Teleportation Provider in
     the scene (standard XRI/Meta rig prerequisite — this piece stays XRI per the
     locomotion note above) — check via MCP whether one of the imported Meta samples
     already has this set up to copy from, rather than hand-building it.
3. Confirm via MCP that ground/track colliders sit on layers 9/10/11
   (`Ground`/`Environment`/`Track`) — `ArcadeKart.cs`'s suspension raycast is hardcoded
   to that layer mask.
4. Run the headless Editor compile check (per your global workflow) after each aider
   batch, and use MCP to confirm no "Missing Script" warnings in either scene before
   treating a step as done.

**Still genuinely yours to do** (not automatable): the one-time Editor-open/MCP-connect
in Step 1 below, and actual on-headset VR testing at the end — I can wire and verify
structure via MCP, but I can't feel whether the wheel/pedal feel is right.

## Folder structure (after this pass)

```
xr-racing-app/Assets/
├── Gameplay/                              # new — this pass's own code, not a "Reference"
│   ├── Scripts/
│   │   ├── KartSystems/                   # ported from Karting_Reference (verbatim)
│   │   │   ├── ArcadeKart.cs
│   │   │   ├── Inputs/
│   │   │   │   └── BaseInput.cs           # IInput, InputData, BaseInput
│   │   │   ├── KartAnimation/
│   │   │   │   ├── KartAnimation.cs
│   │   │   │   └── KartPlayerAnimator.cs
│   │   │   └── Utilities/
│   │   │       └── MinMaxParameters.cs
│   │   ├── Vehicle/                        # ported from Car Demo, namespace-fixed by aider
│   │   │   └── CarTeleportAnchor.cs        # still XRI-based — locomotion, not "interaction"
│   │   └── Input/                          # new, written by aider
│   │       ├── KartKeyboardInput.cs        # PC — new Input System
│   │       └── XRWheelInput.cs             # VR — reads wheel Transform + OVRInput buttons
│   └── Scenes/
│       ├── DrivingPrototype_PC.unity       # new — built by Claude via Unity MCP
│       └── DrivingPrototype_VR.unity       # new — wheel = primitive + Meta Grabbable/
│                                            # HandGrabInteractable/TwoGrabRotateTransformer,
│                                            # built by Claude via Unity MCP
├── Packages/                                # new — NuGet DLLs for R3 (copied w/ .meta)
│   ├── R3.1.3.0/
│   ├── ObservableCollections.3.3.4/
│   ├── Microsoft.Bcl.AsyncInterfaces.6.0.0/
│   ├── Microsoft.Bcl.TimeProvider.8.0.0/
│   ├── System.ComponentModel.Annotations.5.0.0/
│   ├── System.Runtime.CompilerServices.Unsafe.6.0.0/
│   └── System.Threading.Channels.8.0.0/
└── References/                              # existing — untouched, read-only source
    ├── Karting Reference/…
    └── Input Reference/…
```

## Aider fit

Each aider task below targets one or two explicit files and spells out exact class,
field, and type names in the `--message` so the local model isn't guessing at API shape
— per your global aider workflow this keeps tasks small enough for `--edit-format diff`
to apply cleanly:

- **Step 1** (namespace rename) is a pure mechanical SEARCH/REPLACE — lowest risk, good
  first task to confirm the local model + `diff` format round-trips correctly in this
  repo.
- **Steps 2 & 3** (new files) pass `BaseInput.cs` as read-only context via aider's
  `--read` flag, so the model sees the real `InputData`/`IInput` shape instead of
  relying on the description in the message. This is added on top of the invocation
  pattern in your CLAUDE.md specifically because these tasks require the model to match
  an external type's shape exactly (`InputData`'s three fields) rather than just editing
  the file it's given.

## Verification

- Headless Editor batch compile, `grep "error CS"` on the log — clean after each aider
  batch (per your global Unity C# verification workflow).
- Open `DrivingPrototype_PC.unity`, Play, confirm WASD drives the kart around the oval
  with visible wheel roll/steer and working brake.
- Open `DrivingPrototype_VR.unity` on/with a Quest (or Editor + Meta Link), confirm:
  grabbing the wheel steers, the A/X button accelerates from either hand, either
  trigger brakes/reverses.
- No changes to any existing tracked asset's GUID; all new files get committed only
  after Unity has generated their `.meta`s on next Editor open (do not hand-author
  `.meta` files).

## Execution sequence for aider

**Step 0 — Claude, directly (unblocks your part — do this before anything else):**
- Edit `xr-racing-app/Packages/manifest.json` to add the 3 dependency lines listed
  above (UniTask, NuGetForUnity, Unity MCP) — nothing else yet, so the one Editor-open
  in Step 1 resolves everything needed for the MCP bridge in a single pass.

**Step 1 — You (do this now, then everything else runs unattended):**
- Open the Unity Editor once so Package Manager resolves the new manifest entries
  (needs network) and the Unity MCP bridge starts listening.
- Connect the Unity MCP server to this Claude Code session per its own setup docs
  (one-time).
- Let me know once it's connected — everything from Step 2 onward needs no further
  input from you until it's time to test on-headset.

**Step 2 — Claude, directly (docs):**
- `git mv xr-racing/Markdown/xr-racing-bootstrap-plan.md xr-racing/docs/xr-racing-bootstrap-plan.md`
  (consolidate the existing plan doc into `docs/` — `Markdown/` is being retired as a
  folder name in favor of the more conventional `docs/`), then remove the now-empty
  `Markdown/` folder.
- Write this plan as a standalone markdown doc at
  `xr-racing/docs/XR-Racing-Prototype-Plan.md`, so it's a permanent repo artifact
  rather than only living in the ephemeral plan-mode file.
- Append a new guardrail to `xr-racing/CLAUDE.md`'s "CI/CD & Agent Guardrails" list:
  *"5. Prefer Meta XR SDK interaction components (Grabbable, HandGrabInteractable,
  transformers) over Unity XR Interaction Toolkit equivalents for new
  grabbable/interactable objects. XR Interaction Toolkit remains a required dependency
  for locomotion (Teleportation/Locomotion System), since Meta's SDK doesn't provide an
  equivalent for that."*

**Step 3 — Claude, directly (file operations, no aider):**
1. `mkdir -p` the new folders under `Assets/Gameplay/Scripts/...` as listed above.
2. `cp` (no `.meta` needed — fresh files, Unity mints new GUIDs) the 5 verbatim-port
   scripts from xr-sandbox into their `Assets/Gameplay/Scripts/...` destinations listed
   above.
3. `cp` `CarTeleportAnchor.cs` into `Assets/Gameplay/Scripts/Vehicle/` (pre-namespace-fix;
   aider edits it in place next).
4. `cp -R` (preserving `.meta`s) the 7 `Assets/Packages/*` folders listed above from
   xr-sandbox into `xr-racing-app/Assets/Packages/`.

**Step 4 — aider task 1 (namespace fixup on the ported Vehicle script):**
- File: `Assets/Gameplay/Scripts/Vehicle/CarTeleportAnchor.cs`
- `--message`: "Rename the namespace in this file to `XrRacing.Gameplay.Vehicle`
  (currently `App.Demos.CarDemo.Scripts`). Do not change any other logic, fields, or
  method bodies — this is a namespace-only rename."

**Step 5 — aider task 2 (new PC input script):**
- File to edit: `Assets/Gameplay/Scripts/Input/KartKeyboardInput.cs` (new file)
- Read-only context (`--read`): `Assets/Gameplay/Scripts/KartSystems/Inputs/BaseInput.cs`
- `--message`: "Create a new C# file at this path. Namespace `XrRacing.Gameplay.Input`.
  Class `KartKeyboardInput : KartGame.KartSystems.BaseInput` (BaseInput is defined in
  `Assets/Gameplay/Scripts/KartSystems/Inputs/BaseInput.cs`, namespace
  `KartGame.KartSystems`, abstract method `InputData GenerateInput()` returning a
  struct with bool Accelerate, bool Brake, float TurnInput). Use Unity's new Input
  System (`UnityEngine.InputSystem`), NOT the legacy `UnityEngine.Input` API — read
  `Keyboard.current`. Map: TurnInput = -1 when A or LeftArrow is held, +1 when D or
  RightArrow is held, 0 otherwise (a float, not stepped). Accelerate = true when W or
  UpArrow is held. Brake = true when S or DownArrow is held. Add null-check on
  `Keyboard.current` (return default InputData if null, e.g. in the Editor without
  focus)."

**Step 6 — aider task 3 (new VR input script):**
- File to edit: `Assets/Gameplay/Scripts/Input/XRWheelInput.cs` (new file)
- Read-only context (`--read`): `Assets/Gameplay/Scripts/KartSystems/Inputs/BaseInput.cs`
- `--message`: "Create a new C# file at this path. Namespace `XrRacing.Gameplay.Input`.
  Class `XRWheelInput : KartGame.KartSystems.BaseInput` (BaseInput is defined in
  `Assets/Gameplay/Scripts/KartSystems/Inputs/BaseInput.cs`, namespace
  `KartGame.KartSystems`, abstract method `InputData GenerateInput()` returning a
  struct with bool Accelerate, bool Brake, float TurnInput). The wheel itself is a
  plain Transform being physically rotated by Meta XR Interaction SDK's
  TwoGrabRotateTransformer (no custom grab script) — this class only reads the
  resulting rotation. Add fields:
  `[SerializeField] private Transform wheelTransform;`,
  `[SerializeField] private Vector3 wheelSpinAxis = Vector3.forward;`,
  `[SerializeField] private float maxWheelAngle = 450f;`,
  `[SerializeField] private float triggerThreshold = 0.5f;`.
  In Awake(), if wheelTransform is not null, cache its starting local rotation as
  `_originRotation`. In GenerateInput(): if wheelTransform is null, TurnInput = 0;
  otherwise compute the signed angle between `_originRotation` and
  `wheelTransform.localRotation` around `wheelSpinAxis` using
  `Quaternion.Angle`/`Vector3.SignedAngle` on their projected axes (mirror the intent
  of computing a signed twist angle about one axis, not a full 3D angle), then
  TurnInput = Mathf.Clamp(angle / maxWheelAngle, -1f, 1f). Accelerate =
  `OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.Touch)` (Meta XR SDK OVRInput
  API — checks the A/X face button on either hand's Touch controller). Brake = true
  when the larger of
  `OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch)` and
  `OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch)`
  exceeds triggerThreshold."

**Step 7 — Claude, via Unity MCP:**
- Build both scenes and wire everything per the "Scene setup" section above, using MCP
  tool calls end-to-end instead of a headless script or manual dragging.
- Run the headless Editor compile check, confirm no "Missing Script"/broken-reference
  warnings in either scene via MCP before calling this step done.

Report tokens sent/received per aider call as usual, plus one combined total at the end
of this task's aider invocations.
