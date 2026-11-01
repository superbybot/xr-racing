# Automated PR review with Claude — setup guide

A portable recipe: every pull request gets reviewed by Claude in GitHub Actions
against the repo's own written rules, and the local coding agent reads that review
and applies the fixes. Hand this file to another project (or its agent) to set the
same thing up. xr-racing's live copies are `.github/workflows/pr-review.yml` and
`docs/review-rules/`.

## How it works

1. A PR is opened or updated → the `PR review (Claude)` workflow runs on a
   GitHub-hosted runner.
2. Claude reads every enabled rule file in `docs/review-rules/` (plus any
   `## Review focus` section in the PR description), reviews the diff, posts inline
   comments (tagged **Must fix** / **Should fix** / **Nit**) and one summary comment
   starting with `## PR review` and a `Verdict:` line.
3. The local agent (Claude Code, Gemini, …) that opened the PR waits for the run,
   reads the comments, applies the valid ones, pushes — which triggers a re-review —
   and replies to anything it declines.

## Setup (once per repository)

1. **Token** (uses your Claude subscription, not per-token API billing). On any
   machine with Claude Code logged in:
   ```
   claude setup-token
   ```
   Copy the token it prints.
2. **Repo secret** named `CLAUDE_CODE_OAUTH_TOKEN`:
   ```
   gh secret set CLAUDE_CODE_OAUTH_TOKEN --repo <owner>/<repo>
   ```
   (paste the token when prompted), or Settings → Secrets and variables → Actions.
   To bill the API instead, store `ANTHROPIC_API_KEY` and replace the
   `claude_code_oauth_token:` line with `anthropic_api_key: ${{ secrets.ANTHROPIC_API_KEY }}`.
