# 11 — Pinned dev plugins (wiring)

## Goal

Control AI-assist sprawl with **pinned, auditable** Claude Code plugins, wired into this
repo's `.claude/settings.json` so every contributor gets a consistent, version-pinned set.
Each plugin is its own standalone marketplace repo (no custom monorepo).

Pins use the marketplace `ref` (a release tag) — the tightest pin `.claude/settings.json`
supports (the schema takes a tag/branch ref, not a commit SHA). A tag is fixed, not floating,
but technically mutable: a moved upstream tag would change the fetched plugin without a
settings change, so #77's pin auto-update should also catch a tag that moves.

## The plugins

The four plugins and their `ref` pins are the single source of truth in
[`../.claude/settings.json`](../.claude/settings.json):

| Plugin | Marketplace repo | Role |
| --- | --- | --- |
| `ponytail` | `DietrichGebert/ponytail` | sharp minimalism lens |
| `techdocs-authoring` | `pkloehn1/techdocs-authoring` | docs authoring + reference |
| `karpathy-guidelines` | `pkloehn1/karpathy-minimalism` | behavioral conductor |
| `thinking-tools` | `pkloehn1/thinking-tools` | problem-framing toolkit |

`karpathy-guidelines` is the lean conductor (plan / verify / autonomy / context); it delegates
minimalism to `ponytail` and problem-framing to `thinking-tools`. The two maintainer-owned
repos follow the `techdocs-authoring` template (release-please, structural validation, CodeQL)
and each carries its own `NOTICE` for source attribution.

## Wiring

Register the marketplaces and enable the plugins, pinned to exact `ref`, in the committed
`.claude/settings.json` (`.claude/settings.local.json` stays personal/gitignored):

- `extraKnownMarketplaces.<name>.source = { "source": "github", "repo": "<owner/repo>", "ref": "<tag>" }`
- `enabledPlugins["<plugin>@<marketplace>"] = true`

## Acceptance

- In a paragon-stats Claude Code session, all four plugins load (ponytail commands; the
  karpathy, techdocs, and thinking-tools skills available), each pinned by tag `ref`.
- Bumping a plugin requires a `ref` change — not floating.

## Open items

- Auto-updating the pins (Dependabot can't read `.claude/settings.json`) is tracked in #77.
