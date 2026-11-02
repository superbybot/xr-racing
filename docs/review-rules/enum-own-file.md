---
severity: should-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs, xr-racing-app/Assets/Editor/**/*.cs"
enabled: true
added: 2026-11-02
---
# Enums in their own file

Every enum lives in its own file named after it (`RaceState` → `RaceState.cs`), at namespace
level, with its own `/// <summary>`. Flag an enum declared next to another type, including a
public enum nested in a class (`HeadAndHandsAvatarDriver.Mode`): move it out and give it a name
that stands on its own (`AvatarDriverMode`).

**Why:** enums are shared vocabulary; a separate file makes them easy to find and reuse, and keeps
the class file about the class.
**Exempt:** a `private` enum used only inside its class.
**Example:** `public enum RaceState { … }` at the top of `RaceManager.cs` → `Race/RaceState.cs`.
