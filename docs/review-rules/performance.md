---
severity: must-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs"
enabled: true
added: 2026-11-01
---
# Performance (Quest standalone, 72–120 Hz)

- No heap allocations per frame (`Update`, `LateUpdate`, `FixedUpdate`, transformer
  callbacks): no `new` arrays/lists, LINQ, capturing lambdas, string concatenation or
  interpolation, boxing.
- No `GetComponent*`, `Find*`, `Camera.main` or reflection per frame; cache in `Awake`.
- No coroutine or loop that polls forever in every scene without a stated reason.
- Non-alloc physics queries (`RaycastNonAlloc`) in anything called often.
- Prefabs and scenes don't ship with diagnostics enabled.

**Exempt:** diagnostics code that is compiled out of release and opt-in.
