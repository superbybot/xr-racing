# PR review kit — Claude reviews every pull request

Self-contained: everything needed to add an automated Claude PR reviewer to a repo,
with rules kept as one markdown file per rule so they can be added or paused any time
without touching the workflow. Give this file to a project's agent and say "set this up".

Source of truth: `global-ai-configs/shared/skills/pr-review-setup/` (the
`pr-review-setup` skill). Generated from xr-racing's live setup — regenerate rather
than hand-edit.

## How it works

1. A non-draft PR is opened or updated → `.github/workflows/pr-review.yml` runs on a
   GitHub-hosted runner.
2. Claude reads every enabled rule in `docs/review-rules/` (plus any `## Review focus`
   section in the PR description), reviews the diff, posts inline comments tagged
   **Must fix** / **Should fix** / **Nit** with the rule name, and one summary comment
   starting `## PR review` with a `Verdict:` line.
3. The local agent that opened the PR reads the review, applies valid fixes, pushes
   (which re-runs the review) and replies to anything it declines.

## Setup steps

1. **Auth (once per repo).** Easiest: run `/install-github-app` in Claude Code — it
   installs the Claude GitHub app and creates the `CLAUDE_CODE_OAUTH_TOKEN` secret.
   Manual alternative: `claude setup-token`, copy the printed token, then
   `gh secret set CLAUDE_CODE_OAUTH_TOKEN --repo <owner>/<repo>` and paste it, and install
   https://github.com/apps/claude on the repo. (`claude setup-token` alone does NOT
   store the secret.) Check with `gh secret list`. To bill the API instead, store
   `ANTHROPIC_API_KEY` and use `anthropic_api_key:` in the workflow.
2. **Workflow:** create `.github/workflows/pr-review.yml` from File 1; set
   `sparse-checkout` to the repo's code folders or delete that block.
3. **Rules:** create `docs/review-rules/` with `README.md`, `_template.md` and the rule
   files below. The starter rules are from a Unity/C# project — keep what applies,
   fix `applies_to` globs and paths, drop the rest, add the project's own.
4. **Agent rule:** paste the "PR review loop" section into the project's
   `CLAUDE.md` / `GEMINI.md`.
5. Push to the default branch, open a small test PR, and confirm the `review` check
   posts comments.

## File 1: `.github/workflows/pr-review.yml`

```yaml
name: PR review (Claude)

on:
  pull_request:
    types: [opened, synchronize, reopened, ready_for_review]

concurrency:
  group: pr-review-${{ github.event.pull_request.number }}
  cancel-in-progress: true

jobs:
  review:
    if: github.event.pull_request.draft == false
    runs-on: ubuntu-latest
    timeout-minutes: 30
    permissions:
      contents: read
      pull-requests: write
      issues: write
      id-token: write
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 1
          # Optional: check out only code + docs (big asset folders slow the job). Delete for small repos.
          sparse-checkout: |
            .github
            docs
            src            # <- your code folders
```

## File 2: `docs/review-rules/README.md`

````markdown
# PR review rules

The `PR review (Claude)` workflow (`.github/workflows/pr-review.yml`) reviews every
pull request against **every enabled rule file in this folder**. Agents follow the
same rules when writing code and when applying review feedback. Nothing else needs
changing to add, edit or retire a rule — the workflow reads this folder each run.

## Add a rule

Create `docs/review-rules/<short-name>.md` (copy `_template.md`), or ask the agent:
"add a review rule: …". One rule (or one closely related group) per file:

```markdown
---
severity: must-fix        # must-fix | should-fix | nit
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs"   # glob(s), comma-separated; omit = all changed files
enabled: true             # false = keep the file but stop checking it
added: 2026-11-01
---
# Rule title

What to flag, in one or two sentences.

**Why:** the reason (helps the reviewer judge edge cases).
**Exempt:** cases that are fine.
**Example:** a short bad → good snippet, if it helps.
```

## Retire or change a rule

Edit the file. Set `enabled: false` to pause it, or delete it. Rule changes apply to
the next review run — including on the PR that changes them.

## Severity

- **must-fix** — bugs and hard rules; the agent always applies these.
- **should-fix** — applied unless the agent finds the claim wrong (it replies why).
- **nit** — optional style.

## Scope

`xr-racing-app/Assets/References/` is adopted third-party code: only flag a change
there if it breaks something.
````

## File 3: `docs/review-rules/_template.md`

````markdown
---
severity: should-fix
applies_to: "xr-racing-app/Assets/**/*.cs"
enabled: false
added: YYYY-MM-DD
---
# Rule title

What to flag.

**Why:** …
**Exempt:** …
````

## Starter rules: `docs/review-rules/<name>.md`

One file each. Adapt paths and `applies_to` to the project.

### `docs/review-rules/architecture.md`

````markdown
---
severity: should-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs"
enabled: true
added: 2026-11-01
---
# MVVM / Clean Architecture where it fits

- Data and rules (models like `DriverSettings`, race/lap logic) stay free of
  `MonoBehaviour`, scene lookups and UI types where practical.
- Views (UI MonoBehaviours) display state and forward input; they don't own game rules.
- Dependencies point inward: UI → gameplay → data. Gameplay must not read UI state
  (e.g. `DriverSettingsMenu.IsOpen`) directly; pass an interface or event.
- No new static mutable state or singletons.
- Diagnostics live in `Gameplay/Scripts/Diagnostics/`, use `DiagnosticCsvWriter`, compile
  only under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, and are off by default.
- Split a class over ~300 lines that mixes responsibilities; share duplicated logic.
````

### `docs/review-rules/comments.md`

