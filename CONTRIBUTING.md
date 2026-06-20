# Contributing

Thanks for your interest in paragon-stats.

## Build and test

Requires the .NET 10 SDK (Windows x64).

```
dotnet build
dotnet test
```

Run `dotnet format` before committing; a pre-commit hook enforces this once the
hook framework is in place.

## Code style

Enforced by `.editorconfig` and analyzers (StyleCop, Meziantou). Warnings are
treated as errors — fix issues at the source rather than suppressing them.

## Commits and pull requests

- Branch from `main`; open PRs against `main`.
- **Signed commits are required.**
- Use [Conventional Commits](https://www.conventionalcommits.org)
  (`feat:`, `fix:`, `chore:`, `docs:`, ...).
- All status checks must pass before merge.

## House rules

**Clean-room.** Do not paste code from the original HeroStats source or the
`herostats-svn-archive`. That archive is for understanding *concepts* (log formats,
which stats were computed) only.

**Dependencies.** Apache-2.0-compatible licenses only (MIT, BSD, ISC, Apache-2.0,
CC0, …). No GPL, AGPL, MPL, or source-available non-OSI licenses. Verify the SPDX
identifier against the package's actual `LICENSE`.
