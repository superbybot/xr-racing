# Follow-up: VR seat-entry / exit flow via CarTeleportAnchor

## Status

Skipped for the initial driving-prototype pass (`docs/xr-racing-app-prototype-implementation-plan.md`).
For that pass, the VR player is spawned already seated in the kart (the
`OVRCameraRig` is just parented directly under `KartClassic_Player` at a
fixed local offset) — no teleport-to-enter/exit interaction exists yet.

`CarTeleportAnchor.cs` (`Assets/Gameplay/Scripts/Vehicle/CarTeleportAnchor.cs`)
was ported and compiles, but is **not instantiated/wired into either scene**.
It's dead code sitting ready for this follow-up task.

## What's needed to actually use CarTeleportAnchor

`CarTeleportAnchor : TeleportationAnchor` (XRI 3.x) needs the following to
function, none of which exist in `DrivingPrototype_VR.unity` yet:

1. **XRI Locomotion System** in the scene:
   - `LocomotionMediator`
   - `ContinuousMoveProvider`
   - `TeleportationProvider`
   - A `CharacterController` on the XR rig (Meta's `OVRCameraRig` doesn't ship
     one by default — Unity's `XROrigin` pattern normally provides this).
2. **A teleport-ray interactor** on at least one controller, so the player can
   actually select the `CarTeleportAnchor` to trigger `OnTeleporting` (that's
   what calls `EnterCarAsync`).
3. **`CarTeleportAnchor`'s own serialized fields**, all currently unset:
   - `exitPoint` — a `Transform` marking where the player is placed/oriented
     on exit (falls back to `Vector3.forward` orientation if the exit point's
     forward is purely vertical).
   - `xrOrigin` — the player rig's root transform; the driver's transform gets
     reparented under this on enter.
   - `locomotionMediator`, `characterController`, `continuousMoveProvider` —
     disabled on enter (`DisableLocomotion`) and re-enabled after a 0.2s delay
     on exit (`EnableLocomotion`), so the player doesn't walk through the kart
     body or double-move while seated.
4. Add the `CarTeleportAnchor` component itself somewhere on/near the kart
   (needs its own collider set up as a `TeleportationAnchor` target, since
   that's the base class XRI expects players to point-and-select).

## Where to look before hand-building this

Per the original plan: check the imported Meta sample content under
`Assets/Samples/` (Meta XR Interaction SDK / Interaction SDK Essentials) for
an existing scene that already has a working `LocomotionMediator` +
`ContinuousMoveProvider` + `TeleportationProvider` + ray-interactor rig set up
— copying a known-working configuration is much less error-prone than
assembling XRI's locomotion stack field-by-field from scratch.

## Why this was deferred

This is genuinely a separate sub-system (XR locomotion) from the core ask
(steering-wheel driving), with several interdependent pieces prone to subtle
misconfiguration. The user's call: skip it for this pass, keep the prototype
scope to "driving mechanics only," and come back to seat-entry/exit as its
own follow-up task once the driving feel itself is validated.