3. **GitHub app**: install the Claude GitHub app on the repo
   (https://github.com/apps/claude), or run `/install-github-app` inside Claude Code,
   so the action can post comments.
4. Add the two files below and push them to the default branch. The workflow
   reviews PRs from then on; it skips draft PRs.

## File 1: `.github/workflows/pr-review.yml`

Adjust `sparse-checkout` to the folders the reviewer needs (drop it entirely for a
small repo; it's there because Unity asset folders are huge).

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
          sparse-checkout: |
            .github
            docs
            src            # ← your code folders

      - uses: anthropics/claude-code-action@v1
        with:
          claude_code_oauth_token: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}
          prompt: |
            REPO: ${{ github.repository }}
            PR NUMBER: ${{ github.event.pull_request.number }}

            You are reviewing this pull request.

            Rules: read every `*.md` file in docs/review-rules/ except README.md and files
            starting with `_`. Each has YAML front matter: skip it if `enabled: false`;
            apply it only to changed files matching `applies_to` (comma-separated globs,
            all files when absent); use its `severity` (must-fix / should-fix / nit).
            Also apply any extra checks listed under a "## Review focus" heading in the PR
            description (`gh pr view`), for this PR only. Review against these rules plus
            real bugs, nothing else. Look at the changed lines (`gh pr diff`) and read the
            surrounding files for context.

            1. For each issue, post an inline comment on the line with
               mcp__github_inline_comment__create_inline_comment. Start it with the severity
               in bold (**Must fix**, **Should fix** or **Nit**) and the rule file name in
               brackets (e.g. [performance] or [bug]), then the problem in one sentence,
               then the concrete fix (a suggestion block when it's small).
            2. Then post one summary comment with `gh pr comment`, exactly in this shape so
               an agent can parse it:

               ## PR review
               Verdict: CHANGES REQUESTED | APPROVED
               - [ ] **Must fix** [rule] `path/File.cs:123` — problem — fix
               - [ ] **Should fix** ...
               - [ ] **Nit** ...

               Use "Verdict: APPROVED" with no list when nothing needs changing.
            Don't comment on unchanged code unless the change breaks it. Don't praise.
          claude_args: |
            --allowedTools "mcp__github_inline_comment__create_inline_comment,Bash(gh pr comment:*),Bash(gh pr diff:*),Bash(gh pr view:*),Read,Grep,Glob"
```

## File 2: the rules folder, `docs/review-rules/`

One markdown file per rule, so rules can be added, paused or removed at any time
without touching the workflow. The reviewer reads every `*.md` in the folder except
`README.md` and files starting with `_`.

Rule file format (`docs/review-rules/<short-name>.md`):

```markdown
---
severity: must-fix        # must-fix | should-fix | nit
applies_to: "src/**/*.cs" # glob(s), comma-separated; omit = all changed files
enabled: true             # false = keep the file but stop checking it
added: 2026-11-01
---
# Rule title

What to flag, in one or two sentences.

**Why:** the reason (helps the reviewer judge edge cases).
**Exempt:** cases that are fine.
**Example:** a short bad → good snippet, if it helps.
```

Also add a `README.md` in the folder explaining this format, and a `_template.md`
(ignored by the reviewer) to copy from. Starter rules from xr-racing (Unity/C#) —
one file each; adapt paths and drop what doesn't apply:

| File | Severity | Rule |
|---|---|---|
| `comments.md` | should-fix | No comments that restate the code; keep why-comments; stale comments are bugs. |
| `no-runtime-addcomponent.md` | must-fix | Dependencies are `[SerializeField]` fields wired in scenes/prefabs; no runtime `AddComponent`, UI built with `new GameObject`, `GetComponent`/`Find`/`Camera.main` fallbacks (Editor builders, tools and documented SDK workarounds exempt). |
| `csharp-conventions.md` | should-fix | Microsoft C# conventions: PascalCase members, `_camelCase` private fields, `var` when obvious, namespace = folder, `using` not full names, Allman braces, `...Async` + `CancellationToken`. |
| `architecture.md` | should-fix | MVVM / Clean Architecture where it fits: models free of MonoBehaviour/UI, thin views, dependencies point inward, no new static state, diagnostics isolated and off by default, split large mixed classes. |
| `performance.md` | must-fix | No per-frame allocations, lookups or reflection; no forever-polling loops; non-alloc physics; no diagnostics enabled in shipped prefabs. |
| `unity-assets.md` | must-fix | `.meta` with every asset change, GUIDs unchanged, `#if UNITY_EDITOR` for Editor code outside Editor folders, `OnEnable`/`OnDisable` symmetry, nothing generated or temporary committed. |

**One-off checks:** for a single PR, put extra checks under a `## Review focus`
heading in the PR description; the reviewer applies them to that PR only.

## File 3: rule for the local agent (paste into `CLAUDE.md` / `GEMINI.md`)

```markdown
## PR review loop

Every PR is reviewed by the `PR review (Claude)` workflow against the enabled rules
in `docs/review-rules/`. To add, change or pause a rule, edit that folder (one file
per rule; `enabled: false` pauses it) — never the workflow. After opening a PR or
pushing to one:
1. Wait for it: `gh pr checks <PR> --watch` (the "review" check).
2. Read the result: `gh pr view <PR> --comments` (the `## PR review` summary) and
   `gh api repos/{owner}/{repo}/pulls/<PR>/comments` (inline comments).
3. Apply every **Must fix**, and **Should fix** unless it's wrong — verify each claim
   against the code first; the reviewer can be mistaken. Nits are optional.
4. Re-run the project's checks (compile/tests), commit, push. The push re-runs the review.
5. Reply to any comment you decline, with the reason (`gh pr comment` or a reply on the thread).
6. Stop after 3 rounds, or when the verdict is APPROVED, and report what was
   applied and declined. Never merge on the user's behalf unless asked.
```

## Checking it works

- Open a small test PR → the **Actions** tab shows `PR review (Claude)` → the PR gets
  inline comments plus a `## PR review` summary within a few minutes.
- `401`/auth errors: the secret is missing or the token expired — rerun
  `claude setup-token` and update the secret.
- No comments but a green run: check the Claude GitHub app is installed on the repo
  and the job has `pull-requests: write`.
- Forked PRs can't read secrets, so they aren't reviewed.
