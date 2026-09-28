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
| :--- | :--- | :--- | :---: | :---: | :--- | :--- |
| **`SteeringWheelTransformer.cs`** | `XrRacing.Gameplay.Input` | 114 | **A-** | ✅ Zero | One- or two-hand grab transformer. Hot-path string formatting is guarded behind `WheelDebugLog.Enabled`, eliminating per-frame GC while driving. | Maintain zero-allocation pattern. |
| **`XRWheelInput.cs`** | `XrRacing.Gameplay.Input` | 504 | **A-** | ✅ Low | Grabbable wheel input and pedal mapping. `debugLog` defaults to `false`. Hot-path `LogInteractorStatus` uses cached array lookups instead of scene traversal. | Maintain low-allocation pattern. |
| **`WheelDebugLog.cs`** | `XrRacing.Gameplay.Input` | 75 | **A** | ✅ Zero | Gated behind `#if XR_WHEEL_DEBUG` compiler symbol with zero-cost no-op stubs when undefined. Zero disk I/O in release builds. | Activate only when profiling with custom scripting define. |
| **`VRCameraHeightSmoother.cs`** | `XrRacing.Gameplay.Vehicle` | 61 | **A** | ✅ Zero | Highly focused, clean `Mathf.SmoothDamp` implementation, handles null targets and disable states gracefully. | None. Exemplary pattern. |
| **`DriverSeatAdjuster.cs`** | `XrRacing.Gameplay.Vehicle` | 109 | **A-** | ✅ Zero | Robust coordinate space math (`InverseTransformPoint`/`Direction`), correct event pairing with `DriverSettings`. | Excellent. Consider caching parent transform. |
| **`DriverSettings.cs`** | `XrRacing.Gameplay.Settings` | 103 | **A** | ✅ Low | Clean POCO data model, static change event, and PlayerPrefs persistence with clamp safety. | None. Clean architecture. |
| **`DriverSettingsMenu.cs`** | `XrRacing.Gameplay.UI` | 184 | **B+** | ✅ Low | Clean UI event routing for VR slider/toggle interactions. Minor static state coupling (`IsOpen`). | Add null checks for UI references in `Awake`. |
| **`KartKeyboardInput.cs`** | `XrRacing.Gameplay.Input` | 38 | **A** | ✅ Zero | Concise, zero GC, clean struct return for PC testing fallback. | None. |
| **`CarTeleportAnchor.cs`** | `XrRacing.Gameplay.Vehicle` | 171 | **B+** | ✅ Low | Solid UniTask/R3 implementation for car enter/exit flow; properly disposes subjects in `OnDestroy`. | Complete wiring when locomotion system is added. |
| **`SceneGroupLoaderDeviceFix.cs`**| `XrRacing.Gameplay.SdkPatches` | 218 | **B+** | ⚠️ Low | Necessary reflection workaround for Meta SDK serialization bug on Quest. Includes SDK version warning guard. | Isolate to sample build configurations. |
| **`QuestBuildDeploy.cs`** | *(global / Editor)* | 275 | **A-** | N/A | Reliable batch build script with `try ... finally` application ID restoration and exit code handling. | Keep global signature for CI compatibility. |
| **`DriverSettingsMenuBuilder.cs`** | *(global / Editor)* | 426 | **B** | N/A | Procedural UI builder using Meta UISet prefabs. Uses hardcoded package paths. | Wrap in Editor namespace or extract constants. |
| **`CreateSampleBuildProfiles.cs`**| *(global / Editor)* | 58 | **B+** | N/A | Concise editor utility for generating build profile assets. | Wrap in Editor namespace. |
| **`ArcadeKart.cs`** *(Ported)* | `KartGame.KartSystems` | 603 | **B** | ⚠️ Med | Ported physics controller from Unity Karting Microgame. Complex raycast suspension and wheel friction model. | Maintain as reference port without unneeded edits. |
| **`KartAgent.cs`** *(Ported)* | `KartGame.AI` | 300 | **B** | ⚠️ Med | Ported ML-Agents Agent driving `ArcadeKart` via `IInput`. Raycast sensors in `CollectObservations`. Clean training vs inferencing mode separation. | Verify sensor raycast allocation overhead in VR; maintain in `KartGame.AI`. |
| **`DebugCheckpointRay.cs`** | `KartGame.AI` | 35 | **A** | ✅ Zero | Editor Gizmo visualization tool for checkpoint orientations. | None. Clean utility. |
| **`DebugCheckpointRayEditor.cs`** | `KartGame.AI` | 44 | **A** | N/A | Custom Inspector for `DebugCheckpointRay`. Located in `Assets/Editor/`. | None. Clean editor tool. |

---

## 3. High-Priority Quality Debt & Action Plan

### Priority 1: Stop Hot-Path Allocations in `SteeringWheelTransformer.cs` (RESOLVED)
- **Status:** Complete. Debug string formatting and logging calls enclosed within `if (WheelDebugLog.Enabled)`.

### Priority 2: Decouple Diagnostics in `XRWheelInput.cs` & Harden `WheelDebugLog.cs` (RESOLVED)
- **Status:** Complete. `debugLog` defaults to `false`. `WheelDebugLog` is gated behind `#if XR_WHEEL_DEBUG` compiler directives with no-op stubs. `LogInteractorStatus()` uses cached `_handInteractors`.

### Priority 3: Add Assembly Definitions (`.asmdef`) (RESOLVED)
- **Status:** Complete. Created `XrRacing.Gameplay.asmdef` and `XrRacing.Editor.asmdef` with explicit SDK references. Project compiles with zero errors.
