---
severity: should-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs"
enabled: true
added: 2026-11-01
---
# MVVM / Clean Architecture where it fits

- Data and rules (models like `DriverSettings`, race/lap logic) stay free of
  `MonoBehaviour`, scene lookups and UI types where practical.
- Views (UI MonoBehaviours) display state and forward input; they don't own game rules.
- Dependencies point inward: UI → gameplay → data. Gameplay must not read UI state
  (e.g. `DriverSettingsMenu.IsOpen`) directly; pass an interface or event.
- No new static mutable state or singletons.
- Diagnostics live in `Gameplay/Scripts/Diagnostics/`, use `DiagnosticCsvWriter`, compile
  only under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, and are off by default.
- Split a class over ~300 lines that mixes responsibilities; share duplicated logic.
