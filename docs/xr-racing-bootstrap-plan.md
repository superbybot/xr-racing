# Bootstrap xr-racing-app from xr-sandbox + Meta/Photon SDKs

## Context

`xr-racing-app` (Unity 6000.5.2f1, URP 17.5.0) is currently the untouched default
3D template — no XR packages, no racing content. Rather than starting from zero,
we're bootstrapping it from `/Users/byronbautista/xr-sandbox/xr-sandbox-app`, a
sister sandbox project that already has:

- A hand-built **Car Demo** (`Assets/App/Demos/Car Demo`): a grabbable XR
  steering wheel + joystick rig, kart models/materials, and (separately) the
  C# driving scripts — matches the target control scheme (grabbable wheel, VR,
  Meta Quest).
- **Karting_Reference** (`Assets/App/References/Karting_Reference`): Unity's
  official Karting Microgame asset in full — kart variants, modular track kit,
  AI, game modes, VFX, UI — imported as reference/source content, own
  `ThirdPartyNotice.txt` included.
- Meta XR SDK (Core, Interaction, Interaction.OVR, Movement) and XR Interaction
  Toolkit already resolved and working, plus their sample content physically
  imported under `Assets/Samples/...`.

Target shape for xr-racing-app (confirmed earlier in this session): VR-only,
Meta Quest standalone, grabbable-wheel driving, AI opponents + multiplayer PvP
via **Photon Fusion 2**.

This plan covers **bootstrap only** — assets, packages, and a README. No
gameplay/netcode code is written in this pass; that's future work once the
ported scripts and Fusion are in place.

## Part 1 — Copy racing assets from xr-sandbox

Two source trees, copied **with their `.meta` sidecars** so Unity GUIDs (and
therefore all cross-references between prefabs/materials/models) stay intact —
a plain re-import without matching metas would silently break every prefab
reference.

**A. Car Demo** (`.../Assets/App/Demos/Car Demo`, 2.3MB) → copy
`Materials/`, `Models/`, `Prefabs/` only (no Scripts, no Scenes — scripts are
explicitly deferred to a later pass), **excluding** the 3 files:
`Prefabs/HotRod_Player Variant.prefab`, `Prefabs/KartClassic_Player
Variant.prefab`, `Prefabs/TinyKart_Player Variant.prefab` (and their `.meta`s)
— these are Prefab Variants of Karting_Reference's own kart prefabs, superseded
by bringing Karting_Reference in directly.

Known compile-time gap to expect and not silently paper over: the two
remaining prefabs `XR Joystick.prefab` and `XR Steering Wheel.prefab`
reference `XRJoystick.cs` / `XRSteeringWheel.cs` (Car Demo scripts, excluded
this pass) — they **will show "Missing Script" in the Editor** until those
scripts are ported in a future pass. `XR Objects.prefab` has no such
dependency and will be clean. All three also use
`com.unity.xr.interaction.toolkit` Affordance System components, so that
package must be present (see Part 2) or more components will show as missing.

**B. Karting_Reference** (297MB) → copy the **entire folder** verbatim,
per your decision (Prefabs, Art, Audio, Animations, ModularTrackKit,
PhysicsMaterials, ScriptableObjects, Scripts, Scenes, Timelines, Tutorials,
AddOns, PostProcessing, plus `KartingMicrogame_README.txt` /
`ThirdPartyNotice.txt`).

Destination: mirror source structure under `xr-racing-app/Assets/App/...`
(`Assets/App/Demos/Car Demo/{Materials,Models,Prefabs}` and
`Assets/App/References/Karting_Reference/`) so the two projects stay
structurally comparable for future syncs.

Karting_Reference's scripts use Cinemachine, ML-Agents, ProBuilder, and
`UnityEngine.VFX` — see Part 2 for the package additions this requires.
Flag: `com.unity.visualeffectgraph` isn't in xr-sandbox's own manifest despite
the `UnityEngine.VFX` usage, so whether those specific scripts compile clean
is unverified — check on first Editor open rather than assuming.

## Part 2 — Package manifest updates

Add to `xr-racing-app/Packages/manifest.json` `dependencies`, matching
xr-sandbox's known-good **resolved** versions (from `packages-lock.json`, all
served off Unity's default registry — no scoped registry entry needed):

