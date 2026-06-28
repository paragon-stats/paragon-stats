# Bootstrap plan

Task files for bootstrapping the paragon-stats repo. Read [`00-context.md`](00-context.md)
first, then work the task files in numeric order (01 → 11); the lists below group them by milestone.

## Tasks

Grouped into bootstrap milestones (Track A — no product version). Product versions
(v0.1 CLI, GUI, v1.0 stable) are a separate track — see [`RELEASE-MAP.md`](RELEASE-MAP.md).

### Scaffold

- [`01-dotnet-projects.md`](01-dotnet-projects.md) — Solution + Core/Cli/Tests via `dotnet new`
- [`03-repo-hygiene.md`](03-repo-hygiene.md) — `LICENSE`, `README`, `CONTRIBUTING`, `.gitignore`, `.gitattributes`, `Makefile`, `SECURITY`
- [`06-first-push.md`](06-first-push.md) — Commit + push the scaffold (repo already exists)

### Quality gates

- [`02-build-config.md`](02-build-config.md) — `Directory.Build.props`, CPM, analyzers, `.editorconfig`, AOT
- [`05-precommit-hooks.md`](05-precommit-hooks.md) — Git hooks (Husky.Net)
- [`08-linting-style-guides.md`](08-linting-style-guides.md) — Polyglot linters, style-guide docs, **CodeQL**

### Automation & release

- [`04-github-meta.md`](04-github-meta.md) — CI build workflow, `CODEOWNERS`, Dependabot
- [`09-issue-pr-automation.md`](09-issue-pr-automation.md) — Issue/PR templates, auto-labeler, triage, Projects
- [`10-release-automation.md`](10-release-automation.md) — Release Please + MinVer (SemVer)

### Protected trunk

- [`07-branch-protection.md`](07-branch-protection.md) — Branch ruleset; required checks incl. CodeQL

### Dev plugins

- [`11-plugins-monorepo.md`](11-plugins-monorepo.md) — Pinned dev plugins (ponytail/techdocs/karpathy/thinking-tools), wired into `.claude/settings.json`

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
