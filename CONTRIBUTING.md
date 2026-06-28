# Contributing

## Setup

Requires the .NET 10 SDK (Windows or Linux x64). After cloning, run the bootstrap script — it
restores the .NET local tools, installs the Husky.Net hooks, and verifies the toolchain (signed
commits are required):

```text
dotnet run scripts/dev/bootstrap.cs            # add -- --verify to just check the environment
```

## Build and test

```text
dotnet build
dotnet test
```

Husky.Net hooks run `dotnet format`, the commit-message + encoding checks, and the full
Super-Linter image on commit (needs Docker; skipped without it). CI runs the same linters.

## Code style

Enforced by `.editorconfig` and analyzers (StyleCop, Meziantou). Warnings are errors — fix at
the source rather than suppressing.

## Commits and pull requests

- Branch from `main`; open PRs against `main`.
- **Signed commits are required.**
- Use [Conventional Commits](https://www.conventionalcommits.org) (`feat:`, `fix:`, `chore:`,
  `docs:`, ...) — they drive the version; see the [release strategy](docs/release-strategy.md).
- All status checks must pass, and every PR clears two reviews (correctness + over-engineering;
  see the [review workflow](docs/style-guides/review-workflow.md)) before merge.

## Code quality (SonarQube)

CI scans every push/PR via SonarQube Cloud. Optional local tooling — a SonarQube MCP server and
SonarLint connected mode — is wired in `.mcp.json` and `.vscode/settings.json`.

## House rules

**Clean-room.** Do not paste code from the original HeroStats source or the
`herostats-svn-archive`. That archive is for understanding *concepts* (log formats, which stats
were computed) only.

**Dependencies.** Apache-2.0-compatible licenses only (MIT, BSD, ISC, Apache-2.0, CC0, …). No
GPL, AGPL, MPL, or source-available non-OSI licenses. Verify the SPDX identifier against the
package's actual `LICENSE`.

**AI assistance.** Welcome — but AI meta-docs aren't committed; see the
[AI-assistance policy](docs/style-guides/ai-assistance-policy.md).
