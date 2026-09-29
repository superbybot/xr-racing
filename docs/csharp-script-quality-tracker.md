# C# Script Quality Tracker

**Project:** `xr-racing`  
**Platform:** Meta Quest Standalone VR (72/90/120 Hz)  
**Standard Version:** 1.0 (September 2026)  

---

## 1. VR C# Quality Standard & Rubric

In Standalone VR on Meta Quest, frame drops directly cause simulation sickness and tracking judder. Therefore, our C# quality standard enforces strict performance, memory, and architectural rules:

### Tier 1: VR Performance & Garbage Collection (Zero-Tolerance)
* **Zero GC in Hot Paths:** No heap allocations inside `Update`, `FixedUpdate`, `LateUpdate`, or Transformer callbacks (`UpdateTransform`).
* **No Per-Frame String Operations:** Never use string concatenation (`+`), string interpolation (`$"{...}"`), or `string.Join` in per-frame execution.
* **Component & Hierarchy Caching:** Cache all `GetComponent`, `FindObjectsByType`, and `transform.parent` lookups in `Awake`/`Start`. Never call `FindObjectsByType` or `GameObject.Find` in update loops.
* **Struct Value Types for High-Frequency Data:** Pass input and transformation data as `struct` or `ref struct` (e.g. `InputData`).

### Tier 2: Robustness & Lifecycle Management
* **Null Safety:** Guard public and serialized references against null. Use `[RequireComponent]` for mandatory companion components.
* **Event Symmetry:** Every event subscription in `OnEnable` must have an exact unsubscription in `OnDisable`.
* **Reactive & Async Disposal:** All R3 `Subject<T>` and UniTask cancellation tokens must be disposed/canceled in `OnDestroy` or `OnDisable`.
* **Static State Containment:** Avoid static mutable state across scene loads; provide explicit `Reset` or lifecycle-bound accessors.

### Tier 3: Architecture & Clean Code
* **Single Responsibility Principle (SRP):** Classes should not exceed 300–400 lines unless handling an indivisible hardware abstraction. Separate diagnostics/logging from core gameplay mechanics.
* **Namespace Uniformity:** All custom scripts must reside in `XrRacing.Gameplay.<Domain>` or `XrRacing.Editor.<Domain>`.
* **Ported vs Native Code Boundary:** Keep third-party or ported reference code (`KartGame.KartSystems`) isolated from custom XR mechanics.

### Tier 4: Unity & C# Idioms
* **Field Naming Convention:**
  * Private/internal fields: `_camelCase`
  * Serialized private fields: `camelCase` (with `[SerializeField]`)
  * Properties, methods, classes, structs, enums: `PascalCase`
