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
