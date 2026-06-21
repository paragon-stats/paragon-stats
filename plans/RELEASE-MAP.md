# Release map — two tracks: bootstrap milestones and product versions

Versioning is **Release Please** driven: the version is computed from Conventional
Commits, so **only `feat:`/`fix:` bump it** — `chore:`/`ci:`/`docs:` do not. That
means the entire bootstrap (all chores) produces **no product release**; the product
version stays at `0.0.0` until the first **feature** lands. So bootstrap progress and
product versions are two separate tracks. MinVer stamps the .NET/AOT binary from
whatever tag exists. See [`10-release-automation.md`](10-release-automation.md).

## Track A — Bootstrap milestones (repo readiness, *not* product versions)

Tracked by milestone completion; they emit no product semver (optionally a single
`v0.0.1` "repo ready" tag at the end of Dev plugins). Done when the repo can build,
test, lint, release, and protect itself.

| Milestone | Source tasks | Workstream |
| --- | --- | --- |
| **Scaffold** | `01`, `03`, `06` | .NET solution + repo hygiene; scaffold committed/pushed ✅ |
| **Quality gates** | `02`, `05`, `08` | build config + analyzers; pre-commit; polyglot linting + style guides + CodeQL |
| **Automation & release** | `04`, `09`, `10` | CI; full issue/PR automation; Release Please + MinVer |
| **Protected trunk** | `07` | branch ruleset; required checks: `build`, lint, commitlint, **CodeQL** |
| **Dev plugins** | `11` | private plugins monorepo (Karpathy) + ponytail; wire into this repo |

## Track B — Product release roadmap (capability-driven)

Begins once the parsing engine starts (first `feat:`). **All `0.x` releases are GitHub
pre-releases**; `1.0.0` is the first non-pre-release. The maintainer develops on
**Debian 13**; the product ships **cross-platform (win-x64 + linux-x64)**. GUI was
deferred but lands inside Phase 1.

| Version | Stage | Flag | Capability gate |
| --- | --- | --- | --- |
| `0.1.0` | **CLI** | pre-release | First usable CLI: parse CoH logs + initial stats. |
| `0.2`–`0.x` | **Features** | pre-release | Incremental features — one minor per feat; milestones added as mapped. |
| `0.x` | **GUI** | pre-release | GUI on the stable Core/CLI; exact version emerges (un-pegged). |
| `0.x` | **Release candidate** | pre-release | Integrate CLI + GUI, polish, docs. |
| `1.0.0` | **Stable** | release | First supported release = CLI + GUI; frozen format; signed AOT (win-x64 + linux-x64). |
| `2.0.0` | **Major (future)** | release | Breaking overhaul / beyond v1. |

> Product milestones: **v0.1 — CLI**, **GUI** (un-pegged), **v1.0 — Stable (CLI + GUI)**.
> Feature milestones (`v0.2`…) are added as features get mapped; `v2.0` is unscoped.

## Issues (one per task/workstream)

Each gets `area/*` + `type/*` labels and a milestone. Closed by the PR that lands the
work (`Closes #N`). The live list is on GitHub — this is the original seed mapping.

| Title | Milestone |
| --- | --- |
| Scaffold .NET 10 solution (Core, Cli, Tests) | Scaffold |
| Add repo hygiene (LICENSE, README, …) | Scaffold |
| Add Directory.Build.props, CPM, analyzers, .editorconfig, AOT | Quality gates |
| Add pre-commit framework | Quality gates |
| Add polyglot linting, style-guide docs, and CodeQL | Quality gates |
| Add CI build workflow, CODEOWNERS, Dependabot | Automation & release |
| Add full issue/PR automation | Automation & release |
| Add SemVer release mechanism (Release Please + MinVer) | Automation & release |
| Apply main branch protection ruleset (+ CodeQL required) | Protected trunk |
| Stand up private plugins monorepo + wire plugins | Dev plugins |

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
