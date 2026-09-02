# Contributing

## Setup

Requires the .NET 10 SDK on **Windows x64** — the only platform CI verifies and the only RID
the product ships. The code is portable enough to build elsewhere, but nothing checks that any
more, so treat another dev host as unsupported. After cloning, run the bootstrap script — it
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

Husky.Net hooks run `dotnet format` and the commit-message + encoding checks **on commit**,
and the full Super-Linter image **on push** (needs Docker; skipped without it).

Super-Linter runs at push rather than at commit for a reason worth knowing: with `RUN_LOCAL`
it diffs against `origin/main` using *committed* history, so in a pre-commit hook it never
saw the staged files — it linted the previous commit and reported a pass on code nobody had
checked. At push time `HEAD` contains the commits and the range matches the CI workflow's
exactly. Both read the same [`.github/super-linter.env`](.github/super-linter.env), so the
linter set and the config cannot drift either.

## Code style

Enforced by `.editorconfig` and analyzers (StyleCop, Meziantou). Warnings are errors — fix at
the source rather than suppressing.

## Commits and pull requests

- Branch from `main`; open PRs against `main`.
- **Signed commits are required.**
- Use [Conventional Commits](https://www.conventionalcommits.org) (`feat:`, `fix:`, `chore:`,
  `docs:`, ...) — they drive the version; see [versioning](docs/ROADMAP.md#versioning).
- All status checks must pass, and every PR clears two reviews (correctness + over-engineering;
  see the [review workflow](docs/style-guides/review-workflow.md)) before merge.

## Code quality (SonarQube)

CI scans every push/PR via SonarQube Cloud. Optional local tooling:

- **MCP servers** — `.mcp.json` defines `sonarqube` and `github` (both Docker,
  digest-pinned). Each reads its token from its own gitignored env file, so neither
  server sees the other's secret. Populate them once, values unquoted (`--env-file` keeps quotes literally):
  `echo "SONARQUBE_TOKEN=$(op read 'op://Homelab/SonarQube Cloud claude-code Token/credential')" > .env.sonarqube`
  `echo "GITHUB_PERSONAL_ACCESS_TOKEN=$(op read 'op://Homelab/paragon-stats-mcp/credential')" > .env.github`
- **SonarLint connected mode** — `.vscode/settings.json` binds the project; create an IDE
  connection with id `paragon-stats`.

## House rules

**Clean-room.** Do not paste code from the original HeroStats source or the
`herostats-svn-archive`. That archive is for understanding *concepts* (log formats, which stats
were computed) only.

**Dependencies.** Apache-2.0-compatible licenses only (MIT, BSD, ISC, Apache-2.0, CC0, …). No
GPL, AGPL, MPL, or source-available non-OSI licenses. Verify the SPDX identifier against the
package's actual `LICENSE`.

**AI assistance.** Welcome — but AI meta-docs aren't committed; see the
[AI-assistance policy](docs/style-guides/ai-assistance-policy.md).
