# Contributing

Thanks for your interest in paragon-stats.

## Setup

Requires the .NET 10 SDK (Windows or Linux x64). After cloning, run the bootstrap script —
it restores the .NET local tools, installs the Husky.Net git hooks, and verifies the
toolchain (signed commits are required):

```text
dotnet run scripts/dev/bootstrap.cs
```

`dotnet run scripts/dev/bootstrap.cs -- --verify` confirms the environment is ready.

## Build and test

```text
dotnet build
dotnet test
```

Husky.Net git hooks run `dotnet format` and the Conventional-Commit check locally;
markdown/YAML/Actions linting and secret scanning run in CI.

## Code style

Enforced by `.editorconfig` and analyzers (StyleCop, Meziantou). Warnings are
treated as errors — fix issues at the source rather than suppressing them.

## Commits and pull requests

- Branch from `main`; open PRs against `main`.
- **Signed commits are required.**
- Use [Conventional Commits](https://www.conventionalcommits.org)
  (`feat:`, `fix:`, `chore:`, `docs:`, ...).
- All status checks must pass before merge.
- Every PR clears two reviews (correctness + over-engineering) before merge —
  see the [review workflow](docs/style-guides/review-workflow.md).

## Code quality (SonarQube)

CI scans every push/PR via SonarQube Cloud (`SONAR_TOKEN` secret). Optional local
tooling: the MCP server (`.mcp.json`) runs `scripts/mcp/run_sonarqube_mcp.py` (needs
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

**AI assistance.** Welcome — but AI meta-docs aren't committed; see the
[AI-assistance policy](docs/style-guides/ai-assistance-policy.md).
