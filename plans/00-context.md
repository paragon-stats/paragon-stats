# 00 — Project context

Read this first. It captures the decisions made in the planning conversation
that the rest of the task files assume.

## What this is

**paragon-stats** is a clean-room reimplementation of the *concept* behind
HeroStats — a chat-log-driven stats engine for *City of Heroes* (currently
licensed to Homecoming Servers, LLC).

- **Clean-room** means no code is copied from the original HeroStats source.
- The original source lives as a read-only reference at
  `pkloehn1/herostats-svn-archive` (a separate, private repo — not in scope
  for this bootstrap).
- The archive is for *concept* understanding only (log formats, what stats
  the original computed). Do not paste code from it into paragon-stats.

## Phase 1 scope

- **Language**: C# / .NET 10 LTS
- **Platform**: cross-platform — ships **win-x64 + linux-x64** native AOT
  binaries (CoH players are mostly on Windows; Linux/Proton covered too). The
  maintainer develops on **Debian 13 (Trixie)**. Native AOT cannot cross-compile,
  so each RID is published on its matching CI runner.
- **Architecture**: library + CLI + tests
  - `src/ParagonStats.Core` — parsing engine and domain model
  - `src/ParagonStats.Cli` — entry point, native AOT publish
  - `tests/ParagonStats.Core.Tests` — xUnit v3
- **GUI**: deferred to later in Phase 1
- **Code signing**: deferred until post-GUI; will apply to SignPath
  Foundation OSS program when adoption justifies it

## Style guide

- **License**: Apache-2.0
- **Documentation**: terse. Follow the Sonarr repo's pattern — short README,
  link out to deeper docs. No committed AI-agent meta-docs (see
  [ai-assistance-policy.md](../docs/style-guides/ai-assistance-policy.md)).
- **Code style**: Microsoft .NET conventions via `.editorconfig`. Start lean
  from `dotnet new editorconfig`; only add overrides as you encounter
  specific needs.
- **Commit messages**: Conventional Commits where reasonable (`feat:`,
  `fix:`, `chore:`, `docs:`, etc.)
- **Branching**: trunk-based; PRs to `main` only; signed commits required

## Dependency license posture

When adding NuGet packages or GitHub Actions:

- **Compatible**: MIT, BSD, ISC, 0BSD, Unlicense, Apache-2.0, CC0
- **Avoid**: GPL, AGPL, source-available non-OSI (BSL, SSPL, Elastic, RSAL)
- **Case-by-case**: LGPL (library dependency only), MPL-2.0

Verify the SPDX identifier matches the actual `LICENSE` file in the package
— NuGet metadata is sometimes wrong.

## Repo identity

- **Org**: `paragon-stats`
- **Repo**: `paragon-stats/paragon-stats` (public)
- **Maintainer**: `@pkloehn1`

## Attribution

Credit the original HeroStats authors in the README footer:

> Inspired by HeroStats by `ineffablebob`, `lberger`, `msawczyn`,
> `lpfjones`, `thesteinerd`.

This is community courtesy, not a copyright obligation (paragon-stats is
clean-room).