````markdown
---
severity: should-fix
applies_to: "**/*.cs, tools/**"
enabled: true
added: 2026-11-01
---
# No self-explanatory comments

Flag comments that restate the code: `// Initialize variables`, `// Find Playground track`,
`// Smooth fade out`, `// 3, 2, 1`, a color name next to `new Color(...)`. Flag stale
comments that no longer match the code (treat as a bug).

**Why:** they add noise and go stale; the code already says what it does.
**Keep:** comments that explain why — a workaround, a non-obvious constraint, execution
order, units, a bug being prevented. XML `<summary>` on public types and non-obvious members.
````

### `docs/review-rules/csharp-conventions.md`

````markdown
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
````

### `docs/review-rules/folder-conventions.md`

````markdown
---
severity: should-fix
applies_to: "**"
enabled: true
added: 2026-11-01
---
# Folder conventions

Check that every new or moved file sits where the repo layout says it belongs, and
that any new folder makes sense (a real domain or kind of file, not a one-off or a
duplicate of an existing folder). Suggest the right location when it doesn't.

- Runtime C#: `xr-racing-app/Assets/Gameplay/Scripts/<Domain>/` (Characters, Input,
  Race, Settings, Tracks, UI, Vehicle, Network, SdkPatches, …) with namespace
  `XrRacing.Gameplay.<Domain>`. Debug/diagnostic code: `Gameplay/Scripts/Diagnostics/`.
- Editor C#: `Assets/Editor/Build/` (build and deploy) or `Assets/Editor/Builders/`
  (scene/UI builders), namespace `XrRacing.Editor.<Folder>`.
- Adopted third-party code: `Assets/References/` — never our own new code.
- Prefabs, scenes, materials: under `Assets/Gameplay/<Prefabs|Scenes|…>/`, grouped by feature.
- Reusable scripts and tools: `tools/` (test helpers in `tools/autotest/<operator|probes|setup|recording>/`)
  with descriptive names. Docs: `docs/`. Temporary output: `_temp/` (never committed).
- Nothing temporary or generated inside `xr-racing-app/`.

**Why:** a predictable layout keeps assemblies, namespaces and ownership clear.
**Exempt:** paths a package or SDK requires (e.g. `Resources/`, `Plugins/`, `StreamingAssets/`).
````

### `docs/review-rules/no-active-editor-menus.md`

````markdown
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
````

### `docs/review-rules/no-runtime-addcomponent.md`

````markdown
---
severity: must-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs"
enabled: true
added: 2026-11-01
---
# No runtime AddComponent / object-building quick fixes

Dependencies are `[SerializeField] private` fields wired in the scene or prefab (or by
an Editor builder script). Flag in runtime code: `AddComponent<T>()`, `new GameObject(...)`
to build UI or hierarchy, `GetComponent*`/`Find*` fallbacks such as
`if (x == null) x = GetComponent<X>()`, `Camera.main` fallbacks, and
`RuntimeInitializeOnLoadMethod` used to spawn components.

**Why:** hidden runtime wiring breaks silently and can't be configured in the Inspector.
**Exempt:** Editor builder scripts (`Assets/Editor/Builders/`), `tools/`, and SDK
workarounds in `SdkPatches/` that have no other hook (they must say why).
````

### `docs/review-rules/performance.md`

````markdown
---
severity: must-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs"
enabled: true
added: 2026-11-01
---
# Performance (Quest standalone, 72–120 Hz)

- No heap allocations per frame (`Update`, `LateUpdate`, `FixedUpdate`, transformer
  callbacks): no `new` arrays/lists, LINQ, capturing lambdas, string concatenation or
  interpolation, boxing.
- No `GetComponent*`, `Find*`, `Camera.main` or reflection per frame; cache in `Awake`.
- No coroutine or loop that polls forever in every scene without a stated reason.
- Non-alloc physics queries (`RaycastNonAlloc`) in anything called often.
- Prefabs and scenes don't ship with diagnostics enabled.

**Exempt:** diagnostics code that is compiled out of release and opt-in.
````

### `docs/review-rules/unity-assets.md`

````markdown
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
````

## Agent rule for `CLAUDE.md` / `GEMINI.md`

````markdown
## PR review loop

Every PR is reviewed by the `PR review (Claude)` GitHub Action against the enabled
rules in `docs/review-rules/` (one file per rule; `enabled: false` pauses one). When
the user asks to add, change or drop a review rule, edit that folder — never the
workflow. One-off checks for a single PR go under a `## Review focus` heading in its
description. After opening a PR or pushing to one:
1. Wait for it: `gh pr checks <PR> --watch` (the "review" check).
2. Read it: `gh pr view <PR> --comments` (the `## PR review` summary) and
   `gh api repos/{owner}/{repo}/pulls/<PR>/comments` (inline comments).
3. Apply every **Must fix**, and **Should fix** unless it's wrong — check each claim
   against the code first. Nits are optional.
4. Re-run the project's checks (compile/tests), commit, push (the push re-runs the review).
5. Reply to each comment you decline, with the reason.
6. Stop after 3 rounds or when the verdict is APPROVED; report what was applied and
   declined. Don't merge unless the user asks.
````

## Troubleshooting

- Auth/401 errors or the run fails at the Claude step: the secret is missing or expired
  (`gh secret list`); redo step 1.
- Green run but no comments: the Claude GitHub app isn't installed on the repo, or the
  job lacks `pull-requests: write`.
- PRs from forks can't read secrets, so they aren't reviewed.
- Re-run a review without new commits: `gh run rerun <run-id>` (find it with `gh pr checks <PR>`).
