# Release map — two tracks: bootstrap milestones and product versions

Versioning is **Release Please** driven: the version is computed from Conventional
Commits, so **only `feat:`/`fix:` bump it** — `chore:`/`ci:`/`docs:` do not. That
means the entire bootstrap (all chores) produces **no product release**; the product
version stays at `0.0.0` until the first **feature** lands. So bootstrap progress and
product versions are two separate tracks. MinVer stamps the .NET/AOT binary from
whatever tag exists. See [`10-release-automation.md`](10-release-automation.md).

## Track A — Bootstrap milestones (repo readiness, _not_ product versions)

Tracked by milestone completion; they emit no product semver (optionally a single
`v0.0.1` "repo ready" tag at the end of Dev plugins). Done when the repo can build,
test, lint, release, and protect itself.

| Milestone | Source tasks | Workstream |
| ------------------------ | ---------------- | ------------------------------------------------------------------------------ |
| **Scaffold** | `01`, `03`, `06` | .NET solution + repo hygiene; scaffold committed/pushed ✅ |
| **Quality gates** | `02`, `05`, `08` | build config + analyzers; pre-commit; polyglot linting + style guides + CodeQL |
| **Automation & release** | `04`, `09`, `10` | CI; full issue/PR automation; Release Please + MinVer |
| **Protected trunk** | `07` | branch ruleset; required checks: `build`, lint, commitlint, **CodeQL** |
| **Dev plugins** | `11` | private plugins monorepo (Karpathy) + ponytail; wire into this repo |

## Track B — Product release roadmap (capability-driven)

**Start-at-patch** (`bump-patch-for-minor-pre-major: true`): pre-1.0, **every
`feat:`/`fix:` bumps patch** (`0.0.1`, `0.0.2`, …) and **only a breaking change
auto-bumps the minor**. The named milestones below are **deliberate promotions**
(a `Release-As:` footer when you reach them), not auto-computed. `0.x` is the
unstable pre-1.0 line; `1.0.0` is the first stable release. Baseline anchored by
the `v0.0.0` tag. Dev on Debian 13; ships cross-platform (win-x64 + linux-x64).

Product scope is mapped in [`FEATURE-MAP.md`](FEATURE-MAP.md): **58 Core/CLI
features** (the pre-`0.1.0` alpha body) plus **21 GUI features** (later milestone),
reimplemented clean-room from the GPLv2 original (concept only).

| Version | Stage | How it's cut |
| --------------- | ---------------------- | ----------------------------------------------------------- |
| `0.0.1`–`0.0.x` | **Alpha** | every `feat:`/`fix:` (patch) — features + fixes accrue here |
| `0.1.0` | **CLI** | deliberate `Release-As: 0.1.0` when the CLI is usable |
| `0.x` | **GUI** | deliberate promotion; version un-pegged |
| `0.x` | **Release candidate** | integrate CLI + GUI, polish, docs |
| `1.0.0` | **Stable (CLI + GUI)** | deliberate `Release-As: 1.0.0`; frozen format; signed AOT |
| `2.0.0` | **Major (future)** | breaking change post-1.0 |

> Product milestones (**CLI / GUI / Stable**) are **deliberate** version promotions —
> pre-1.0 routine changes stay in `0.0.x`; you bump to `0.1.0`/`1.0.0` via `Release-As`.

## Issues (one per task/workstream)

Each gets `area/*` + `type/*` labels and a milestone. Closed by the PR that lands the
work (`Closes #N`). The live list is on GitHub — this is the original seed mapping.

| Title | Milestone |
| ------------------------------------------------------------- | -------------------- |
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
