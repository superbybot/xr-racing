---
severity: should-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs, xr-racing-app/Assets/Editor/**/*.cs"
enabled: true
added: 2026-11-02
---
# `<summary>` on every class

Every class, struct, interface and enum the PR adds, or whose file it changes, has an XML
`/// <summary>` doc comment directly above it saying what it is for (one or two
sentences: its role, not a restatement of its name).

**Why:** a reader can tell what a component does from the type alone, and IDEs show it on hover.
**Exempt:** nested private helper types under ~10 lines.
**Example:**
```csharp
/// <summary>
/// Smooths the kart camera's follow so steering input doesn't jitter the view.
/// </summary>
public class KartCameraFollow : MonoBehaviour
```
