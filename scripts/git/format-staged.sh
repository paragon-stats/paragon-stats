#!/bin/sh
# scripts/git/format-staged.sh — pre-commit format check, "modify & fail" (#47).
#
# Runs `dotnet format` over the staged C# files. If the staged content is already
# well-formatted, the commit proceeds untouched. If formatting changes anything,
# the fix is applied to the working tree, the commit is BLOCKED, and the affected
# files are printed — you review, `git add`, and commit again. The hook never
# silently adds the formatter's changes to your commit. CI re-verifies regardless
# (build.yml: `dotnet format --verify-no-changes`), so this is a fast local guard.
#
# Unstaged tracked edits and untracked files are stashed (--keep-index) first so
# the check only ever sees what is actually being committed, then restored.
set -eu

if [ -z "$(git diff --cached --name-only --diff-filter=ACMR -- '*.cs')" ]; then
  exit 0
fi

stashed=0
restore_unstaged() {
  if [ "$stashed" = 1 ]; then
    git stash pop --quiet 2>/dev/null || cat >&2 <<'MSG'
husky: your unstaged changes overlap the formatting and could not be
auto-restored — they are safe in `git stash list` (pop them manually).
MSG
  fi
}

if ! git diff --quiet || [ -n "$(git ls-files --others --exclude-standard)" ]; then
  git stash push --keep-index --include-untracked --quiet --message "husky:format-staged"
  stashed=1
fi
trap restore_unstaged EXIT

# Format only the staged files (NUL-delimited for paths with spaces); `-r` skips
# an empty list rather than formatting the whole solution.
git diff --cached -z --name-only --diff-filter=ACMR -- '*.cs' \
  | xargs -0 -r dotnet format --no-restore --include

# Compare the formatted working tree to the staged content. This must happen
# before restore_unstaged() runs (popping the stash would re-introduce unstaged
# edits and mask whether it was *formatting* that changed the staged files).
if git diff --quiet -- '*.cs'; then
  exit 0
fi

cat >&2 <<'MSG'
husky: dotnet format reformatted staged C# files — commit blocked.
The fixes are in your working tree; review, `git add`, and commit again:
MSG
git diff --name-only -- '*.cs' >&2
exit 1
