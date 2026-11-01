#!/usr/bin/env bash
# Runs the PR reviewer on this machine with your logged-in Claude Code — same rules
# (docs/review-rules/) and output format as .github/workflows/pr-review.yml, no GitHub
# secret needed. Writes _temp/pr-review/pr<N>.md; --post also posts it as a PR comment.
# usage: tools/pr-review/review_pr.sh <PR number> [--post]
set -euo pipefail
pr="${1:?usage: review_pr.sh <PR number> [--post]}"
root="$(git rev-parse --show-toplevel)"
cd "$root"
out="_temp/pr-review/pr$pr.md"
mkdir -p "$(dirname "$out")"

prompt="You are reviewing pull request #$pr of this repository.

Rules: read every *.md file in docs/review-rules/ except README.md and files starting
with '_'. Each has YAML front matter: skip it if 'enabled: false'; apply it only to
changed files matching 'applies_to' (comma-separated globs, all files when absent); use
its 'severity' (must-fix / should-fix / nit). Also apply any extra checks listed under a
'## Review focus' heading in the PR description ('gh pr view $pr'). Review against these
rules plus real bugs, nothing else. Look at the changed lines ('gh pr diff $pr') and read
the surrounding files for context. Don't comment on unchanged code unless the change
breaks it. Don't praise.

Reply with ONLY the review, exactly in this shape:

## PR review
Verdict: CHANGES REQUESTED | APPROVED
- [ ] **Must fix** [rule-file-name or bug] \`path/File.cs:123\` — problem — fix
- [ ] **Should fix** ...
- [ ] **Nit** ...

Use 'Verdict: APPROVED' with no list when nothing needs changing."

echo "Reviewing PR #$pr locally (this takes a few minutes)..." >&2
claude -p "$prompt" \
  --allowedTools "Read,Grep,Glob,Bash(gh pr diff:*),Bash(gh pr view:*)" \
  --output-format text > "$out"
echo "Saved $out" >&2

if [ "${2:-}" = "--post" ]; then
  { cat "$out"; printf '\n\n_Local review (tools/pr-review/review_pr.sh)._\n'; } | gh pr comment "$pr" --body-file -
  echo "Posted to PR #$pr" >&2
fi
cat "$out"
