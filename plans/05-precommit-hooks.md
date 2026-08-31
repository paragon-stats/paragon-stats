# 05 — Git hooks (Husky.Net)

> Supersedes the Python `pre-commit` plan. For a .NET repo the right architecture is a
> .NET-native hook manager: [Husky.Net](https://alirezanet.github.io/Husky.Net/). Local
> hooks need only the .NET SDK contributors already have — no Python, no `.venv`, no
> `python`/`py`/`python3` ambiguity. Polyglot linting (markdown/yaml/actions) and secret
> scanning move to CI (Super-Linter, `08` / #27), keeping the local hooks lean.
> Divergence since: the pre-commit group now also runs the full Super-Linter image locally
> (see `.husky/task-runner.json`), trading lean hooks for local/CI lint parity.

## Goal

Catch formatting and commit-message issues before commits leave the machine, using
.NET-native tooling; let CI own the polyglot/secret checks.

## Prerequisites

- .NET 10 SDK on PATH (already required to build).

## Steps

1. **Pin Husky.Net** as a local tool: `dotnet tool install husky` (recorded in
   `.config/dotnet-tools.json`).
2. **Install the hooks**: `dotnet husky install` (points `core.hooksPath` at `.husky/`).
3. **`.husky/task-runner.json`** — a `pre-commit` group task running
   `dotnet format --verify-no-changes --severity error`.
4. **`.husky/pre-commit`** → `dotnet husky run --group pre-commit`.
5. **`.husky/commit-msg`** → `bash scripts/git/check-commit-message.sh` — the shared
   Conventional-Commits validator vendored from `paragon-stats/github-actions`; the
   allowed types live in `scripts/git/commit-types.txt`, vendored from the same pin.
   (Originally built here in C#; extracted under #198.)
6. **Bootstrap** (`scripts/dev/bootstrap.cs`, C# file-based): `dotnet tool restore` +
   `dotnet husky install` + toolchain checks; **signed commits are required → hard
   error**. Documented in `CONTRIBUTING.md` as `dotnet run scripts/dev/bootstrap.cs`.

`dotnet build -warnaserror` and the polyglot/secret linters are intentionally CI-only.

## Transition (staged cutover from Python pre-commit)

Local hooks cut over to Husky.Net immediately; CI keeps the Python `pre-commit`
workflow for markdown/yaml/actions/secret/hygiene linting until **Super-Linter (#27)**
takes it over — then `.pre-commit-config.yaml` + `pre-commit.yml` are removed and Python
is gone entirely. No coverage gap at any point.

## Acceptance

- A formatting violation blocks the commit; `dotnet format` fixes it.
- A non-Conventional commit message is rejected by the `commit-msg` hook.
- `dotnet run scripts/dev/bootstrap.cs --verify` reports the environment ready.
