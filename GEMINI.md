# xr-racing (Gemini CLI)

Gemini CLI reads this file. All project rules — reference project, CI/CD & agent
guardrails, and the aider delegation workflow — live in `CLAUDE.md` so there is a
single source of truth shared with Claude Code. Read and follow it in full; where
it says "director", that's you.

@./CLAUDE.md

## Aider Settings & Flags
- When invoking aider, always include `--no-check-update --no-show-release-notes` to suppress update checks and release notes prompts (`https://aider.chat/HISTORY.html#release-notes`).
- Global user config is maintained in `~/.aider.conf.yml` (`check-update: false`, `show-release-notes: false`).
