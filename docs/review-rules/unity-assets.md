---
severity: must-fix
applies_to: "**"
enabled: true
added: 2026-11-01
---
# Unity assets and repo hygiene

- Every new, moved or deleted asset has its `.meta`; GUIDs never change.
- Editor-only code outside an `Editor` folder is wrapped in `#if UNITY_EDITOR`.
- Event subscriptions made in `OnEnable` are removed in `OnDisable`.
- Prefer Meta XR SDK interaction components over XR Interaction Toolkit for new interactables.
- Nothing generated (`Library/`, `Temp/`, `Logs/`, …) or temporary (`_temp/`) is committed;
  test captures and videos never go inside `xr-racing-app/`.
