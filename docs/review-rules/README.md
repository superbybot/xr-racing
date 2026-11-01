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
