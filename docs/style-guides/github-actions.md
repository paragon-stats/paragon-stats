# GitHub Actions style guide

Workflows are linted by **actionlint** (syntax) and audited by **zizmor** (security),
config in [`.github/linters/zizmor.yaml`](../../.github/linters/zizmor.yaml).

## Conventions

- Pin `permissions:` at the least privilege needed; default to `contents: read`.
- Set `concurrency` on PR-triggered workflows. Required checks run with `cancel-in-progress: false` - a cancelled required check reports nothing and freezes the PR (#14).
- Pin actions to a full commit SHA with the exact version in a trailing comment (zizmor enforces the pair matching); Dependabot bumps them.
- Give every job a `timeout-minutes`.
- Reference SDK versions via `global-json-file: global.json`, not hardcoded strings.
- Build/test runs on `windows-latest` only, matching the single shipped RID
  (`win-x64`): Homecoming does not run natively on Linux, so a Linux leg verified a
  platform no player uses. The one-entry matrix shape is deliberate - collapsing it
  would rename the required check `build (windows-latest)` and freeze PRs (#14).
- Runner choice follows what a job *does*, not what the product ships:
  - **Windows** for anything that builds or runs the product - `build`, `binary`,
    and also `sonar` and `codeql`, which each do their own restore/build/test.
  - **Ubuntu** for CI glue that never touches a product binary - super-linter,
    commitlint, release orchestration, pr-summary and the project/label
    automation. The runner OS there says nothing about what the product supports.
  - The AOT publish step in `release.yml` must stay on Windows: `win-x64` is the
    only RID.
