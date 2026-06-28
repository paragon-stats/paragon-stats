# Release map -- two tracks: bootstrap milestones and product versions

Versioning is **Release Please** driven: the version is computed from Conventional Commits.
Pre-1.0, **`feat:` -> minor, `fix:` -> patch** (each feature is its own minor); `chore:` /
`ci:` / `docs:` do not bump. So the entire bootstrap (all chores) produced **no product
release** -- the product line starts at the first feature. MinVer stamps the .NET/AOT binary
from the tag. Full mechanics and diagrams: [`../docs/release-strategy.md`](../docs/release-strategy.md).

## Track A -- Bootstrap milestones (repo readiness, _not_ product versions)

Tracked by milestone completion; they emit no product semver (optionally a single `v0.0.1`
"repo ready" tag at the end of Dev plugins). Done when the repo can build, test, lint,
release, and protect itself.

| Milestone | Source tasks | Workstream |
| --- | --- | --- |
| **Scaffold** | `01`, `03`, `06` | .NET solution + repo hygiene ✅ |
| **Quality gates** | `02`, `05`, `08` | build config + analyzers; pre-commit; linting + CodeQL ✅ |
| **Automation & release** | `04`, `09`, `10` | CI; issue/PR automation; Release Please + MinVer ✅ |
| **Protected trunk** | `07` | branch ruleset; required checks ✅ |
| **Dev plugins** | `11` | private plugins (ponytail/karpathy/techdocs); wire into this repo |

## Track B -- Product release roadmap (feature -> release)

**One feature = one `feat:` PR = one minor.** Bugfixes are `fix:` patches. Deliberate
consolidation releases (the CLI cut, beta, RCs, `1.0.0`) carry no new feature and are cut
with a `Release-As:` footer. Pre-1.0 the minor is a feature odometer, not a compatibility
contract (SemVer major-zero). Cross-platform native AOT (win-x64 + linux-x64).

The canonical features-to-releases **version ladder** lives in
[`../docs/release-strategy.md`](../docs/release-strategy.md#the-ladder-features-to-releases).
The 58 Core/CLI + 21 GUI features are inventoried in [`FEATURE-MAP.md`](FEATURE-MAP.md); the
three phase milestones (**CLI / GUI / Stable**) group the feature issues, and each feature
issue's `feat:` PR cuts its minor.

## How the loop closes

A planned task becomes a milestone-assigned issue, then a branch + PR (`Closes #N`), then a
squash-merge that Release Please turns into a tagged release. The full pipeline -- branch ->
PR -> squash -> Release Please -> tag -> MinVer -> AOT publish -- with diagrams is in
[`../docs/release-strategy.md`](../docs/release-strategy.md).
