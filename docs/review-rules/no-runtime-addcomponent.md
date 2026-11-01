---
severity: must-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs"
enabled: true
added: 2026-11-01
---
# No runtime AddComponent / object-building quick fixes

Dependencies are `[SerializeField] private` fields wired in the scene or prefab (or by
an Editor builder script). Flag in runtime code: `AddComponent<T>()`, `new GameObject(...)`
to build UI or hierarchy, `GetComponent*`/`Find*` fallbacks such as
`if (x == null) x = GetComponent<X>()`, `Camera.main` fallbacks, and
`RuntimeInitializeOnLoadMethod` used to spawn components.

**Why:** hidden runtime wiring breaks silently and can't be configured in the Inspector.
**Exempt:** Editor builder scripts (`Assets/Editor/Builders/`), `tools/`, and SDK
workarounds in `SdkPatches/` that have no other hook (they must say why).
