# 11 — Private plugins monorepo + plugin wiring

## Goal

Control AI-assist sprawl with **pinned, auditable** plugins. Vendor the Karpathy
minimalism guidance into a private, release-tracked plugin (the public source has no
releases — nothing to pin or audit), and consume `ponytail` pinned from upstream.

This is a **separate repo**, not part of `paragon-stats`. It mirrors the maintainer's
`techdocs-authoring`/`unifi-netops` pattern, in monorepo form.

## Part 1 — Create the monorepo (e.g. `pkloehn1/claude-plugins`, private)

Layout:

```
claude-plugins/
├── .claude-plugin/marketplace.json          # lists all plugins in this repo
├── plugins/
│   └── karpathy-minimalism/
│       ├── .claude-plugin/plugin.json        # name, version, description
│       ├── skills/minimalism/SKILL.md         # the 4 principles, vendored
│       └── README.md
├── release-please-config.json                # monorepo: per-package
├── .release-please-manifest.json
├── .github/workflows/release-please.yml
├── LICENSE        # MIT
├── NOTICE         # attribute Karpathy / multica-ai source (MIT)
└── README.md
```

1. **`plugin.json`** — annotate the version so Release Please can bump it:
   ```jsonc
   {
     "name": "karpathy-minimalism",
     "version": "0.0.0", // x-release-please-version
     "description": "Minimalist coding principles (vendored from Karpathy guidelines)."
   }
   ```

2. **`SKILL.md`** — vendor the four principles (Think Before Coding, Simplicity
   First, Surgical Changes, Goal-Driven Execution) as a proper Agent Skill with
   frontmatter (`name`, `description`). MIT-attribute the source in `NOTICE`.

3. **`marketplace.json`** — list `karpathy-minimalism` (and future plugins) with
   their `source` paths under `plugins/`.

4. **Release Please monorepo mode** — `packages` maps each `plugins/<name>` with
   `release-type: simple` and an `extra-files`/generic updater that bumps the
   `version` field in `plugin.json`. Tags are component-scoped, e.g.
   `karpathy-minimalism-v0.1.0`. This tag history is the auditable "what's injected"
   record.

## Part 2 — Wire plugins into `paragon-stats`

Register both marketplaces and enable the plugins **pinned to exact versions** in
this repo's `.claude/settings.json`:

- the private monorepo → enable `karpathy-minimalism@<version>`
- `DietrichGebert/ponytail` → enable `ponytail@<version>` (e.g. the current release)

> Confirm the exact `.claude/settings.json` keys for marketplaces + pinned plugins
> against current Claude Code plugin docs before writing — do not guess the schema.
> Pinning by version (not floating) is the point: it makes injected guidance auditable.

## Acceptance

- `release-please` in the monorepo cuts `karpathy-minimalism-v0.1.0`; the tag and the
  `plugin.json` `version` match.
- In a `paragon-stats` Claude Code session the pinned plugins load (the minimalism
  skill and ponytail commands are available).
- Bumping a plugin requires a version change → new tag → updated pin: no silent drift.

## Open items

- Monorepo repo name (`pkloehn1/claude-plugins`?).
- ponytail version to pin.
- ponytail and the Karpathy plugin both push minimalism — decide whether to run both
  or just one to avoid redundant guidance (sprawl control is the whole point).
