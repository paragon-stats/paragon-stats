#!/usr/bin/env bash
# check-commit-message.sh - validate a commit subject follows Conventional Commits,
# and enforce that release-triggering types touch product code.
#
# Source of truth: paragon-stats/github-actions. Consumers that need this in a git
# hook vendor a pinned copy and verify it against the tagged upstream in CI; edit
# it here, never in the copy.
#
# Inputs are environment variables so the same file serves the composite action
# and a local hook:
#   MESSAGE_FILE   path to the commit message (or PR title)   [required]
#   CHANGED_PATHS  space/newline separated changed paths      [default: empty]
#   TYPES          pipe-separated accepted types              [default: TYPES_FILE]
#   RELEASE_TYPES  pipe-separated types that bump the version [default: TYPES_FILE]
#   TYPES_FILE     commit-types.txt to read both from         [default: next to this file]
#   PRODUCT_PATH   path prefix holding product code           [default: src/]
#   EXEMPT_PATTERN ERE matching subjects to wave through      [default: git's own]
#                  Empty means the default (the action passes "" for unset
#                  inputs); to disable exemptions pass a never-matching
#                  pattern such as 'a^'.
#
# A vendored copy must bring commit-types.txt along with it.
set -euo pipefail

MESSAGE_FILE="${MESSAGE_FILE:-}"
CHANGED_PATHS="${CHANGED_PATHS:-}"
PRODUCT_PATH="${PRODUCT_PATH:-src/}"
EXEMPT_PATTERN="${EXEMPT_PATTERN:-^(Merge |Revert |fixup!|squash!)}"

# The type set is data, not code: one versioned commit-types.txt, read by every
# action and every consumer, so the list cannot drift between repos. Column 1 is
# the type, column 2 the version bump it triggers (minor|patch|none).
#
# Both lists stay overridable so a repo can narrow the set for itself - but a
# repo overriding one must override both, and that contract is enforced: a bare
# type list carries no bump data, so deriving RELEASE_TYPES behind an
# overridden TYPES would silently guard the wrong set.
TYPES="${TYPES:-}"
RELEASE_TYPES="${RELEASE_TYPES:-}"
if { [[ -n "$TYPES" ]] && [[ -z "$RELEASE_TYPES" ]]; } || { [[ -z "$TYPES" ]] && [[ -n "$RELEASE_TYPES" ]]; }; then
  echo "TYPES and RELEASE_TYPES must be overridden together: a bare type list carries no bump data" >&2
  exit 2
fi
if [[ -z "$TYPES" ]]; then
  types_file="${TYPES_FILE:-$(dirname "$0")/commit-types.txt}"
  if [[ ! -f "$types_file" ]]; then
    echo "no commit types: set TYPES and RELEASE_TYPES, or provide $types_file" >&2
    exit 2
  fi
  TYPES="$(awk '!/^[[:space:]]*#/ && NF { printf "%s%s", sep, $1; sep = "|" }' "$types_file")"
  # Anything not spelled exactly "none" is release-triggering, so a typo in
  # column 2 over-guards - which fails loudly - rather than under-guarding.
  RELEASE_TYPES="$(awk '!/^[[:space:]]*#/ && NF && $2 != "none" { printf "%s%s", sep, $1; sep = "|" }' "$types_file")"
  if [[ -z "$TYPES" ]]; then
    echo "no commit types found in $types_file" >&2
    exit 2
  fi
fi

# Every pattern must compile, or the guards that use it fail CLOSED: grep exits
# 2 on a bad regex, which inside an if-condition reads as "no match" and would
# silently skip the very check the pattern exists for.
for pat in "$TYPES" "$RELEASE_TYPES" "$EXEMPT_PATTERN"; do
  rc=0
  grep -qE -e "$pat" /dev/null 2>/dev/null || rc=$?
  if [[ "$rc" -eq 2 ]]; then
    echo "invalid extended regex in TYPES/RELEASE_TYPES/EXEMPT_PATTERN: $pat" >&2
    exit 2
  fi
done

if [[ -z "$MESSAGE_FILE" ]] || [[ ! -f "$MESSAGE_FILE" ]]; then
  echo "usage: MESSAGE_FILE=<file> [CHANGED_PATHS=...] check-commit-message.sh" >&2
  exit 2
fi

# First non-blank, non-comment line is the subject. The `|| true` absorbs the
# SIGPIPE head induces on a long message.
subject="$(grep -vE '^[[:space:]]*(#|$)' "$MESSAGE_FILE" | head -n 1 || true)"
# A UTF-8 BOM is plausible from Windows editors and would break the anchor below.
subject="${subject#$'\xef\xbb\xbf'}"
if [[ -z "$subject" ]]; then
  echo "commit message is empty" >&2
  exit 1
fi

# git's own generated subjects are not Conventional Commits. The default waves
# them all through; a repo that wants hand-written revert(scope): subjects
# instead narrows the pattern to drop "Revert ".
if printf '%s' "$subject" | grep -qE "$EXEMPT_PATTERN"; then
  echo "Exempt subject: $subject"
  exit 0
fi

if ! printf '%s' "$subject" | grep -qE "^($TYPES)(\([[:alnum:]._/ -]+\))?!?: .+"; then
  {
    echo "Commit subject is not a Conventional Commit:"
    echo "  $subject"
    echo "Expected: type(scope): subject   (types: ${TYPES//|/, })"
  } >&2
  exit 1
fi

# Type is the leading run of letters. Deliberately NOT a regex re-match against
# TYPES: with a trailing .* every alternative matches at the same length, so sed
# returns the first alternative rather than the right one, misreading `feature:`
# as `feat` whenever one type is a prefix of another. The subject is already
# validated above, so the leading letters are the type.
type="${subject%%[^[:alpha:]]*}"

# Release-triggering types must change product code, and which types trigger a
# release comes from the fact-set's bump column, not a hardcoded subset - a
# patch-mapped perf: touching only CI would otherwise cut a release, the exact
# defect this guard exists for. Skipped when the caller passes no paths -
# including a value that is only whitespace, which is what an unstripped
# `git diff --name-only` yields when nothing is staged.
if printf '%s' "$type" | grep -qE "^($RELEASE_TYPES)\$" && [[ -n "${CHANGED_PATHS//[[:space:]]/}" ]]; then
  # Normalise Windows separators; a local hook passes native paths.
  normalised="${CHANGED_PATHS//\\//}"
  # Word-splitting is intended so each path is its own line; globbing is NOT -
  # an unquoted path containing * would otherwise re-resolve against the CWD.
  set -f
  # grep must consume all input: -q exits at the first match, and on a large PR
  # the unread remainder gives printf a SIGPIPE that pipefail turns into "no
  # match", rejecting a commit that did touch product code.
  # shellcheck disable=SC2086
  if ! printf '%s\n' $normalised | grep -E "^$PRODUCT_PATH" >/dev/null; then
    {
      echo "'$type:' changes nothing under $PRODUCT_PATH (product code):"
      echo "  $subject"
      echo "Release-triggering types (${RELEASE_TYPES//|/, }) bump the version and must touch $PRODUCT_PATH."
      # Derived, not restated: the non-releasing types are whatever remains of
      # TYPES once the release set is removed, so this hint can never go stale.
      non_release="$(printf '%s' "$TYPES" | tr '|' '\n' | { grep -vxE "($RELEASE_TYPES)" || true; } | sed 's/$/:/' | tr '\n' '/' )"
      [[ -z "$non_release" ]] || echo "Use ${non_release%/} for tooling, docs, tests, or CI."
    } >&2
    exit 1
  fi
  set +f
fi

echo "OK: $subject"
