# C# style guide

The enforced source of truth is [`.editorconfig`](../../.editorconfig) plus the
StyleCop and Meziantou analyzers, with **warnings treated as errors**. Run
`dotnet format` before committing. This page documents only what isn't obvious from
the config.

## Conventions

- **Nullable reference types** are enabled everywhere; don't disable them locally.
- Fix analyzer findings **at the source** — do not `#pragma`/suppress without a
  written reason and (for pre-1.0 suppressions) a tracking issue.
- File-scoped namespaces; `using` directives outside the namespace.
- Private fields `_camelCase`; everything public PascalCase (see `.editorconfig`).

## Naming: identifiers are at least three characters

Locals, parameters (lambdas included), `foreach`/`for` variables, `catch`
declarations, `out var`, pattern designations **and fields** (including consts
and event fields) must be at least three characters, measured after leading
underscores: `_id` violates, `_` does not. Types, methods and properties are out
of scope.

Allowed short names, and only these: `_`, `xp`, `inf`, `i`, `j`, `k` — with or
without a leading underscore, so `_xp` is fine. `xp` and `inf` are the game's own
vocabulary and already appear as `xpRate`/`infRate`; `ex` is deliberately **not**
on the list, so a caught exception is named `exception`. The list is code, not
config: extending it means a PR against `ShortIdentifierAnalyzer` and its
`AnalyzerReleases` files.

Enforcement is **`PS0001`**, the analyzer in `src/ParagonStats.Analyzers`. It
fails `dotnet build` like every other rule here, so it bites locally rather than
after a push, and Sonar sees the same violations via `external_roslyn:*` import.

The rule is written rather than configured because nothing off the shelf can
express a name length: StyleCop's SA13xx family, Meziantou's rules and
`dotnet_naming_style` are casing and affixes only, and **SonarCloud has no C#
rule for it either** — `S117` takes a `format` parameter in other languages but
does not exist for C#. Don't go looking for a quality-profile setting. It mirrors
the Python checker the homelab repos enforce
(`scripts/linting/check_short_identifier_names.py`). Tracking: **#234**.

## AOT safety (Core + Cli)

`ParagonStats.Cli` publishes with **native AOT**. The only runtime built and
released today is `win-x64` (`RuntimeIdentifiers` in the csproj, and the single
publish step in `release.yml`). Keep
`ParagonStats.Core` AOT-compatible:

- No unbounded reflection, `dynamic`, or runtime code generation.
- Prefer source generators / explicit code over reflection-based serialization.
- Anything that trims poorly belongs behind a clearly documented boundary.

## Pre-1.0 suppressions

A few documentation/header rules are off until v1.0 — see `.editorconfig` and the
tracking issue **#11**. Don't add new suppressions without updating that issue.
