# Unity Project Rules for Antigravity

All project rules — reference project, CI/CD & agent guardrails, and the aider delegation workflow — live in `CLAUDE.md` (and `GEMINI.md`) at the repository root as the single source of truth. Where it says "director", that is you.

## CI/CD & Agent Guardrails

1. **Unity Asset Meta Integrity:** Never create, move, rename, or delete a Unity asset without its paired `.meta` file. Never change or corrupt existing GUIDs.
2. **Never Touch Generated Folders:** Never stage or edit generated files under `xr-racing-app/Library`, `Temp`, `Obj`, `Build`, `Builds`, `Logs`, or `UserSettings`.
3. **Builds and Deploys:** Builds and deploys to the Meta Quest happen only via the self-hosted GitHub Actions runner on the Windows machine (or via Editor scripts when authorized) — never run ad-hoc background build commands that conflict with active editor sessions.
4. **Editor-Only Code Isolation:** Editor-only C# outside the `Assets/Editor` folder must be wrapped in `#if UNITY_EDITOR`.
5. **XR Interaction Hierarchy:** Prefer Meta XR SDK interaction components (`Grabbable`, `HandGrabInteractable`, transformers) over Unity XR Interaction Toolkit equivalents for new grabbable/interactable objects. XR Interaction Toolkit remains used for locomotion anchors where Meta SDK does not provide an equivalent.

## Reference Assets Location

Ported reference assets from `xr-sandbox` live in:
- `Assets/References/Karting Reference/`: Unity Karting Microgame reference assets.
- `Assets/References/Input Reference/`: XR cockpit rig reference.
Treat these folders as reference/source, not for direct modification. Project-specific gameplay assets and scripts live under `Assets/Gameplay/`.

## Aider Delegation & Windows Specifics

When invoking aider on Windows:
- Always use flags: `--yes-always --no-auto-commits --no-git --no-show-model-warnings --no-check-update --no-show-release-notes --no-pretty --edit-format diff`.
- Ensure `--no-pretty` is passed in PowerShell to avoid Windows console / xterm prompt toolkit initialization errors.
- Ensure `OPENAI_API_BASE=http://localhost:1234/v1` is set and `OPENAI_BASE_URL` is unset.
