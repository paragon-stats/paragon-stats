# Commit & PR style guide

## Conventional Commits

Format: `type(scope): subject`. Allowed types:
`feat`, `fix`, `docs`, `chore`, `ci`, `refactor`, `test`, `perf`, `style`, `build`, `revert`.

- `feat:` → minor bump; `fix:` → patch; `feat!:`/`BREAKING CHANGE:` → major
  (minor while pre-1.0). This is what Release Please reads to compute the version.
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
