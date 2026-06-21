# 05 — Pre-commit framework

> Supersedes the original Husky.Net plan. The maintainer approved a `pre-commit`
> framework; the Python [`pre-commit`](https://pre-commit.com) tool is the house
> standard across `repo-template` and its spokes and is the best polyglot fit. It
> runs the .NET checks *and* the markdown/yaml/actions/secret hooks from one config.

## Goal

Catch formatting, analyzer, commit-message, and hygiene issues before commits leave
the machine — with the same hooks CI enforces (parity).

## Prerequisites

- Python 3.12+ on PATH (`python3 --version`; `py -3` on Windows). This is a dev-only dependency; it is
  **not** a runtime dependency of the .NET app.

## Steps

1. **Install the tool** (`python3` on Linux/macOS, `py -3` on Windows; pin in
   `requirements-dev.txt` for reproducibility):

   ```text
   python3 -m pip install pre-commit
   ```

2. **Create `.pre-commit-config.yaml`** at the repo root. Right-sized for a .NET
   lib+CLI — keep .NET + markdown + yaml + actions + secrets + hygiene; **omit** the
   homelab Ansible/Docker/Terraform/Traefik/PowerShell hooks and Super-Linter.

   ```yaml
   default_install_hook_types: [pre-commit, commit-msg]
   repos:
     - repo: https://github.com/pre-commit/pre-commit-hooks
       rev: v5.0.0
       hooks:
         - id: end-of-file-fixer
         - id: trailing-whitespace
           args: [--markdown-linebreak-ext=md]
         - id: mixed-line-ending
           args: [--fix=lf]
         - id: check-yaml
         - id: check-json
         - id: check-merge-conflict
         - id: check-added-large-files
           args: [--maxkb=500]
     - repo: https://github.com/igorshubovych/markdownlint-cli
       rev: v0.45.0   # pin latest at bootstrap
       hooks:
         - id: markdownlint
           args: [-c, .github/linters/.markdownlint.yml]
     - repo: https://github.com/adrienverge/yamllint
       rev: v1.37.1   # pin latest at bootstrap
       hooks:
         - id: yamllint
           args: [-c, .github/linters/.yaml-lint.yml]
     - repo: https://github.com/rhysd/actionlint
       rev: v1.7.7    # pin latest at bootstrap
       hooks:
         - id: actionlint
     - repo: https://github.com/gitleaks/gitleaks
       rev: v8.28.0   # pin latest at bootstrap
       hooks:
         - id: gitleaks
     - repo: local
       hooks:
         - id: dotnet-format
           name: dotnet format (staged)
           language: system
           entry: dotnet format --verify-no-changes --severity error
           files: \.cs$
           pass_filenames: false
         - id: conventional-commit
           name: Conventional Commit message
           language: python
           stages: [commit-msg]
           entry: python scripts/check_commit_message.py
   ```

   `dotnet build -warnaserror` is intentionally **not** a pre-commit hook (too slow);
   CI enforces it. Add it as an opt-in `manual` hook if desired.

3. **Add a minimal commit-message checker** at `scripts/check_commit_message.py`
   validating Conventional Commits (`type(scope): subject`, allowed types
   `feat|fix|docs|chore|ci|refactor|test|perf|style|build|revert`). Keep it ~40 lines.

4. **Install the git hooks**:

   ```text
   pre-commit install --install-hooks
   ```

5. **Pin tool versions** in `requirements-dev.txt` (`pre-commit==...`) and document
   onboarding in `CONTRIBUTING.md` (`python3` on Linux/macOS, `py -3` on Windows):

   ```text
   python3 -m pip install -r requirements-dev.txt && pre-commit install --install-hooks
   ```

## Acceptance

- `pre-commit run --all-files` passes on the scaffold.
- A formatting violation in a staged `.cs` blocks the commit; `dotnet format` fixes it.
- A non-Conventional commit message is rejected by the `commit-msg` hook.

## Commit

```text
git add .
git commit -S -m "chore: add pre-commit framework (dotnet format, markdown/yaml/actions/secrets, commitlint)"
```
