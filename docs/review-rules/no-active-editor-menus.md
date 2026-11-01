---
severity: must-fix
applies_to: "xr-racing-app/Assets/**/*.cs"
enabled: true
added: 2026-11-01
---
# No active Editor menu items in commits

`[MenuItem(...)]` attributes (and `[ContextMenu(...)]`) must be committed commented out:

```csharp
// [MenuItem("XR Racing/Build Track Scenes")]
public static void Build()
```

Flag any active (uncommented) menu attribute on a changed line, or in a file the PR
adds or moves. The method itself stays; only the attribute is commented out, so the
menu can be switched back on by removing the `//` when someone is about to use it.

**Why:** leftover menus clutter the Editor and invite running one-off builders by accident.
**Exempt:** a menu the PR description says is in active use, under `## Review focus`
or in the summary. CI entry points (`-executeMethod`) don't need a menu at all.
