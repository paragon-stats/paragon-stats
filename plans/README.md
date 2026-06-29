# Bootstrap plan (historical record)

Task files that bootstrapped the paragon-stats repo. **Kept as-is** for provenance;
these are completed-work records, not the live plan. The authoritative product plan
is the GitHub Project + [`../docs/PRD.md`](../docs/PRD.md).

## Tasks

Grouped into the (now closed) bootstrap milestones.

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

- [`11-plugins.md`](11-plugins.md) — Pinned dev plugins, wired into `.claude/settings.json`

## Done

The bootstrap is complete; this directory is retained as the historical record
of the setup. The forward-looking plan lives in the GitHub Project and
[`../docs/PRD.md`](../docs/PRD.md).
