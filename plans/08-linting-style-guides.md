# 08 — Linting, style guides, and CodeQL

## Goal

A check for every component, a short style guide documenting each, and CodeQL
semantic analysis. Mirrors the house pattern (centralized `.github/linters/`) but
right-sized for a .NET repo.

## Steps

1. **Centralized linter configs** under `.github/linters/` (single source consumed by
   both pre-commit and CI):
   - `.markdownlint.yml` — house rules: `MD013: {line_length: 200, code_blocks: false,
     tables: false}`, `MD033: {allowed_elements: [div, span, img, br, p, a]}`.
   - `.yaml-lint.yml` — `extends: default`; `line-length.max: 200`; `truthy` allow
     `[true,false,yes,no,on,off]`; `indentation: {spaces: 2, indent-sequences: true}`.
   - `zizmor.yaml` — GitHub Actions security linter config (run zizmor in CI).

2. **Style-guide docs** under `docs/style-guides/` (terse, one screen each, link out):
   - `csharp.md` — points at `.editorconfig` + StyleCop/Meziantou as the enforced
     source of truth; documents only project-specific conventions (naming, nullable,
     AOT-safe patterns: no unbounded reflection/`dynamic`).
   - `markdown.md`, `yaml.md`, `github-actions.md` — point at the configs above.
   - `commits.md` — Conventional Commits + signed-commit policy.

3. **C# linting** is already wired by [`02-build-config.md`](02-build-config.md)
   (`.editorconfig`, StyleCop.Analyzers, Meziantou.Analyzer, `TreatWarningsAsErrors`,
   `dotnet format`). Nothing new here beyond the style-guide doc.

4. **Polyglot linters** run via the pre-commit framework
   ([`05`](05-precommit-hooks.md)) and again in CI ([`09`](09-issue-pr-automation.md)
   adds the workflow): markdownlint, yamllint, actionlint, gitleaks.

5. **CodeQL** (mandatory quality gate). Add `.github/workflows/codeql.yml`:

   ```yaml
   name: codeql
   on:
     push:
       branches: [main]
     pull_request:
       branches: [main]
     schedule:
       - cron: "27 3 * * 1"   # weekly baseline
   permissions:
     contents: read
     security-events: write
   concurrency:
     group: codeql-${{ github.ref }}
     cancel-in-progress: true
   jobs:
     analyze:
       runs-on: windows-latest
       timeout-minutes: 30
       steps:
         - uses: actions/checkout@v5
         - uses: actions/setup-dotnet@v5
           with:
             global-json-file: global.json
         - uses: github/codeql-action/init@v4
           with:
             languages: csharp
             queries: security-and-quality
         - run: dotnet build --no-incremental
         - uses: github/codeql-action/analyze@v4
   ```

   Notes: C# is a compiled language, so CodeQL needs a real build — use an explicit
   `dotnet build` (more reliable than autobuild with CPM + analyzers). The
   `security-and-quality` query suite covers both security and maintainability.
   Verify action major versions are current at bootstrap.

## Acceptance

- `pre-commit run --all-files` is green.
- `actionlint`/`zizmor` pass on all workflow files.
- CodeQL workflow completes and uploads results to the Security tab; an intentionally
  vulnerable snippet (e.g. unsanitized path concat) is flagged, then removed.

## Commit

```text
git add .
git commit -S -m "ci: add polyglot linters, style-guide docs, and CodeQL analysis"
```
