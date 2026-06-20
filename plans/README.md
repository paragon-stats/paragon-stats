# Bootstrap plan

Task files for bootstrapping the paragon-stats repo. Read [`00-context.md`](00-context.md)
first, then execute the numbered tasks in order.

## Tasks

Grouped into bootstrap milestones (Track A — no product version). Product versions
(v0.5 CLI, v0.8 GUI, v1.0 stable) are a separate track — see [`RELEASE-MAP.md`](RELEASE-MAP.md).

### M1 — Scaffold

1. [`01-dotnet-projects.md`](01-dotnet-projects.md) — Solution + Core/Cli/Tests via `dotnet new`
3. [`03-repo-hygiene.md`](03-repo-hygiene.md) — `LICENSE`, `README`, `CONTRIBUTING`, `.gitignore`, `.gitattributes`, `Makefile`, `SECURITY`
6. [`06-first-push.md`](06-first-push.md) — Commit + push the scaffold (repo already exists)

### M2 — Quality gates

2. [`02-build-config.md`](02-build-config.md) — `Directory.Build.props`, CPM, analyzers, `.editorconfig`, AOT
5. [`05-precommit-hooks.md`](05-precommit-hooks.md) — Pre-commit framework (Python `pre-commit`)
8. [`08-linting-style-guides.md`](08-linting-style-guides.md) — Polyglot linters, style-guide docs, **CodeQL**

### M3 — Automation & release

4. [`04-github-meta.md`](04-github-meta.md) — CI build workflow, `CODEOWNERS`, Dependabot
9. [`09-issue-pr-automation.md`](09-issue-pr-automation.md) — Issue/PR templates, auto-labeler, triage, Projects
10. [`10-release-automation.md`](10-release-automation.md) — Release Please + MinVer (SemVer)

### M4 — Protected trunk

7. [`07-branch-protection.md`](07-branch-protection.md) — Branch ruleset; required checks incl. CodeQL

### M5 — Dev plugins

11. [`11-plugins-monorepo.md`](11-plugins-monorepo.md) — Private plugins monorepo (Karpathy) + ponytail wiring

## Usage with Claude Code

From inside the `paragon-stats` repo:

```text
claude
> Read plan/00-context.md, then execute plan/01-dotnet-projects.md.
> Stop after each task and let me review the diff before moving on.
```

Each task is self-contained. After completing one, commit the result and
move to the next file.

## Done

Either delete this directory after the bootstrap is complete, or keep it as
historical record of the setup. Your call.
