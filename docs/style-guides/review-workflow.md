# Review workflow

Every pull request clears two reviews and green CI before it merges. The two
reviews use different lenses, so both are required.

## Two reviews, different lenses

- **Correctness review** (`/code-review`) — bugs, correctness, and
  reuse/simplification. Runs one finder per angle and verifies each candidate
  finding independently before reporting.
- **Over-engineering review** (`/ponytail-review`) — complexity only: what to
  delete (reinvented standard library, speculative abstractions, dead
  flexibility). It does not hunt for bugs; that is the correctness review's job.

A PR is ready to merge only once both have run and every confirmed finding is
resolved — fixed, or accepted with a one-line note on the PR explaining why.

## Merge gate

1. All required status checks are green.
2. Both reviews resolved.
3. The maintainer merges (squash or rebase; no merge commits).

Branch from `main` and target `main`. See [commits.md](commits.md) for commit
and versioning conventions.
