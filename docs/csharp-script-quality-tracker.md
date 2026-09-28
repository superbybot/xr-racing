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
| **`XRWheelInput.cs`** | `XrRacing.Gameplay.Input` | 504 | **B** | ⚠️ Med | Large God class mixing wheel angle clamping, hand-tracking curl calculations, controller button inputs, private reflection, and heavy CSV logging. | Decouple diagnostics; default `debugLog = false`. |
| **`WheelDebugLog.cs`** | `XrRacing.Gameplay.Input` | 70 | **C+** | ⚠️ High | Synchronous file I/O with `AutoFlush = true` called directly from the VR update loop. File grew to 3.3MB. | Strip in production builds (`#if UNITY_EDITOR`). |
| **`VRCameraHeightSmoother.cs`** | `XrRacing.Gameplay.Vehicle` | 61 | **A** | ✅ Zero | Highly focused, clean `Mathf.SmoothDamp` implementation, handles null targets and disable states gracefully. | None. Exemplary pattern. |
| **`DriverSeatAdjuster.cs`** | `XrRacing.Gameplay.Vehicle` | 109 | **A-** | ✅ Zero | Robust coordinate space math (`InverseTransformPoint`/`Direction`), correct event pairing with `DriverSettings`. | Excellent. Consider caching parent transform. |
| **`DriverSettings.cs`** | `XrRacing.Gameplay.Settings` | 103 | **A** | ✅ Low | Clean POCO data model, static change event, and PlayerPrefs persistence with clamp safety. | None. Clean architecture. |
| **`DriverSettingsMenu.cs`** | `XrRacing.Gameplay.UI` | 184 | **B+** | ✅ Low | Clean UI event routing for VR slider/toggle interactions. Minor static state coupling (`IsOpen`). | Add null checks for UI references in `Awake`. |
| **`KartKeyboardInput.cs`** | `XrRacing.Gameplay.Input` | 38 | **A** | ✅ Zero | Concise, zero GC, clean struct return for PC testing fallback. | None. |
| **`CarTeleportAnchor.cs`** | `XrRacing.Gameplay.Vehicle` | 171 | **B+** | ✅ Low | Solid UniTask/R3 implementation for car enter/exit flow; properly disposes subjects in `OnDestroy`. | Complete wiring when locomotion system is added. |
| **`SceneGroupLoaderDeviceFix.cs`**| `XrRacing.Gameplay.SdkPatches` | 212 | **B** | ⚠️ Low | Necessary reflection workaround for Meta SDK serialization bug on Quest. Well-documented root cause. | Isolate to sample build configurations. |
| **`QuestBuildDeploy.cs`** | *(global / Editor)* | 275 | **A-** | N/A | Reliable batch build script with `try ... finally` application ID restoration and exit code handling. | Keep global signature for CI compatibility. |
| **`DriverSettingsMenuBuilder.cs`** | *(global / Editor)* | 426 | **B** | N/A | Procedural UI builder using Meta UISet prefabs. Uses hardcoded package paths. | Wrap in Editor namespace or extract constants. |
| **`CreateSampleBuildProfiles.cs`**| *(global / Editor)* | 58 | **B+** | N/A | Concise editor utility for generating build profile assets. | Wrap in Editor namespace. |
| **`ArcadeKart.cs`** *(Ported)* | `KartGame.KartSystems` | 603 | **B** | ⚠️ Med | Ported physics controller from Unity Karting Microgame. Complex raycast suspension and wheel friction model. | Maintain as reference port without unneeded edits. |

---

## 3. High-Priority Quality Debt & Action Plan

### Priority 1: Stop Hot-Path Allocations in `SteeringWheelTransformer.cs`
- **Issue:** In `UpdateTransform()`, `string perHand = ""` and string interpolations run every single frame that the steering wheel is held, generating several KB of heap garbage per second.
- **Fix:** Enclose all debug string formatting and `WheelDebugLog.Write` calls within `if (WheelDebugLog.Enabled)`.

### Priority 2: Decouple Diagnostics in `XRWheelInput.cs`
- **Issue:** `XRWheelInput` contains extensive diagnostics (`LogInteractorStatus`, `LogHandGrabAttempts`, reflection into `Oculus.Interaction.HandGrab.HandGrabInteractor._gripCollider`).
- **Fix:** Ensure `debugLog` defaults to `false`. Wrap reflection lookups and heavy string building in conditional compilation blocks.

### Priority 3: Add Assembly Definitions (`.asmdef`)
- **Issue:** Lack of assembly definitions means all gameplay and editor code compiles into `Assembly-CSharp.dll`, forcing full project recompiles on any script touch.
- **Fix:** Create `XRRacing.Gameplay.asmdef` and `XRRacing.Editor.asmdef`.
