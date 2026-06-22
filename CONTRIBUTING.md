# Contributing

Thanks for your interest in paragon-stats.

## Setup

Requires the .NET 10 SDK (Windows or Linux x64) and Python 3.12+ (for the pre-commit hooks).
After cloning (`python3` on Linux/macOS, `py -3` on Windows):

```text
python3 -m pip install -r requirements-dev.txt
pre-commit install --install-hooks
```

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
tooling: the MCP server (`.mcp.json`) needs Docker + `export SONARQUBE_TOKEN=<token>`;
SonarLint connected mode (`.vscode/settings.json`) needs an IDE connection with id
`paragon-stats`.

## House rules

**Clean-room.** Do not paste code from the original HeroStats source or the
`herostats-svn-archive`. That archive is for understanding *concepts* (log formats,
which stats were computed) only.

**Dependencies.** Apache-2.0-compatible licenses only (MIT, BSD, ISC, Apache-2.0,
CC0, …). No GPL, AGPL, MPL, or source-available non-OSI licenses. Verify the SPDX
identifier against the package's actual `LICENSE`.
