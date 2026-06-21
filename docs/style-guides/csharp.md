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

## AOT safety (Core + Cli)

`ParagonStats.Cli` publishes with **native AOT** (`win-x64` and `linux-x64`). Keep
`ParagonStats.Core` AOT-compatible:

- No unbounded reflection, `dynamic`, or runtime code generation.
- Prefer source generators / explicit code over reflection-based serialization.
- Anything that trims poorly belongs behind a clearly documented boundary.

## Pre-1.0 suppressions

A few documentation/header rules are off until v1.0 — see `.editorconfig` and the
tracking issue **#11**. Don't add new suppressions without updating that issue.
