---
severity: should-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs, xr-racing-app/Assets/Editor/**/*.cs"
enabled: true
added: 2026-11-01
---
# Microsoft C# conventions

- PascalCase for types, methods, properties, events and constants. Fields are
  `_camelCase` at every access level (see `field-naming.md`).
- `var` when the type is obvious from the right-hand side.
- One type per file, named after the type. Namespace matches the folder:
  `XrRacing.Gameplay.<Folder>`, `XrRacing.Editor.<Folder>`.
- `using` directives, not fully qualified names.
- Braces on every body, Allman style, 4-space indent.
- Async: UniTask, `...Async` suffix, a `CancellationToken` through the await chain.
