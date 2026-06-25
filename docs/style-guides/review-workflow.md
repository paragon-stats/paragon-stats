# Review workflow

Every pull request clears two reviews and green CI before it merges. The two
reviews use different lenses, so both are required.

## Two reviews, different lenses

- **Correctness review** — bugs, correctness, and reuse/simplification. One
  finder per angle, each candidate finding independently verified before it is
  reported.
- **Over-engineering review** — complexity only: what to delete (reinvented
  standard library, speculative abstractions, dead flexibility). It does not
  hunt for bugs; that is the correctness review's job.

A PR is ready to merge only once both have run and every confirmed finding is
resolved — fixed, or accepted with a one-line note on the PR explaining why.

## Merge gate

1. All required status checks are green.
2. Both reviews resolved.
3. The maintainer merges.

See [commits.md](commits.md) for branch, commit, and merge conventions.
