#!/usr/bin/env python3
"""Validate that a commit message follows Conventional Commits.

Invoked by the pre-commit `commit-msg` hook with the commit message file path
as the single argument.
"""

import re
import sys

TYPES = "feat|fix|docs|chore|ci|refactor|test|perf|style|build|revert"
SUBJECT = re.compile(rf"^(?:{TYPES})(?:\([\w.\-/ ]+\))?!?: .+")
ALLOWED_PREFIXES = ("Merge ", "Revert ", "fixup!", "squash!")


def main() -> int:
    if len(sys.argv) < 2:
        print("usage: check_commit_message.py <commit-msg-file>")
        return 2
    with open(sys.argv[1], encoding="utf-8") as fh:
        lines = [ln for ln in fh.read().splitlines() if ln.strip() and not ln.startswith("#")]
    if not lines:
        print("commit message is empty")
        return 1
    subject = lines[0]
    if subject.startswith(ALLOWED_PREFIXES):
        return 0
    if not SUBJECT.match(subject):
        print(
            "Commit subject is not a Conventional Commit:\n"
            f"  {subject}\n"
            f"Expected: type(scope): subject   (types: {TYPES.replace('|', ', ')})"
        )
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
