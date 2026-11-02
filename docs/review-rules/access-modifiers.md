---
severity: should-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs, xr-racing-app/Assets/Editor/**/*.cs"
enabled: true
added: 2026-11-02
---
# Explicit access modifiers

Every type and member (class, struct, enum, field, property, method, event, nested type)
states its access modifier (`public`, `private`, `protected`, `internal`). Flag a
declaration that relies on the default, e.g. `Camera _backgroundCamera;` or `void Awake()`.

**Why:** the default differs by context (class members `private`, top-level types
`internal`); writing it out makes intent clear at a glance.
**Exempt:** interface members, enum values, explicit interface implementations, local
variables and local functions (C# doesn't allow a modifier there).
**Example:** `void Awake()` → `private void Awake()`.
