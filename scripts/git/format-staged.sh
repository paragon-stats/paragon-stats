#!/bin/sh
# scripts/git/format-staged.sh — pre-commit format check, "modify & fail" (#47).
#
# Runs `dotnet format` over the *staged* C# files. If the staged content is
# already formatted, the commit proceeds. If formatting changes anything, the
# fix is left in the working tree and the commit is BLOCKED for review +
# re-stage — the hook never silently adds the formatter's output to the commit.
#
# Scope is intentionally the staged .cs files only (not the whole solution);
# CI verifies the full tree (build.yml: `dotnet format --verify-no-changes
# --severity error`), so this is a fast local guard, not the enforcement
# boundary. `--severity error` matches CI so local and CI agree.
set -u

staged=$(git diff --cached --name-only --diff-filter=ACMR -- '*.cs')
[ -n "$staged" ] || exit 0

# Hide unstaged tracked edits so only staged content is formatted/evaluated.
# Untracked files don't affect `--include` and are left in place. Record our
# exact stash commit so we restore *that* one, never a foreign stash on top.
stash_ref=""
if ! git diff --quiet; then
  git stash push --keep-index --quiet --message "husky:format-staged"
  stash_ref=$(git rev-parse --verify --quiet 'stash@{0}') || stash_ref=""
fi

# Safety net: if the script exits abnormally before the explicit restore below
# (e.g. the formatter is killed), best-effort pop so work is not left stashed.
reached_restore=0
# shellcheck disable=SC2329,SC2317  # invoked indirectly via `trap cleanup EXIT`
cleanup() {
  [ "$reached_restore" = 1 ] && return 0
  [ -n "$stash_ref" ] && git stash pop --quiet 2>/dev/null
  return 0
}
trap cleanup EXIT

# Format the staged files (NUL-delimited for paths with spaces). Capture the
# status instead of letting it abort the script, so a formatter failure (e.g. a
# compile error blocking format) yields a clear message, not an opaque exit.
fmt_status=0
git diff --cached -z --name-only --diff-filter=ACMR -- '*.cs' \
  | xargs -0 dotnet format --severity error --include || fmt_status=$?

# Decide the outcome BEFORE restoring: popping the stash re-introduces unstaged
# edits, which would blur whether it was *formatting* that changed the staged .cs.
changed=0
git diff --quiet -- '*.cs' || changed=1

# Explicit restore: pop our stash by verified ref, falling back to apply-by-ref
# if something else landed on top. A restore that cannot complete is itself a
# blocking condition — never proceed leaving the developer's work stranded.
reached_restore=1
restored=1
if [ -n "$stash_ref" ]; then
  if [ "$(git rev-parse --verify --quiet 'stash@{0}' 2>/dev/null)" = "$stash_ref" ]; then
    git stash pop --quiet || restored=0
  else
    git stash apply --quiet "$stash_ref" || restored=0
  fi
fi

if [ "$restored" != 1 ]; then
  cat >&2 <<'MSG'
husky: formatting is done, but your unstaged changes could not be auto-restored
(they overlap the formatting). They are safe in `git stash list` — pop them
manually to resolve. Commit blocked.
MSG
  exit 1
fi

if [ "$fmt_status" -ne 0 ]; then
  echo "husky: dotnet format failed (exit $fmt_status) — commit blocked." >&2
  exit "$fmt_status"
fi

if [ "$changed" -ne 0 ]; then
  cat >&2 <<'MSG'
husky: dotnet format reformatted staged C# files — commit blocked.
Review the changes, `git add` them, and commit again:
MSG
  git diff --name-only -- '*.cs' >&2
  exit 1
fi

exit 0
