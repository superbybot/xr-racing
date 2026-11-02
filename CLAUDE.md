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
save screenshots to `_temp/captures/` and videos to `_temp/videos/` (never under
`xr-racing-app/`), and clean up inputs, Play mode and Simulator settings when done.
Helper scripts are in `tools/autotest/`.

## Repo layout: where things go

Next to `xr-racing-app/` at the git root:
- `tools/` — reusable tools and scripts worth keeping (commit them). Test helpers are
  in `tools/autotest/` by kind: `operator/` (Meta XR Operator driving), `probes/`
  (read-only state dumps), `setup/` (stage a scene/camera for a shot), `recording/`.
  Give new tools descriptive names; delete one-off experiments when done instead of
  committing them.
- `_temp/` — gitignored scratch output, never committed: screenshots and test captures
  in `_temp/captures/<date>_<topic>/`, videos in `_temp/videos/`, other throwaway files
  in `_temp/`. Nothing temporary goes inside `xr-racing-app/` (under `Assets/` it
  creates `.meta` files).
- `docs/` — all project documentation. Remove docs that are obsolete rather than
  archiving them.

Inside `xr-racing-app/Assets/`: our runtime code is `Gameplay/Scripts/<Domain>/`
(namespace `XrRacing.Gameplay.<Domain>`), diagnostics in `Gameplay/Scripts/Diagnostics/`;
Editor code in `Editor/Build/` and `Editor/Builders/` (`XrRacing.Editor.<Folder>`);
adopted Karting Microgame code in `References/KartGame/` (its own assembly,
`XrRacing.References.KartGame.*`) — treat it as third-party.

## Code standards and PR review loop

Write C# to the rules in `docs/review-rules/` (no self-explanatory comments, no runtime
`AddComponent` quick fixes — use `[SerializeField]`, Microsoft C# conventions,
explicit access modifiers, `_camelCase` fields at every access level, a `<summary>` on
every type, MVVM/Clean Architecture where it fits, no per-frame allocations).

When the user asks to add, change or drop a review rule, edit `docs/review-rules/`
(one file per rule, front matter per `docs/review-rules/README.md`; `enabled: false`
to pause one). Don't edit the workflow for rule changes.

Every PR is reviewed by the `PR review (Claude)` GitHub Action against the enabled
rules in that folder (setup: `docs/pr-review-setup.md`). One-off checks for a single
PR go under a `## Review focus` heading in its description. After opening a PR or pushing to one:
1. Wait for it: `gh pr checks <PR> --watch` (the "review" check).
2. Read it: `gh pr view <PR> --comments` (the `## PR review` summary) and
   `gh api repos/{owner}/{repo}/pulls/<PR>/comments` (inline comments).
3. Apply every **Must fix**, and **Should fix** unless it's wrong — check each claim
   against the code first. Nits are optional. C# edits go through aider as usual.
4. Re-run the compile check, commit, push (the push re-runs the review).
5. Reply to each comment you decline, with the reason.
6. Stop after 3 rounds or when the verdict is APPROVED; report what was applied and
   declined. Don't merge unless the user asks.
