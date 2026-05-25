# Bootstrap plan

Task files for bootstrapping the paragon-stats repo. Read [`00-context.md`](00-context.md)
first, then execute the numbered tasks in order.

## Tasks

1. [`01-dotnet-projects.md`](01-dotnet-projects.md) — Solution + Core/Cli/Tests via `dotnet new`
2. [`02-build-config.md`](02-build-config.md) — `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`
3. [`03-repo-hygiene.md`](03-repo-hygiene.md) — `LICENSE`, `README`, `CONTRIBUTING`, `.gitignore`, `.gitattributes`, `Makefile`
4. [`04-github-meta.md`](04-github-meta.md) — `CODEOWNERS`, Dependabot, build workflow
5. [`05-husky-hooks.md`](05-husky-hooks.md) — Pre-commit hooks via Husky.Net
6. [`06-first-push.md`](06-first-push.md) — Initial commit, push, verify CI green
7. [`07-branch-protection.md`](07-branch-protection.md) — Manual UI checklist for the ruleset

## Usage with Claude Code

From inside the `paragon-stats` repo:

```
claude
> Read plan/00-context.md, then execute plan/01-dotnet-projects.md.
> Stop after each task and let me review the diff before moving on.
```

Each task is self-contained. After completing one, commit the result and
move to the next file.

## Done

Either delete this directory after the bootstrap is complete, or keep it as
historical record of the setup. Your call.
