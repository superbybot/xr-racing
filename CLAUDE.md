# xr-racing

## Reference project: xr-sandbox

This project's implementation leans heavily on
`/Users/byronbautista/xr-sandbox/xr-sandbox-app` — it's a sister sandbox
project with the XR driving rig, Meta XR SDK setup, and a Unity Karting
Microgame reference already worked out, and is where the assets currently in
`xr-racing-app/Assets/References/` were ported from.

Don't hesitate to look there when deciding how to implement something here —
check its scripts, prefabs, and package setup for existing patterns before
building from scratch. Treat it as a reference/source, not something to edit.

## CI/CD & Agent Guardrails

1. Never create/move/rename/delete a Unity asset without its .meta file, and never change existing GUIDs.
2. Never stage or edit generated files under xr-racing-app/Library, Temp, Obj, Build, Builds, Logs, or UserSettings.
3. Builds and deploys to the Quest happen only via the self-hosted GitHub Actions runner on the Windows machine, triggered by push to main — never by invoking Unity.exe or other Windows binaries directly from a macOS/non-Windows session.
4. Editor-only C# outside the Assets/Editor folder must be wrapped in #if UNITY_EDITOR.
5. Prefer Meta XR SDK interaction components (Grabbable, HandGrabInteractable, transformers) over Unity XR Interaction Toolkit equivalents for new grabbable/interactable objects. XR Interaction Toolkit remains a required dependency for locomotion (Teleportation/Locomotion System), since Meta's SDK doesn't provide an equivalent for that.

## Aider workflow & global Unity rules

The director/aider workflow and the Unity & C# rules are global — they live in
`superbybot/global-ai-configs` (`shared/`), linked at `~/.ai-rules` on every
machine and loaded by each agent's global `CLAUDE.md` / `GEMINI.md`. Don't copy
them back here. If they aren't in your context, read
`~/.ai-rules/AIDER_WORKFLOW.md`, `~/.ai-rules/GLOBAL_UNITY_RULES.md`, and
`~/.ai-rules/MACHINES.md` before starting.

xr-racing specifics on top of those:
- The Unity project is in `xr-racing-app/`, not the git root: read the Editor
  version from `xr-racing-app/ProjectSettings/ProjectVersion.txt` and pass
  `-projectPath xr-racing-app` to a headless pass. Still run aider with cwd at
  the git root.
- Log aider runs with `"repo": "xr-racing"`.
- Guardrail 3 above overrides any global build/deploy guidance.

## Autonomous testing

Before testing gameplay yourself (Play mode, screenshots, driving), read
`docs/autonomous-testing.md` and follow its rules. In short: drive through the
real input path (simulated controllers on the wheel via Meta XR Operator, not
keyboard or physics hacks), never add test-only components to gameplay scenes,
save captures to `test-captures/` (never under `Assets/`), and clean up inputs,
Play mode and Simulator settings when done. Helper scripts are in `tools/autotest/`.
