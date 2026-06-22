#!/usr/bin/env python3
"""Bootstrap the local dev environment for paragon-stats (cross-platform).

Sets up everything needed to develop and commit, then confirms the toolchain:
  - a .venv with the pinned pre-commit (requirements-dev.txt)
  - pre-commit git hooks (pre-commit + commit-msg, per .pre-commit-config.yaml)
  - restored .NET local tools (.config/dotnet-tools.json)
  - verifies Python, the .NET SDK, git, and signed-commit config

Idempotent: re-running is a no-op unless requirements-dev.txt, .pre-commit-config.yaml,
or .config/dotnet-tools.json changed (tracked by a hash in .venv).

Usage (run with your interpreter; Windows uses `py -3`, Debian uses `python3`):
  py -3 scripts/bootstrap.py            python3 scripts/bootstrap.py
  python3 scripts/bootstrap.py --verify     # check only, make no changes
  python3 scripts/bootstrap.py --dry-run    # print the plan, make no changes
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
VENV = REPO_ROOT / ".venv"
STATE_FILE = VENV / ".bootstrap-state.json"
TRACKED = ("requirements-dev.txt", ".pre-commit-config.yaml", ".config/dotnet-tools.json")
MIN_PYTHON = (3, 12)


def _venv_python() -> Path:
    return VENV / ("Scripts/python.exe" if sys.platform == "win32" else "bin/python")


def _run(argv: list[str], *, capture: bool = False) -> subprocess.CompletedProcess[str]:
    return subprocess.run(argv, cwd=str(REPO_ROOT), check=False, text=True, capture_output=capture)


def _desired_hash() -> str:
    digest = hashlib.sha256()
    for rel in TRACKED:
        path = REPO_ROOT / rel
        digest.update(path.read_bytes() if path.exists() else b"")
    return digest.hexdigest()


def _up_to_date() -> bool:
    if not _venv_python().exists() or not STATE_FILE.exists():
        return False
    try:
        return json.loads(STATE_FILE.read_text(encoding="utf-8")).get("hash") == _desired_hash()
    except (OSError, json.JSONDecodeError):
        return False


def _prerequisites() -> list[str]:
    problems: list[str] = []
    if sys.version_info < MIN_PYTHON:
        have = ".".join(map(str, sys.version_info[:3]))
        problems.append(f"Python {MIN_PYTHON[0]}.{MIN_PYTHON[1]}+ required (running {have}).")
    if shutil.which("dotnet") is None:
        problems.append("`dotnet` not on PATH — install the .NET SDK (matches global.json).")
    if shutil.which("git") is None:
        problems.append("`git` not on PATH.")
    return problems


def _warn_if_unsigned() -> None:
    result = _run(["git", "config", "--get", "commit.gpgsign"], capture=True)
    if result.stdout.strip().lower() != "true":
        print("[warn] commit.gpgsign != true — signed commits are required; configure signing before committing.")


def verify() -> int:
    problems = _prerequisites()
    if not _venv_python().exists():
        problems.append(".venv missing — run: python3 scripts/bootstrap.py")
    elif _run([str(_venv_python()), "-m", "pre_commit", "--version"], capture=True).returncode != 0:
        problems.append("pre-commit not installed in .venv.")
    if not (REPO_ROOT / ".git" / "hooks" / "pre-commit").exists():
        problems.append("pre-commit git hook not installed.")
    if not _up_to_date():
        problems.append("environment is stale — re-run: python3 scripts/bootstrap.py")
    if problems:
        print("[FAIL] dev environment is not ready:")
        for problem in problems:
            print(f"  - {problem}")
        return 1
    print("[OK] dev environment is correct and up to date.")
    return 0


def _plan() -> list[str]:
    steps: list[str] = []
    if not _venv_python().exists():
        steps.append("create .venv")
    steps.append("install pinned pre-commit (requirements-dev.txt)")
    steps.append("install pre-commit + commit-msg git hooks")
    steps.append("restore .NET local tools (dotnet tool restore)")
    return steps


def bootstrap(*, dry_run: bool) -> int:
    problems = _prerequisites()
    if problems:
        print("[FAIL] missing prerequisites:")
        for problem in problems:
            print(f"  - {problem}")
        return 1

    if _up_to_date():
        print("[OK] dev environment already up to date.")
        _warn_if_unsigned()
        return 0

    if dry_run:
        print("[DRY-RUN] would run:")
        for step in _plan():
            print(f"  - {step}")
        return 0

    if not _venv_python().exists():
        print("[INFO] creating .venv ...")
        if _run([sys.executable, "-m", "venv", str(VENV)]).returncode != 0:
            return 1

    venv_py = str(_venv_python())
    print("[INFO] installing pre-commit ...")
    if _run([venv_py, "-m", "pip", "install", "--upgrade", "pip"]).returncode != 0:
        return 1
    if _run([venv_py, "-m", "pip", "install", "-r", "requirements-dev.txt"]).returncode != 0:
        return 1

    print("[INFO] installing git hooks ...")
    if _run([venv_py, "-m", "pre_commit", "install", "--install-hooks"]).returncode != 0:
        return 1

    print("[INFO] restoring .NET local tools ...")
    if _run(["dotnet", "tool", "restore"]).returncode != 0:
        return 1

    STATE_FILE.write_text(json.dumps({"hash": _desired_hash()}, indent=2), encoding="utf-8")
    _warn_if_unsigned()
    print("[OK] dev environment ready.")
    return 0


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Bootstrap the paragon-stats dev environment.")
    parser.add_argument("--verify", action="store_true", help="Check the environment; make no changes.")
    parser.add_argument("--dry-run", action="store_true", help="Print the plan; make no changes.")
    args = parser.parse_args(argv)
    if args.verify and args.dry_run:
        print("[FAIL] --verify and --dry-run are mutually exclusive.")
        return 1
    return verify() if args.verify else bootstrap(dry_run=args.dry_run)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
