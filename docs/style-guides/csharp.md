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
declarations, `out var` and pattern designations must be at least three
characters, measured after leading underscores (`_id` violates, `_` does not).

Allowed short names, and only these: `_`, `xp`, `inf`, `i`, `j`, `k`. `xp` and
`inf` are the game's own vocabulary and already appear as `xpRate`/`infRate`;
`ex` is deliberately **not** on the list, so a caught exception is named
`exception`. Type and member names are out of scope.

Enforcement is **`PS0001`**, the analyzer in `src/ParagonStats.Analyzers`. It
fails `dotnet build` like every other rule here, so it bites locally and in CI
rather than after a push.

Nothing off the shelf can do this, which is why the rule is written rather than
configured. StyleCop's SA13xx family, Meziantou's rules and
`dotnet_naming_style` are all casing and affixes, with no length or regex
capability at any severity. **SonarCloud has no C# rule for it either**: the
naming rule with a configurable `format` (`S117`) exists for other languages but
not for C#, whose only parameterised naming rules are `S2342` (enums) and
`S6669` (logger fields) — verified against the rules API, not assumed. Do not go
looking for a quality-profile setting; there isn't one.

Sonar still sees violations without any profile work: the scanner imports
third-party Roslyn diagnostics as `external_roslyn:*` issues. In practice the
build fails first, because warnings are errors here.

The rule mirrors the Python one the homelab repos already enforce
(`scripts/linting/check_short_identifier_names.py`), so the convention reads the
same across languages. Tracking: **#234**.

## AOT safety (Core + Cli)

`ParagonStats.Cli` publishes with **native AOT** (`win-x64` and `linux-x64`). Keep
`ParagonStats.Core` AOT-compatible:

- No unbounded reflection, `dynamic`, or runtime code generation.
- Prefer source generators / explicit code over reflection-based serialization.
- Anything that trims poorly belongs behind a clearly documented boundary.

## Pre-1.0 suppressions

A few documentation/header rules are off until v1.0 — see `.editorconfig` and the
tracking issue **#11**. Don't add new suppressions without updating that issue.
