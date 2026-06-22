# Contributing

Thanks for your interest in paragon-stats.

## Setup

Requires the .NET 10 SDK (Windows or Linux x64) and Python 3.12+. After cloning, run the
bootstrap script — it creates a `.venv`, installs the pinned pre-commit + git hooks,
restores the .NET local tools, and verifies the toolchain (idempotent):

```text
python3 scripts/bootstrap.py        # Windows: py -3 scripts/bootstrap.py
```

`python3 scripts/bootstrap.py --verify` confirms the environment is correct and up to date.

## Build and test

```text
dotnet build
dotnet test
```

`dotnet format`, markdown/YAML/Actions linting, secret scanning, and Conventional
Commit checks all run automatically via the pre-commit hooks (and again in CI).

## Code style

Enforced by `.editorconfig` and analyzers (StyleCop, Meziantou). Warnings are
treated as errors — fix issues at the source rather than suppressing them.

## Commits and pull requests

- Branch from `main`; open PRs against `main`.
- **Signed commits are required.**
- Use [Conventional Commits](https://www.conventionalcommits.org)
  (`feat:`, `fix:`, `chore:`, `docs:`, ...).
- All status checks must pass before merge.

## Code quality (SonarQube)

CI scans every push/PR via SonarQube Cloud (`SONAR_TOKEN` secret). Optional local
tooling: the MCP server (`.mcp.json`) runs `scripts/run_sonarqube_mcp.py` (needs
`python` + Docker; refreshes the image only when the registry digest changes) plus
`export SONARQUBE_TOKEN=<token>`; SonarLint connected mode (`.vscode/settings.json`)
needs an IDE connection with id `paragon-stats`.

## House rules

**Clean-room.** Do not paste code from the original HeroStats source or the
`herostats-svn-archive`. That archive is for understanding *concepts* (log formats,
which stats were computed) only.

**Dependencies.** Apache-2.0-compatible licenses only (MIT, BSD, ISC, Apache-2.0,
CC0, …). No GPL, AGPL, MPL, or source-available non-OSI licenses. Verify the SPDX
identifier against the package's actual `LICENSE`.
