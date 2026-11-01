# Code audit — 2026-11-01

Audit of our C# against the rules in `docs/review-rules/` (comments, no runtime
`AddComponent` quick fixes, Microsoft C# conventions, MVVM / Clean Architecture,
performance). Scope: `Assets/Gameplay/Scripts/` (~3,900 lines) and `Assets/Editor/`
(~2,500 lines). Not audited: `References/KartGame/` (adopted Karting Microgame code,
kept as-is) and the seated-driver WIP (`HeadAndHandsAvatarDriver`, `FirstPersonHeadHider`).

Overall: the gameplay code is in good shape — clear names, why-comments, cached
lookups, correct event pairing, UniTask with cancellation. The main debts are
**cross-layer static state**, **objects built at runtime instead of in the prefab**,
and **diagnostics mixed into gameplay classes**.

## Fixed in the organize-codebase PR

| Area | Issue | Fix |
|---|---|---|
| Structure | Adopted Karting code (`KartGame.*`) mixed into `Gameplay/Scripts/` | Moved to `References/KartGame/` with its own assembly; namespace `XrRacing.References.KartGame.*` |
| Structure | Debug loggers spread over `Input/`, `Vehicle/`, `UI/` | All in `Gameplay/Scripts/Diagnostics/`, namespace `XrRacing.Gameplay.Diagnostics` |
| Diagnostics | Three copies of the same CSV file-writing code; wheel log behind its own `XR_WHEEL_DEBUG` define (so its flag did nothing) | Shared `DiagnosticCsvWriter`; all three behind `UNITY_EDITOR \|\| DEVELOPMENT_BUILD` |
| Performance | `KartMotionDebugLog` shipped **enabled** on `PlayerKart_VR` with raw samples (a CSV row and string building every physics step in the Editor and dev builds); `XRWheelInput.debugLog` on | Both off in the prefab |
| Quick fix | `DriverSettingsMenu` did `AddComponent<MenuRayDebugLog>()`, on by default | Optional `[SerializeField] MenuRayDebugLog` reference |
| Quick fix | `RaceCountdownUI` fell back to `GetComponent`/`AddComponent<CanvasGroup>()` | Removed; `RaceUIBuilder` now adds the CanvasGroup at build time and `DrivingPrototype_VR` has it wired |
| Quick fix | `PhysicsRateMatcher` spawned a GameObject + component at startup; its serialized range could never be edited | Static class hooked up at startup, no component |
| Performance | `SceneGroupLoaderDeviceFix` polled every 0.5 s for the whole session in every scene (HashSet + `FindObjectsByType` each time) | Build scene names cached once; retries only for 5 s after each scene load |
| Performance | `FingerSpreadTuner` allocated four arrays every `LateUpdate` | Reused buffers; summary corrected (it's the permanent finger fix, not a temporary tuner) |
| Lookups | `RaceManager`, `TrackLoader` called `kart.GetComponent<Rigidbody>()` | Use the kart's cached `Rigidbody` |
| Conventions | Fully qualified names throughout `XRWheelInput`, `KartKeyboardInput`; redundant `using` of own namespace in two UI scripts | `using` directives |
| Conventions | `QuestBuildDeploy`, `CreateSampleBuildProfiles` had no namespace; flat `Editor/` folder | `Editor/Build/` (`XrRacing.Editor.Build`), `Editor/Builders/`; CI `-executeMethod` updated |
| Comments | Self-explanatory comments (`// Decelerate kart smoothly`, `// 3, 2, 1`, `// START!`, `// Smooth fade out`, `// Find Playground track`, color comments, …) | Removed |

## Follow-ups (separate PRs)

Ordered by value. Items marked **Editor** need prefab/scene changes made in the
Unity Editor or through a builder script, not by hand-editing YAML.

1. **Static cross-layer flags** (Clean Architecture). `DriverSettingsMenu.IsOpen` and
   `RaceManager.IsControlsLocked` are global statics read by `XRWheelInput`,
   `KartKeyboardInput`, `DriverSeatAdjuster` and `MenuRayDebugLog`, so input depends
   on UI and race code. Replace with one injected "controls gate" (an interface or a
   small state asset wired with `[SerializeField]`) that UI and race logic write and
   input reads.
2. **`XRWheelInput` mixes input with ~250 lines of grab diagnostics** (SRP). Move the
   logging (`LogInteractorStatus`, `LogHandGrabAttempts`, `LogHands`, `Describe*`,
   the reflection on `_gripCollider`) into `Diagnostics/WheelGrabDiagnostics`, reading
   the wheel through read-only properties.
3. **`DriverSettingsMenu` is view + controller in 537 lines** (MVVM). Split a plain C#
   `DriverSettingsViewModel` (working copy of `DriverSettings`, Recenter / Reset /
   Respawn commands, change events) from a thin view that binds sliders and toggles.
   Its runtime scene surgery (finding ray/hand visuals with `FindObjectsByType`,
   destroying `RectTransformBoundsClipperDriver` to thicken the hit box) belongs in
   `DriverSettingsMenuBuilder` at edit time, with the results serialized. **Editor**
4. **Objects built at runtime.** `DriverSeatAdjuster.CreateFade()` builds a Canvas +
   Image fade quad in code; `VRCameraHeightSmoother.Start()` creates
   `SteeringWheelMount` and reparents the wheel. Author both in `PlayerKart_VR` and
   serialize the references. **Editor**
5. **`GetComponent` / `Camera.main` fallbacks** for the head, follow target and wheel
   grabbable (`DriverSeatAdjuster`, `VRCameraHeightSmoother`, `XRWheelInput`,
   `FloatingLapUI`, `RaceEndingMenu`, `DriverSettingsMenu`, `KartMotionDebugLog`).
   Wire every reference in the prefab/scene, then delete the fallbacks and report
   missing ones in `OnValidate`. **Editor**
6. **Playground found by name.** `RaceManager.IsPlayground` and
   `RaceEndingMenu.OnBackToPlayground` both search display names for "Playground".
   Add an `isPlayground` flag on the track entry, set by `TrackScenesBuilder`.
7. **Duplicated code.** "Place the panel in front of the head" exists in
   `DriverSettingsMenu` and `RaceEndingMenu` (and `GetHead` in three UI scripts);
   `TrackLoader.LoadTrackAsync` and `RespawnAsync` repeat the freeze/restore-kart
   block. Extract a shared placement helper and a freeze scope.
8. **Field naming.** Serialized private fields are `camelCase` in most scripts but
   `_camelCase` in `Characters/`; the convention is `_camelCase`. Rename with
   `[FormerlySerializedAs]` and update the builders' `FindProperty("...")` strings
   in the same PR. Same for the lowercase public fields on `TrackLoader.TrackEntry`
   and `DriverSettingsMenu.PedalSelector` (make them PascalCase properties).
9. **`SceneGroupLoaderDeviceFix` version guard** expects Meta SDK 69 / OVRPlugin 1.107
   but the project is on 207, so it warns every launch. Check whether the Meta bug
   still exists on 207 and delete the patch if not.
10. **Small ones.** `TrackLoader.PlaceKart` uses `Physics.RaycastAll` (allocates;
    called only on load, low priority). `LapTracker`'s wrap-around rule
    (`index < 3`) is a magic number. `RaceManager`'s 1.5 s post-race brake is
    hard-coded; make it a serialized field. `DrivingPrototype_VR.unity` still holds a
    stale `debugRayLog: 1` value that Unity drops on the next save.

## Per-script notes

| Script | Lines | Notes |
|---|---:|---|
| `Characters/CharacterLineup`, `CharacterLineupOption`, `JoystickRigMover` | 35–83 | Clean. |
| `Characters/FingerSpreadTuner` | 106 | Fixed (allocations, summary). |
| `Input/XRWheelInput` | 524 | Follow-ups 1, 2, 5. Input logic itself is clean and allocation-free when not logging. |
| `Input/SteeringWheelTransformer` | 120 | Clean; logging is guarded. |
| `Input/KartKeyboardInput` | 37 | Follow-up 1. |
| `Settings/DriverSettings` | 115 | Clean model. Public fields are fine for a `[Serializable]` data class. |
| `Race/RaceManager` | 315 | Follow-ups 1, 6, 10. |
| `Race/LapTracker` | 125 | Clean; follow-up 10. |
| `Tracks/TrackLoader` | 327 | Follow-ups 7, 8, 10. |
| `Tracks/TrackInfo` | 43 | Clean. |
| `Vehicle/DriverSeatAdjuster` | 351 | Follow-ups 1, 4, 5; two fade implementations (coroutine and UniTask) could be one. |
| `Vehicle/VRCameraHeightSmoother` | 221 | Follow-ups 4, 5. Math is well documented. |
| `Vehicle/PhysicsRateMatcher` | ~45 | Fixed. |
| `UI/DriverSettingsMenu` | 537 | Follow-ups 1, 3, 5, 7. |
| `UI/FloatingLapUI`, `RaceCountdownUI`, `RaceEndingMenu`, `OverlayLayer` | 32–147 | Fixed comments/quick fix; follow-ups 5, 6, 7. |
| `SdkPatches/SceneGroupLoaderDeviceFix` | ~210 | Fixed polling; follow-up 9. |
| `Diagnostics/*` | — | Fixed (shared writer, one gate, off by default). Per-frame string building is acceptable here: compiled out of release and opt-in. |
| `Editor/Builders/*` | 135–611 | Builders may `AddComponent` by design. `CharacterTestSceneBuilder` (611 lines) could be split by concern if it keeps growing. |
| `Editor/Build/QuestBuildDeploy`, `CreateSampleBuildProfiles` | 69–293 | Fixed namespace. |
