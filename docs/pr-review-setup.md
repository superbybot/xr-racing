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
   Manual alternative (works with write access only, e.g. a repo owned by another
   account): run `claude setup-token` in a real terminal, copy only the printed
   `sk-ant-oat…` token, then in Claude Code run
   `! pbpaste | gh secret set CLAUDE_CODE_OAUTH_TOKEN --repo <owner>/<repo>`. Typing
   `gh secret set` via `!` without a pipe stores an empty secret (it can't prompt).
   No app install is needed, because File 1 posts with `GITHUB_TOKEN`. `claude
   setup-token` alone does NOT store the secret. Check with `gh secret list`; a run that
   ends after 1 turn with `total_cost_usd: 0` means the stored value isn't a valid token.
   To bill the API instead, store `ANTHROPIC_API_KEY` and use `anthropic_api_key:`.
   Pushing the workflow needs `gh auth refresh -h github.com -s workflow` once.
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

Comments are posted with the job's own `GITHUB_TOKEN`, so the Claude GitHub App only
needs the `CLAUDE_CODE_OAUTH_TOKEN` secret, not an app install on the repo owner's
account. The summary comment is edited in place on each push (`--edit-last`), so a PR
has one live `## PR review` comment naming the commit it covers.

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

      - uses: anthropics/claude-code-action@v1
        with:
          claude_code_oauth_token: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}
          # Comment with the workflow's own token, so the Claude GitHub App doesn't have to be
          # installed on the repo owner's account.
          github_token: ${{ secrets.GITHUB_TOKEN }}
          prompt: |
            REPO: ${{ github.repository }}
            PR NUMBER: ${{ github.event.pull_request.number }}

            You are reviewing this pull request.

            Rules: read every `*.md` file in docs/review-rules/ except README.md and files
            starting with `_`. Each has YAML front matter: skip it if `enabled: false`;
            apply it only to changed files matching `applies_to` (comma-separated globs,
            all files when absent); use its `severity` (must-fix / should-fix / nit).
            Also apply any extra checks listed under a "## Review focus" heading in the PR
            description (`gh pr view ${{ github.event.pull_request.number }} --json title,body`), for this PR only. Review against these rules plus
            real bugs, nothing else. Look at the changed lines (`gh pr diff ${{ github.event.pull_request.number }}`) and read the
            surrounding files for context. The project's CLAUDE.md has background if you need it.

            1. For each issue, post an inline comment on the line with
               mcp__github_inline_comment__create_inline_comment. Start it with the severity
               in bold (**Must fix**, **Should fix** or **Nit**) and the rule file name in
               brackets (e.g. [performance] or [bug]), then the problem in one sentence,
               then the concrete fix (a suggestion block when it's small).
            2. Then write the summary comment, updating the previous one instead of adding
               a new comment on every push:
               `gh pr comment ${{ github.event.pull_request.number }} --edit-last --create-if-none --body "<summary>"`.
               Use exactly this shape so an agent can parse it:

               ## PR review
               Verdict: CHANGES REQUESTED | APPROVED
               Commit: ${{ github.event.pull_request.head.sha }}
               - [ ] **Must fix** [rule] `path/File.cs:123` — problem — fix
               - [ ] **Should fix** ...
               - [ ] **Nit** ...

               Use "Verdict: APPROVED" with no list when nothing needs changing.
            Don't comment on unchanged code unless the change breaks it. Don't praise.
          claude_args: |
            --allowedTools "mcp__github_inline_comment__create_inline_comment,Bash(gh pr comment:*),Bash(gh pr diff:*),Bash(gh pr view:*),Read,Grep,Glob"
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

- PascalCase for types, methods, properties, events and constants. Fields are
  `_camelCase` at every access level (see `field-naming.md`).
- `var` when the type is obvious from the right-hand side.
- One type per file, named after the type. Namespace matches the folder:
  `XrRacing.Gameplay.<Folder>`, `XrRacing.Editor.<Folder>`.
- `using` directives, not fully qualified names.
- Braces on every body, Allman style, 4-space indent.
- Async: UniTask, `...Async` suffix, a `CancellationToken` through the await chain.
````

### `docs/review-rules/access-modifiers.md`

````markdown
---
severity: should-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs, xr-racing-app/Assets/Editor/**/*.cs"
enabled: true
added: 2026-11-02
---
# Explicit access modifiers

Every type and member (class, struct, enum, field, property, method, event, nested type)
states its access modifier (`public`, `private`, `protected`, `internal`). Flag a
declaration that relies on the default, e.g. `Camera _backgroundCamera;` or `void Awake()`.

**Why:** the default differs by context (class members `private`, top-level types
`internal`); writing it out makes intent clear at a glance.
**Exempt:** interface members, enum values, explicit interface implementations, local
variables and local functions (C# doesn't allow a modifier there).
**Example:** `void Awake()` → `private void Awake()`.
````

### `docs/review-rules/field-naming.md`

````markdown
---
severity: should-fix
applies_to: "xr-racing-app/Assets/Gameplay/**/*.cs, xr-racing-app/Assets/Editor/**/*.cs"
enabled: true
added: 2026-11-02
---
# Fields start with an underscore

Every field declared in a class or struct is `_camelCase`, whatever its access level:
`public Camera _backgroundCamera;`, `[SerializeField] private float _jumpHeight;`,
`private static Config _config;`. Flag a new or renamed field without the `_`.

**Why:** fields are told apart from locals, parameters and properties at a glance.
**Exempt:** `const` fields (PascalCase); fields the PR doesn't add or rename.

**Renaming a serialized field** (public, or `[SerializeField]`): don't use
`[FormerlySerializedAs]`. The same PR renames the old key everywhere Unity stored it, so
no Inspector value is lost. Flag as **Must fix** any `[FormerlySerializedAs]`, and any
place the PR missed that still has the old name:
- the component's block in `.prefab`, `.unity` and `.asset` files, i.e. a `  oldName:` key
  under a `MonoBehaviour` whose `m_Script` guid is the script's `.meta` guid
- prefab and scene overrides: `propertyPath: oldName` (and `oldName.` / `oldName.Array`)
- `.anim` curves that animate the field: `attribute: oldName`
- code that uses the name as a string: `nameof`, `FindProperty("oldName")`, reflection,
  and tool scripts that read or write the YAML
Check with `grep -rn "oldName"` across the repo: after the PR, only unrelated hits remain.
**Example:** `private Camera bgCamera;` → `private Camera _bgCamera;`
````

### `docs/review-rules/class-summary.md`

````markdown
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
````

### `docs/review-rules/enum-own-file.md`

````markdown
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
**Exempt:** CI/CD menus — build and deploy commands in `Assets/Editor/Build/` stay active
(e.g. `Build Commands/Build and Deploy to Quest`). Also a menu the PR description says is
in active use, under `## Review focus` or in the summary.
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
- Green run but no comments: the job lacks `pull-requests: write`, or `github_token:` was
  removed from the workflow (then the Claude GitHub app must be installed on the repo).
- PRs from forks can't read secrets, so they aren't reviewed.
- Re-run a review without new commits: `gh run rerun <run-id>` (find it with `gh pr checks <PR>`).
