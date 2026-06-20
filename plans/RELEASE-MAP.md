# Release map — plan tasks → milestones → SemVer → issues

Maps the bootstrap tasks onto GitHub **Milestones** and **SemVer version tags**.
Versioning is **Release Please** driven (Conventional Commits → `major.minor.patch`);
MinVer stamps the .NET/AOT binary from the tag. Pre-1.0, breaking changes bump the
minor. See [`10-release-automation.md`](10-release-automation.md).

## Milestones

| Milestone                     | Source tasks                                                                                   | Workstream                                                                               | Completes at tag                      |
| ----------------------------- | ---------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- | ------------------------------------- |
| **M1 — Scaffold**             | [`01`](01-dotnet-projects.md), [`03`](03-repo-hygiene.md), [`06`](06-first-push.md)            | .NET solution + repo hygiene; scaffold committed/pushed                                  | `v0.1.0`                              |
| **M2 — Quality gates**        | [`02`](02-build-config.md), [`05`](05-precommit-hooks.md), [`08`](08-linting-style-guides.md)  | build config + analyzers; pre-commit framework; polyglot linting + style guides + CodeQL | `v0.2.0`                              |
| **M3 — Automation & release** | [`04`](04-github-meta.md), [`09`](09-issue-pr-automation.md), [`10`](10-release-automation.md) | CI; full issue/PR automation; Release Please + MinVer                                    | `v0.3.0`                              |
| **M4 — Protected trunk**      | [`07`](07-branch-protection.md)                                                                | branch ruleset; required checks: `build`, lint, commitlint, **CodeQL**                   | `v0.4.0`                              |
| **M5 — Dev plugins**          | [`11`](11-plugins-monorepo.md)                                                                 | private plugins monorepo (Karpathy) + ponytail; wire into this repo                      | monorepo `karpathy-minimalism-v0.1.0` |
| **M6 — Parsing engine**       | _future_                                                                                       | Core parsing engine + CLI (real Phase-1 work)                                            | `v1.0.0`                              |

> The GitHub repo already exists, is public, and the plan files are pushed. M1
> repo-creation/first-push is therefore mostly done; the scaffold is committed on top.

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

```
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
