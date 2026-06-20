# Commit & PR style guide

## Conventional Commits

Format: `type(scope): subject`. Allowed types:
`feat`, `fix`, `docs`, `chore`, `ci`, `refactor`, `test`, `perf`, `style`, `build`, `revert`.

- `feat:` → minor bump; `fix:` → patch; `feat!:`/`BREAKING CHANGE:` → major
  (minor while pre-1.0). This is what Release Please reads to compute the version.
- Subject in imperative mood, no trailing period, ~72 chars.
- Reference issues in the body/footer: `Closes #N` (auto-closes on merge) or `Refs #N`.

Enforced by a `commit-msg` hook (pre-commit) and the `commitlint` CI check.

## Commits & PRs

- **Signed commits are required.**
- Branch from `main`; PRs target `main`; all required checks must pass.
- Squash or rebase merges only (no merge commits).
