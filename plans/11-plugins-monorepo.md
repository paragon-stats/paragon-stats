# 11 — Pinned dev plugins (wiring)

## Goal

Control AI-assist sprawl with **pinned, auditable** Claude Code plugins, wired into this
repo's `.claude/settings.json` so every contributor gets a consistent, version-pinned set.
Each plugin is its own standalone marketplace repo (no custom monorepo); pins use the
marketplace `ref` (a release tag) — the tightest pin `.claude/settings.json` supports.

## The plugins

| Plugin | Marketplace repo | Role | Pin |
| --- | --- | --- | --- |
| `ponytail` | `DietrichGebert/ponytail` | sharp minimalism lens | `v4.7.0` |
| `techdocs-authoring` | `pkloehn1/techdocs-authoring` | docs authoring + reference | `v0.2.0` |
| `karpathy-guidelines` | `pkloehn1/karpathy-minimalism` | behavioral conductor | `v0.1.0` |
| `thinking-tools` | `pkloehn1/thinking-tools` | problem-framing toolkit | `v0.1.0` |

`karpathy-guidelines` is the lean conductor (plan / verify / autonomy / context); it
delegates minimalism to `ponytail` and problem-framing to `thinking-tools`. The two
maintainer-owned repos follow the `techdocs-authoring` template (release-please, structural
validation, CodeQL).

## Wiring

Register the marketplaces and enable the plugins, pinned to exact `ref`, in the committed
`.claude/settings.json` (`.claude/settings.local.json` stays personal/gitignored):

- `extraKnownMarketplaces.<name>.source = { "source": "github", "repo": "<owner/repo>", "ref": "<tag>" }`
- `enabledPlugins["<plugin>@<marketplace>"] = true`

## Acceptance

- In a paragon-stats Claude Code session, all four plugins load (ponytail commands; the
  karpathy, techdocs, and thinking-tools skills available), each pinned by tag `ref`.
- Bumping a plugin requires a `ref` change — no floating, no silent drift.

## Changed from the original plan

Dropped: the custom private plugins **monorepo** and vendoring Karpathy minimalism into it.
Instead `karpathy-minimalism` and `thinking-tools` are standalone maintainer repos. Auto-updating
the pins (Dependabot can't read `.claude/settings.json`) is tracked separately in #77.
