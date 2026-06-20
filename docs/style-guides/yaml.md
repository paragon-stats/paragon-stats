# YAML style guide

Enforced by yamllint via [`.github/linters/.yaml-lint.yml`](../../.github/linters/.yaml-lint.yml).

## Conventions

- 2-space indentation; sequences indented under their key.
- Lines up to 200 chars.
- Booleans: `true`/`false` (also `yes`/`no`/`on`/`off` accepted).
- No explicit `---`/`...` document markers (GitHub workflow/config YAML omits them).
- Quote strings only when needed (special characters, leading symbols).
