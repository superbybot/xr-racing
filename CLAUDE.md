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

## Aider delegation workflow (copy of global rule)

> **This section is a copy of the global rule in `~/.claude/CLAUDE.md` (macOS).**
> It lives here so every machine and agent working in this repo — including the
> Windows PC, and both Claude Code and Gemini CLI (`GEMINI.md` imports this
> file) — follows the same workflow. If the global file changes, re-sync this
> section. "Director" below means whichever agent is driving the session
> (Claude or Gemini).
>
> **Machine-specific bits to adapt per machine** (marked ⚙️): shell-profile
> location, `lms` path, Unity Editor path, and where the log files live. The
> macOS values are shown; on Windows use the equivalents. Guardrail 3 above
> still governs builds/deploys on every machine.

For any non-trivial implementation task (new features, refactors, multi-file
changes, bug fixes with a known plan), the director **plans and reviews**, and
delegates the actual code-writing to **aider** running headless against a
self-hosted local model. The director does not use Edit/Write to hand-implement
work that fits this flow.

### Division of labor

- **Director:** understand the request, explore the codebase, form the plan,
  break it into concrete aider tasks (instruction + exact file paths), invoke
  aider non-interactively, review the diff/output, run tests/build/lint, iterate
  with follow-up aider calls (or fix directly only if it's a trivial one-liner),
  and do all git operations, planning, and read-only investigation.
- **Aider:** the actual file edits for planned implementation work.

### Plan documents

End every implementation plan with an **"Execution sequence for aider"** section,
written for an AI to execute directly:
- Separate git file operations (`git mv`/`git rm`, done by the director) from
  content edits (aider). Order so files exist at their final path before aider
  edits them, and foundational code exists before anything references it.
- List aider work as numbered tasks with exact `--message` text and exact file
  paths, ready to paste into the invocation below.
- Call out anything that's neither the director's nor aider's (e.g. Unity prefab
  surgery that only works inside the Editor) so it isn't skipped or hand-edited
  as serialized YAML.

### Before delegating

- **Uniqueness check.** Aider fails *silently* when the target text isn't unique
  in the file — it edits whichever occurrence looks right and reports success
  (`wrong_edit`, the costliest failure). Grep the target file for the exact
  string first. If it appears more than once, widen the instruction with
  surrounding context or make the edit directly.
- **Never delegate YAML edits** (Unity `.asset`, `.yaml`, `.yml`) — not even small
  ones. A truncated SEARCH block on a large array still "matches" and leaves
  orphaned, invalid YAML with no error; large blocks also overflow the local
  model's context (16k). The director edits YAML directly via Read+Edit. Reserve
  aider for source code (C#, etc.). If YAML ever goes to aider anyway, verify
  against the real diff/line count, not aider's "Applied edit".
- **Never ask aider to create a file from nothing.** The empty-SEARCH-block case
  fails silently (0-byte file, or content written to a doubled/nested path). The
  director creates the file first (`Write`, real skeleton or a `# placeholder`
  line), runs aider with **cwd at the git root**, and afterwards verifies the
  target's actual byte count.

### Invocation

```
env -u OPENAI_BASE_URL OPENAI_API_BASE=http://localhost:1234/v1 OPENAI_API_KEY=dummy \
  aider --yes-always --no-auto-commits --no-git --no-show-model-warnings \
  --edit-format diff \
  --model openai/<model-name> \
  --message "<precise instruction: what to change and why>" \
  <file1> <file2> ...
```
(On Windows, set the same env vars in the shell's own syntax; same flags.)

- Always unset `OPENAI_BASE_URL` and set `OPENAI_API_BASE=http://localhost:1234/v1`
  explicitly — litellm prefers `OPENAI_BASE_URL`, and the LAN IP in the user's
  shell env isn't reachable from the agent's sandboxed shell, so every request
  would silently time out.
- ⚙️ **Model name is never guessed or hardcoded from this doc.** Read the
  `--model` value from the user's `start aider` shell function (macOS:
  `grep -n "start aider" -A5 ~/.zshrc`) — that's the single source of truth.
  Any model named in docs is illustrative and may be stale.
- ⚙️ **Model server is LM Studio** (macOS: `~/.lmstudio/bin/lms`, not on PATH).
  Run `lms ps`; if the `.zshrc` model isn't loaded (including "No models loaded"),
  run `lms load <model>` immediately — don't ask first. `lms ls` only lists
  *local-disk* models; LM Studio can dispatch to a model hosted on another LAN
  node (e.g. "loaded on Byron-PC"), so absence from `lms ls` is not a reason to
  give up. Try `lms load` first, every time.
- Skip short-timeout curl pre-checks (a remote-hosted model can take 60s+ to
  answer). Just invoke aider with a long Bash timeout (~240000 ms). After any
  timeout, re-read the target files to see what actually landed before retrying.
- Always pass explicit file paths; never let aider roam the repo.
- `--edit-format diff` is required: unknown/local models default to `whole`,
  which reproduces the entire file and can silently drop lines or exhaust output
  tokens. Diff mode makes bad output fail loudly instead.
- `--no-git` avoids repo-map/git integration; if aider throws `BadObject` errors
  from git, don't repair the repo — fall back to `--no-git` or direct edits.
- Don't call the `start aider` shell function from the agent's shell (not
  reliably in scope); use the explicit form above. Only *read* it for the model.

### Reporting & logging

- After every aider run, parse `Tokens: X sent, Y received.` (`k` = ×1000,
  `M` = ×1,000,000; sum multiple lines from retries) and report it as
  approximate tokens not spent against the frontier-model API. If there's no
  `Tokens:` line, say so — don't invent a number.
- When all aider calls for a task are done, report one **combined total**
  (sent + received across every invocation) as tokens spent locally / saved.
- After every invocation (success or failure), append one JSON line to the global
  log ⚙️ `~/.claude/aider_usage_log.jsonl` (create if missing; no need to ask):
  `{"timestamp": "<ISO 8601>", "repo": "xr-racing", "files": [...], "task": "<one line>", "tokens_sent": <int|null>, "tokens_received": <int|null>, "outcome": "clean|needed_retry|wrong_edit|timeout_no_apply|timeout_partial", "notes": "<optional>"}`
  `outcome` is the real success metric, not tokens saved.
- At the end of a session with meaningful aider use, append a dated
  `## YYYY-MM-DD` retrospective to ⚙️ `~/.claude/aider_usage_notes.md`: what
  worked, what didn't, what cost more director effort than it saved, and a real
  recommendation.

### Verifying Unity C# changes

After a *batch* of aider edits (not per file — it's slow), catch real compiler
errors with a headless Editor pass and grep for `error CS`; no matches means a
clean compile, and this is authoritative over reading the diff by hand:
```
<Unity-Editor-binary> -batchmode -quit -nographics \
  -projectPath <project-root> -logFile <logfile-path>
grep "error CS" <logfile-path>
```
⚙️ Read `m_EditorVersion` from `xr-racing-app/ProjectSettings/ProjectVersion.txt`
and locate that exact version (macOS:
`/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity`; on
Windows it's under the Unity Hub `Editor\<version>\Editor\Unity.exe`). Never
assume a version.

### Fallback

Before calling the local server unusable, always attempt `lms load <model>` first.
Only if that fails, or aider's output is clearly wrong after 1–2 real attempts,
say so explicitly and either retry with a more precise instruction or implement
directly. Never silently keep retrying, never silently switch to a paid model,
and never ask the user to load the model manually before trying `lms load`.
