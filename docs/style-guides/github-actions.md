# GitHub Actions style guide

Workflows are linted by **actionlint** (syntax) and audited by **zizmor** (security),
config in [`.github/linters/zizmor.yaml`](../../.github/linters/zizmor.yaml).

## Conventions

- Pin `permissions:` at the least privilege needed; default to `contents: read`.
- Set `concurrency` with `cancel-in-progress` on PR-triggered workflows.
- Pin actions to a major version (`actions/checkout@v4`); Dependabot bumps them.
- Give every job a `timeout-minutes`.
- Reference SDK versions via `global-json-file: global.json`, not hardcoded strings.
- Runners are `windows-latest` (this project is Windows x64 only).