- `com.meta.xr.sdk.core`: `205.0.0`
- `com.meta.xr.sdk.interaction`: `205.0.0`
- `com.meta.xr.sdk.interaction.ovr`: `205.0.0`
- `com.meta.xr.sdk.movement`: `https://github.com/oculus-samples/Unity-Movement.git` (git dependency)
- `com.unity.xr.interaction.toolkit`: `3.4.1` (needed for the two kept prefabs' Affordance components)
- `com.unity.xr.openxr`: `1.16.1` (Quest runtime loader)
- `com.unity.cinemachine`: `3.1.5` (Karting_Reference dependency)
- `com.unity.ml-agents`: `4.0.0` (Karting_Reference AI dependency)
- `com.unity.probuilder`: `6.0.9` (Karting_Reference dependency)

These version numbers are "known-good as installed in xr-sandbox today," not
verified against the registry's actual current-latest — Package Manager will
offer an Update if something newer exists once opened.

**Samples** (per your decision to bring "all the samples," including the
~450MB Movement SDK set): copy the already-imported sample folders straight
from `xr-sandbox-app/Assets/Samples/` into `xr-racing-app/Assets/Samples/`
(same GUID-preservation approach as Part 1), rather than relying on
Package Manager's per-sample "Import" button:
- `Meta XR Core SDK` (5.2MB)
- `Meta XR Interaction SDK Essentials` (1.7MB)
- `Meta XR Interaction ​SDK` (26MB)
- `Meta XR Movement SDK` (450MB)

This must happen **after** the manifest edit and after Unity has resolved the
new packages once (opening the Editor), since the sample content's scripts
reference package-provided types.

## Part 3 — Photon Fusion 2 (multiplayer)

Photon isn't on Unity's public registry — getting the SDK in requires a
Photon account + App ID and a manual download, which isn't something doable
from this shell without your credentials. This pass just gets the project
ready for it; you'll do the actual Photon account/download step yourself:

1. You create a free Photon account and a Fusion 2 App ID at
   `dashboard.photonengine.com`.
2. You download the **Photon Fusion 2** SDK (Unity package) from the Photon
   dashboard/Fusion docs.
3. Import it into `xr-racing-app` via `Assets > Import Package` (or drop the
   UPM tarball into `Packages/manifest.json` if using the UPM-distributed
   flavor — Fusion 2 supports both; recommend the Asset Store/unitypackage
   route since that's what Photon documents primarily).
4. Paste your App ID into the Fusion Hub window inside the Editor.

Architecture note for later: Fusion 2's client-authoritative + prediction
model is the reason it was picked over PUN2 for this project — it's the
right fit once we're syncing kart physics across players (rewind/resim,
input-based prediction) rather than just RPCs.

## Part 4 — README

Rewrite `xr-racing/README.md` (currently just `# xr-racing`) to note the tech
stack and assets in use so far — nothing more elaborate than that for now:

- **Engine**: Unity 6000.5.2f1, Universal Render Pipeline 17.5.0
- **Target**: VR, Meta Quest (standalone), grabbable-wheel driving
- **XR**: Meta XR SDK (Core/Interaction/Interaction.OVR) 205.0.0, Meta XR
  Movement SDK, Unity XR Interaction Toolkit 3.4.1, OpenXR 1.16.1
- **Multiplayer**: Photon Fusion 2 (manual account/App-ID setup, see repo
  notes once Part 3 is done)
- **Assets in use**:
  - XR driving rig (grabbable steering wheel, joystick, kart models) —
    ported from the `xr-sandbox` Car Demo
  - Kart/track/AI reference content — Unity's official **Karting
    Microgame** asset (`Assets/App/References/Karting_Reference`,
    third-party notice included in that folder)

## Verification

- Open `xr-racing-app` in Unity 6000.5.2f1 and let Package Manager resolve
  the new manifest entries (needs network access; may prompt for a Unity ID
  if any package requires entitlement checks).
- Check the Console for compile errors — expect the two "Missing Script"
  prefab warnings called out in Part 1 as known/expected, watch for anything
  *beyond* that (e.g. the `UnityEngine.VFX` question flagged above).
- Confirm `XR Objects.prefab`, `XR Joystick.prefab`, `XR Steering Wheel.prefab`
  and the Karting_Reference kart/track prefabs open without "Missing Prefab"
  (broken GUID) errors in the Inspector.
- Run Meta's **XR Project Setup Tool** (Meta > Tools > Project Setup Tool)
  after packages resolve and apply its recommended fixes — this is what
  actually configures OpenXR/Quest provider settings; not something to hand-edit.

## Execution sequence for aider

No aider tasks in this pass — everything here is binary asset copying
(FBX/prefab/material files, which aider's text-diffing isn't suited for and
shouldn't touch), a JSON manifest edit, and short factual README prose, all
better done directly. Sequence:

1. **Claude, directly (file copies, preserving `.meta` sidecars):**
   - `cp -R` `xr-sandbox-app/Assets/App/Demos/Car Demo/{Materials,Models,Prefabs}` →
     `xr-racing-app/Assets/App/Demos/Car Demo/`, then remove the 3 excluded
     `*_Player Variant.prefab(+.meta)` files from the destination.
   - `cp -R` `xr-sandbox-app/Assets/App/References/Karting_Reference` →
     `xr-racing-app/Assets/App/References/Karting_Reference` (whole tree).
   - `cp -R` the 4 named sample folders from
     `xr-sandbox-app/Assets/Samples/` → `xr-racing-app/Assets/Samples/`.
   - Create/verify parent `.meta` files exist for any newly-created
     directories Unity hasn't seen before (Unity normally regenerates these
     on next Editor open — don't hand-author them).
2. **Claude, directly (config/doc edits, not code):**
   - Edit `xr-racing-app/Packages/manifest.json` to add the 9 package entries
     listed in Part 2.
   - Rewrite `xr-racing/README.md` per Part 4.
3. **Neither Claude nor aider — manual/GUI steps for you:**
   - Open the project in Unity Editor to let Package Manager resolve the new
     packages (needs network + possibly Unity ID entitlement checks).
   - Run Meta's XR Project Setup Tool and apply recommended fixes.
   - Photon Fusion 2 account creation, App ID, SDK download and import
     (Part 3, steps 1–4) — entirely manual, outside this repo's automation.
