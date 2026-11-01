---
severity: should-fix
applies_to: "**"
enabled: true
added: 2026-11-01
---
# Folder conventions

Check that every new or moved file sits where the repo layout says it belongs, and
that any new folder makes sense (a real domain or kind of file, not a one-off or a
duplicate of an existing folder). Suggest the right location when it doesn't.

- Runtime C#: `xr-racing-app/Assets/Gameplay/Scripts/<Domain>/` (Characters, Input,
  Race, Settings, Tracks, UI, Vehicle, Network, SdkPatches, …) with namespace
  `XrRacing.Gameplay.<Domain>`. Debug/diagnostic code: `Gameplay/Scripts/Diagnostics/`.
- Editor C#: `Assets/Editor/Build/` (build and deploy) or `Assets/Editor/Builders/`
  (scene/UI builders), namespace `XrRacing.Editor.<Folder>`.
- Adopted third-party code: `Assets/References/` — never our own new code.
- Prefabs, scenes, materials: under `Assets/Gameplay/<Prefabs|Scenes|…>/`, grouped by feature.
- Reusable scripts and tools: `tools/` (test helpers in `tools/autotest/<operator|probes|setup|recording>/`)
  with descriptive names. Docs: `docs/`. Temporary output: `_temp/` (never committed).
- Nothing temporary or generated inside `xr-racing-app/`.

**Why:** a predictable layout keeps assemblies, namespaces and ownership clear.
**Exempt:** paths a package or SDK requires (e.g. `Resources/`, `Plugins/`, `StreamingAssets/`).
