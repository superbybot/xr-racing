---
severity: should-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs, xr-racing-app/Assets/Editor/**/*.cs"
enabled: true
added: 2026-11-01
---
# Microsoft C# conventions

- PascalCase for types, methods, properties, events, constants and public fields;
  `_camelCase` for private fields in new code (existing serialized fields keep their
  names until renamed with `[FormerlySerializedAs]`).
- `var` when the type is obvious from the right-hand side.
- One type per file, named after the type. Namespace matches the folder:
  `XrRacing.Gameplay.<Folder>`, `XrRacing.Editor.<Folder>`.
- `using` directives, not fully qualified names.
- Braces on every body, Allman style, 4-space indent.
- Async: UniTask, `...Async` suffix, a `CancellationToken` through the await chain.
