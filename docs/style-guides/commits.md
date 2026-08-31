# Commit & PR style guide

## Conventional Commits

Format: `type(scope): subject`. The allowed `type` set is the org-wide fact-set in
[paragon-stats/github-actions](https://github.com/paragon-stats/github-actions) — `feat`,
`fix`, `docs`, `chore`, `ci`, `refactor`, `test`, `perf`, `security`, `style`, `build`,
`revert`. [`scripts/git/commit-types.txt`](../../scripts/git/commit-types.txt) is the
vendored copy pinned at v2.0.2 (CI fails on drift), read by the commit/PR-title validator;
the `branch-name` check reads the same fact-set from the shared action.

- `feat:` → minor bump; `fix:`/`perf:`/`security:`/`revert:` → patch;
  `feat!:`/`BREAKING CHANGE:` → major (minor while pre-1.0). Release-triggering types must
  touch `src/` — reverting a CI change is written as `ci:`, not `revert:`.
- Subject in imperative mood, no trailing period, ~72 chars.
- Reference issues in the body/footer: `Closes #N` (auto-closes on merge) or `Refs #N`.

Enforced by a Husky.Net `commit-msg` hook and the `commitlint` CI check.

## Commits & PRs

- **Signed commits are required.**
- Branch from `main`; PRs target `main`; all required checks must pass.
- Branch names follow `<type>/<issue#>-<short-kebab-summary>` (e.g. `ci/14-protected-trunk`) —
  same `type` set as commits. Enforced by the `branch-name` check; bot branches
  (`dependabot/**`, `release-please--**`) are exempt. Multi-issue branches use the lead issue.
- Squash or rebase merges only (no merge commits).
