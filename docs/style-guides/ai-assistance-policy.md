# AI-assistance policy

paragon-stats is built with AI assistance, and contributors are welcome to use it.
This policy is about what gets **committed**, not how you work.

## No committed AI meta-docs

Agent instruction files are intentionally **kept out of the repo** — their absence
is policy, not an oversight:

- `CLAUDE.md`, `AGENTS.md`, `.cursorrules`, `copilot-instructions.md`,
  `.github/copilot-instructions.md`, `.claude/rules/`, and similar.

Why:

- They're **tool-specific** — Claude's rules aren't Cursor's aren't Copilot's.
  Committing one maintainer's tooling imposes it on everyone else.
- They **drift** from the real sources of truth — the guides in this directory,
  the analyzers, and CI. Two copies of the rules is one too many.
- Docs here are terse by design (Sonarr-style); agent meta-docs are sprawl.

## Bring your own

Use whatever AI tooling you like locally. Keep its config out of git — add it to
`.git/info/exclude` so it's never staged. Your local `CLAUDE.md` or `.cursorrules`
is yours; it just doesn't get committed.

## One bar for all code

Every change — AI-assisted or hand-written — clears the same review and CI gate
(see [review-workflow.md](review-workflow.md)). Origin doesn't move the bar.
