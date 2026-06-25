# Testing & TDD

Write the test first. For every behaviour change — feature or fix — add the
failing test before the code that makes it pass.

## Why tests-first is the rule, not a preference

New code must be covered, and that's enforced, not aspirational:

- The **SonarQube quality gate** enforces the new-code coverage requirement and
  **blocks the merge** when it isn't met. Coverage is collected with
  `dotnet-coverage` and reported to SonarCloud on every PR.
- A red test or a coverage gap fails CI before review — caught in the pipeline,
  not in someone's head.

Writing the test first is the only dependable way to keep new code covered
without backfilling.

## Framework

- **xUnit v3** (not v2), run with `dotnet test`.
- Tests live in `tests/`, one test project per production project
  (`ParagonStats.Core.Tests` covers `ParagonStats.Core`).

## Loop

1. Write a failing test for the behaviour you want.
2. Make it pass with the smallest change.
3. Refactor while the test stays green.
4. `dotnet test` locally; CI re-runs it and the coverage gate.

See [csharp.md](csharp.md) for code style and
[review-workflow.md](review-workflow.md) for how PRs are reviewed.
