# GitHub Actions style guide

Workflows are linted by **actionlint** (syntax) and audited by **zizmor** (security),
config in [`.github/linters/zizmor.yaml`](../../.github/linters/zizmor.yaml).

## Conventions

- Pin `permissions:` at the least privilege needed; default to `contents: read`.
- Set `concurrency` on PR-triggered workflows. Required checks run with `cancel-in-progress: false` - a cancelled required check reports nothing and freezes the PR (#14).
- Pin actions to a full commit SHA with the exact version in a trailing comment (zizmor enforces the pair matching); Dependabot bumps them.
- Give every job a `timeout-minutes`.
- Reference SDK versions via `global-json-file: global.json`, not hardcoded strings.
- Build/test runs on a `windows-latest` + `ubuntu-latest` matrix; the product ships
  `win-x64` only (Linux is a dev host, not a ship target), AOT-published per RID.
