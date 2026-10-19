# xr-racing (Gemini CLI)

Gemini CLI reads this file. All xr-racing project rules — reference project and
CI/CD & agent guardrails — live in `CLAUDE.md` so there is a single source of
truth shared with Claude Code. Read and follow it in full. The aider workflow and
Unity rules are global (`~/.ai-rules/`, loaded by your global `GEMINI.md`); where
they say "director", that's you.

@./CLAUDE.md

## Aider settings
- Global aider config is maintained in `~/.aider.conf.yml` (`check-update: false`,
  `show-release-notes: false`); the invocation flags are in
  `~/.ai-rules/AIDER_WORKFLOW.md`.