* **Inspector Quality:** All `[SerializeField]` fields should include `[Tooltip("...")]` and reasonable `[Range(...)]` or `[Min(...)]` constraints where applicable.
* **Strip Debugging from Release:** Diagnostic logging tools must be compiled out or disabled in release builds (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`).

---

## 2. Script Quality Scorecard & Audit

| Script | Namespace | LOC | Score | GC Risk | Key Observations & Technical Debt | Action Required |
| :--- | :--- | :---: | :---: | :---: | :--- | :--- |
| **`SteeringWheelTransformer.cs`** | `XrRacing.Gameplay.Input` | 102 | **A-** | ✅ Zero | One- or two-hand grab transformer. Hot-path string formatting is guarded behind `WheelDebugLog.Enabled`, eliminating per-frame GC while driving. | Maintain zero-allocation pattern. |
| **`XRWheelInput.cs`** | `XrRacing.Gameplay.Input` | 442 | **A** | ✅ Zero in Prod | Grabbable wheel input and pedal mapping. `debugLog` defaults to `false`. All diagnostics and reflection compiled out in release builds (`#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`). | Maintain decoupled pattern. |
| **`WheelDebugLog.cs`** | `XrRacing.Gameplay.Input` | 68 | **A** | ✅ Zero | Gated behind `#if XR_WHEEL_DEBUG` compiler symbol with zero-cost no-op stubs when undefined. Zero disk I/O in release builds. | Activate only when profiling with custom scripting define. |
| **`KartKeyboardInput.cs`** | `XrRacing.Gameplay.Input` | 32 | **A** | ✅ Zero | Concise, zero GC, clean struct return for PC testing fallback. | None. |
| **`PhysicsRateMatcher.cs`** | `XrRacing.Gameplay.Vehicle` | 48 | **A+** | ✅ Zero | Dynamically locks physics step rate to Quest display refresh rate (72/90/120 Hz). Zero-allocation, clean event subscription. | None. Exemplary pattern. |
| **`VRCameraHeightSmoother.cs`** | `XrRacing.Gameplay.Vehicle` | 94 | **A** | ✅ Zero | Low-pass vertical filter & head-pivot tilt smoothing. Filters suspension jitter without swinging viewpoint. | None. Exemplary pattern. |
| **`DriverSeatAdjuster.cs`** | `XrRacing.Gameplay.Vehicle` | 236 | **A-** | ✅ Zero | Robust coordinate space math (`InverseTransformPoint`/`Direction`), correct event pairing with `DriverSettings` and `TrackLoader` async fades. | Maintain clean event pairing. |
| **`KartMotionDebugLog.cs`** | `XrRacing.Gameplay.Vehicle` | 239 | **A-** | ✅ Zero in Prod | Diagnostic motion telemetry. Gated behind `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`. `AutoFlush = false`, flushes once per second. | None. Safe for production. |
| **`TrackLoader.cs`** | `XrRacing.Gameplay.Tracks` | 205 | **A** | ✅ Low | Async additive scene switcher with fade transitions. Calls `Resources.UnloadUnusedAssets()` on track unload to prevent RAM leaks. | None. |
| **`TrackInfo.cs`** | `XrRacing.Gameplay.Tracks` | 36 | **A** | ✅ Zero | Concise POCO MonoBehaviour holding track display name, spawn point, and checkpoints. Gizmos in `#if UNITY_EDITOR`. | None. |
| **`DriverSettings.cs`** | `XrRacing.Gameplay.Settings` | 97 | **A** | ✅ Low | Clean POCO data model, static change event, and PlayerPrefs persistence with clamp safety. | None. Clean architecture. |
| **`DriverSettingsMenu.cs`** | `XrRacing.Gameplay.UI` | 385 | **A-** | ✅ Low | In-VR floating settings menu with seat height/distance sliders, pedal selectors, recenter, and additive map picker. | None. |
| **`OverlayLayer.cs`** | `XrRacing.Gameplay.UI` | 28 | **A** | ✅ Zero | MenuOverlay layer constants and recursive layer application utility. | None. |
| **`SceneGroupLoaderDeviceFix.cs`**| `XrRacing.Gameplay.SdkPatches` | 191 | **B+** | ⚠️ Low | Necessary reflection workaround for Meta SDK serialization bug on Quest. Includes SDK version warning guard. | Isolate to sample build configurations. |
| **`ArcadeKart.cs`** *(Ported)* | `KartGame.KartSystems` | 500 | **B** | ⚠️ Med | Ported physics controller from Unity Karting Microgame. Complex raycast suspension and wheel friction model. | Maintain as reference port without unneeded edits. |
| **`BaseInput.cs`** *(Ported)* | `KartGame.KartSystems` | 22 | **A** | ✅ Zero | Abstract input provider class returning `InputData` struct. Decouples physics from hardware. | None. |
| **`KartAnimation.cs`** *(Ported)* | `KartGame.KartSystems` | 73 | **A-** | ✅ Zero | Controls steering wheel and tire rotation visuals from kart input. | None. |
| **`KartPlayerAnimator.cs`** *(Ported)*| `KartGame.KartSystems` | 28 | **A** | ✅ Zero | Steers driver avatar IK and pedal visual animations. | None. |
| **`ArcadeEngineAudio.cs`** *(Ported)*| `KartGame.KartSystems` | 59 | **A-** | ✅ Zero | Modulates engine pitch and volume based on kart speed and drift state. | None. |
| **`MinMaxParameters.cs`** *(Ported)*| `KartGame.KartSystems` | 33 | **A** | ✅ Zero | Min/max parameter helper struct for physics ranges. | None. |
| **`KartAgent.cs`** *(Ported)* | `KartGame.AI` | 259 | **B** | ⚠️ Med | Ported ML-Agents Agent driving `ArcadeKart` via `IInput`. Raycast sensors in `CollectObservations`. Clean training vs inferencing mode separation. | Verify sensor raycast allocation overhead in VR; maintain in `KartGame.AI`. |
| **`DebugCheckpointRay.cs`** | `KartGame.AI` | 32 | **A** | ✅ Zero | Editor Gizmo visualization tool for checkpoint orientations. | None. Clean utility. |
| **`QuestBuildDeploy.cs`** | *(global / Editor)* | 240 | **A-** | N/A | Reliable batch build script with `try ... finally` application ID restoration and exit code handling. | Keep global signature for CI compatibility. |
| **`DriverSettingsMenuBuilder.cs`** | `XrRacing.Editor.UI` | 468 | **B+** | N/A | Procedural UI builder using Meta UISet prefabs. Includes explicit asset existence checks. | Keep isolated in Editor assembly. |
| **`TrackScenesBuilder.cs`** | `XrRacing.Editor.Tracks` | 367 | **B+** | N/A | Procedural track scene builder; creates track scenes, adds `TrackInfo`, and syncs App build profile. | Keep isolated in Editor assembly. |
| **`MenuOverlayRenderingSetup.cs`** | `XrRacing.Editor.UI` | 119 | **A-** | N/A | Injects URP RenderObjects draw-on-top feature for `MenuOverlay` layer with depth testing off. | Safe to re-run. |
| **`DebugCheckpointRayEditor.cs`** | `KartGame.AI` | 46 | **A** | N/A | Custom Inspector for `DebugCheckpointRay`. Located in `Assets/Editor/`. | None. Clean editor tool. |
| **`CreateSampleBuildProfiles.cs`**| *(global / Editor)* | 46 | **B+** | N/A | Concise editor utility for generating build profile assets. | Keep isolated in Editor assembly. |

---

## 3. High-Priority Quality Debt & Action Plan

### Priority 1: Stop Hot-Path Allocations in `SteeringWheelTransformer.cs` (RESOLVED)
- **Status:** Complete. Debug string formatting and logging calls enclosed within `if (WheelDebugLog.Enabled)`.

### Priority 2: Decouple Diagnostics in `XRWheelInput.cs` & Harden `WheelDebugLog.cs` (RESOLVED)
- **Status:** Complete. `debugLog` defaults to `false`. `WheelDebugLog` is gated behind `#if XR_WHEEL_DEBUG` compiler directives with no-op stubs. All diagnostics and reflection in `XRWheelInput` are compiled out of release builds (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`).

### Priority 3: Add Assembly Definitions (`.asmdef`) (RESOLVED)
- **Status:** Complete. Created `XrRacing.Gameplay.asmdef` and `XrRacing.Editor.asmdef` with explicit SDK references. Project compiles with zero errors.

### Priority 4: Prevent Standalone Track Unload Memory Leaks (RESOLVED)
- **Status:** Complete. `TrackLoader.UnloadOtherTracksAsync` invokes `Resources.UnloadUnusedAssets()` during track transitions while faded to black, keeping RAM usage flat across multi-track sessions.

### Priority 5: Eliminate Unused Dead Code (RESOLVED)
- **Status:** Complete. Removed `CarTeleportAnchor.cs` and archived its design doc, clearing all 3 CS0114 compiler warnings.
