# Markdown style guide

Enforced by markdownlint via [`.github/linters/.markdownlint.yml`](../../.github/linters/.markdownlint.yml)
(in CI via Super-Linter).

## Conventions

- Prose lines up to 200 chars; code blocks and tables are never length-checked.
- Inline HTML limited to `div`, `span`, `img`, `br`, `p`, `a` (badges/layout).
- Keep docs terse and link out (Sonarr-style) — short README, deeper detail elsewhere.
- One H1 per file; use sentence-case headings.
- Unordered lists indent 2 spaces (MD007); ordered lists use a `1.`-only or sequential
  prefix (MD029 `one_or_ordered`). Use a bullet list, not faked ordered markers, for an
  unordered set.
- Tables are **compact** — a single space around cell content (`| a | b |`), MD060
  `compact`. Don't let your editor column-align them (Prettier does); a markdownlint-based
  formatter matches. Editor setup is per-developer (user settings), not committed here.
