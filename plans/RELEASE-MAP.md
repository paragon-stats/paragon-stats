# Release map — two tracks: bootstrap milestones and product versions

Versioning is **Release Please** driven: the version is computed from Conventional
Commits, so **only `feat:`/`fix:` bump it** — `chore:`/`ci:`/`docs:` do not. That
means the entire bootstrap (M1–M5, all chores) produces **no product release**; the
product version stays at `0.0.0` until the first **feature** lands. So bootstrap
progress and product versions are two separate tracks. MinVer stamps the .NET/AOT
binary from whatever tag exists. See [`10-release-automation.md`](10-release-automation.md).

## Track A — Bootstrap milestones (repo readiness, *not* product versions)

These are tracked by milestone completion. They emit no product semver (optionally a
single `v0.0.1` "repo ready" tag at the end of M5). Done when the repo can build,
test, lint, release, and protect itself.

| Milestone                     | Source tasks                                                                                   | Workstream                                                                               |
| ----------------------------- | ---------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| **M1 — Scaffold**             | [`01`](01-dotnet-projects.md), [`03`](03-repo-hygiene.md), [`06`](06-first-push.md)            | .NET solution + repo hygiene; scaffold committed/pushed ✅                                |
| **M2 — Quality gates**        | [`02`](02-build-config.md), [`05`](05-precommit-hooks.md), [`08`](08-linting-style-guides.md)  | build config + analyzers; pre-commit framework; polyglot linting + style guides + CodeQL |
| **M3 — Automation & release** | [`04`](04-github-meta.md), [`09`](09-issue-pr-automation.md), [`10`](10-release-automation.md) | CI; full issue/PR automation; Release Please + MinVer                                     |
| **M4 — Protected trunk**      | [`07`](07-branch-protection.md)                                                                | branch ruleset; required checks: `build`, lint, commitlint, **CodeQL**                    |
| **M5 — Dev plugins**          | [`11`](11-plugins-monorepo.md)                                                                 | private plugins monorepo (Karpathy) + ponytail; wire into this repo                       |

## Track B — Product release roadmap (capability-driven)

Begins once the parsing engine starts (first `feat:`). **All `0.x` releases are
flagged as GitHub pre-releases**; `1.0.0` is the first non-pre-release. The
maintainer is Windows-only; GUI was deferred but lands inside Phase 1 here.

| Version            | Stage                  | Release flag | Capability gate                                                              |
| ------------------ | ---------------------- | ------------ | --------------------------------------------------------------------------- |
| `0.1.0`–`0.4.x`    | **Alpha**              | pre-release  | Parsing engine slices: read CoH logs, parse events, first stats. CLI forms. |
| **`0.5.0`**        | **CLI**                | pre-release  | CLI feature-complete: full core stat set computed + rendered. Format may still change. |
| `0.6`–`0.7`        | **Beta (CLI hardening)** | pre-release | Real-world log coverage, AOT perf, edge cases; freeze the output format.   |
| **`0.8.0`**        | **GUI**                | pre-release  | GUI lands on top of the stable Core/CLI.                                     |
| `0.9.x`            | **Release candidate**  | pre-release  | Integrate CLI + GUI, polish, docs.                                          |
| **`1.0.0`**        | **Stable**             | release      | First supported release = **CLI + GUI**, frozen format, signed AOT binary.  |
| **`2.0.0`**        | **Major (future)**     | release      | Breaking stats/log-format overhaul or major new capability beyond v1.       |

> Milestones for the product track: **v0.5 — CLI**, **v0.8 — GUI**, **v1.0 — Stable
> (CLI + GUI)**. v2.0 is intentionally unscoped for now.
>
> The GitHub repo already exists, is public, and the plan files are pushed. M1
> repo-creation/first-push is done; the scaffold is committed on top.

## Issues (one per task/workstream)

Created via `gh` after milestones exist. Each gets `area/*` + `type/*` labels and a
milestone. Closed by the PR that lands the work (`Closes #N`).

| #   | Title                                                                                          | Milestone | Labels                    |
| --- | ---------------------------------------------------------------------------------------------- | --------- | ------------------------- |
| —   | Scaffold .NET 10 solution (Core, Cli, Tests)                                                   | M1        | `area/build` `type/chore` |
| —   | Add repo hygiene (LICENSE, README, CONTRIBUTING, gitignore, gitattributes, Makefile, SECURITY) | M1        | `area/docs` `type/chore`  |
| —   | Add Directory.Build.props, CPM, analyzers, .editorconfig, AOT                                  | M2        | `area/build` `type/chore` |
| —   | Add pre-commit framework (replaces Husky.Net)                                                  | M2        | `area/ci` `type/chore`    |
| —   | Add polyglot linting, style-guide docs, and CodeQL                                             | M2        | `area/ci` `type/chore`    |
| —   | Add CI build workflow, CODEOWNERS, Dependabot                                                  | M3        | `area/ci` `type/chore`    |
| —   | Add full issue/PR automation (templates, labeler, triage, projects)                            | M3        | `area/ci` `type/chore`    |
| —   | Add SemVer release mechanism (Release Please + MinVer)                                         | M3        | `area/build` `type/feat`  |
| —   | Apply main branch protection ruleset (+ CodeQL required)                                       | M4        | `area/ci` `type/chore`    |
| —   | Stand up private plugins monorepo + wire ponytail/karpathy                                     | M5        | `area/ci` `type/feat`     |

## How the loop closes (reduced manual effort)

```text
plan task ──▶ GitHub issue (milestone-assigned)
                 │
                 ▼
          branch + PR ("Closes #N")
                 │  auto-labeled, added to Project board,
                 │  required checks: build, lint, commitlint, CodeQL
                 ▼
          merge ──▶ issue auto-closes
                 │
                 ▼
   Release Please release PR ──▶ merge ──▶ tag vX.Y.Z + GitHub Release + CHANGELOG
                 │
                 ▼
   MinVer stamps the next build/AOT binary with X.Y.Z
```
