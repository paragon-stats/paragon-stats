# 06 — Push the scaffold

> **Status update**: the repo already exists, is **public**, and the plan files are
> pushed (`bdb3814`); local `main` tracks `origin/main`. So "create repo + first
> push" is done. This task is now just: commit each M1 scaffold step (signed) and
> push, then confirm CI goes green once the workflows from M2/M3 land.

## Goal

Get the scaffold onto GitHub and confirm the CI workflow goes green.

## Prerequisites

- The GitHub repo `paragon-stats/paragon-stats` exists (✓ already created/pushed).
- Commit signing is configured locally:
  ```
  git config commit.gpgsign        # should return "true"
  git config user.signingkey       # should return a key fingerprint or path
  ```
  And the corresponding public key is registered at
  <https://github.com/settings/keys>.

## Steps

1. **Initialize git** if not already (skip if `.git/` exists):
   ```
   git init -b main
   ```

2. **Stage everything**:
   ```
   git add .
   git status      # sanity-check what's about to be committed
   ```

3. **Make the initial commit** (signed). If you've been committing per-task
   throughout, this step is a no-op; otherwise:
   ```
   git commit -S -m "chore: initial scaffold"
   ```

4. **Add the remote and push**:
   ```
   git remote add origin git@github.com:paragon-stats/paragon-stats.git
   git push -u origin main
   ```

   If you authenticate over HTTPS instead of SSH, substitute:
   ```
   git remote add origin https://github.com/paragon-stats/paragon-stats.git
   ```

5. **Verify on GitHub**:
   - Open <https://github.com/paragon-stats/paragon-stats>
   - Confirm the file tree is present and README renders
   - Click the **Actions** tab; the `build` workflow should be running or
     completed. It must finish green before moving to Task 07.

## If the workflow fails

- **Format check failure**: run `dotnet format` locally, commit the changes,
  push.
- **Build failure**: examine the analyzer warning that became an error and
  fix at the source — do not suppress.
- **Test failure**: fix the test or the code under test.
- **SDK version mismatch**: ensure `global.json` matches the SDK version the
  workflow's `setup-dotnet` step is installing.

## Acceptance

- Repo shows the full file tree on GitHub
- README renders correctly on the repo landing page
- Actions tab shows the `build` workflow as success
