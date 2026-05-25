# 07 — Branch protection (manual, GitHub UI)

## Goal

Apply a ruleset to `main` so future direct pushes are blocked, PRs require
review and passing checks, and commits must be signed.

## Why manual

Applying rulesets via `gh api` requires integration IDs that vary by repo
and are not easily discoverable without trial-and-error API calls. The
GitHub UI handles this without ambiguity. This is a one-time setup.

## Prerequisites

- Task 06 complete: the `build` workflow has run at least once on `main`,
  so its check name appears in the status-check picker.

## Steps (in GitHub UI)

Navigate to: **Settings → Rules → Rulesets → New branch ruleset**

1. **Ruleset Name**: `main-protection`

2. **Enforcement status**: `Active`

3. **Target branches**: click **Add target**, choose **Include default branch**

4. **Bypass list**: click **Add bypass**, choose **Repository admin role**,
   bypass mode **Always**.
   (This lets you push directly during early solo work; remove the bypass
   once contributors arrive.)

5. **Rules** — enable these checkboxes:
   - [x] Restrict deletions
   - [x] Require linear history
   - [x] Require signed commits
   - [x] Block force pushes
   - [x] **Require a pull request before merging**
     - Required approvals: `1`
     - [x] Dismiss stale pull request approvals when new commits are pushed
     - [x] Require review from Code Owners
     - [x] Require conversation resolution before merging
     - Allowed merge methods: `Squash`, `Rebase` (uncheck `Merge`)
   - [x] **Require status checks to pass**
     - [x] Require branches to be up to date before merging
     - Add required check: `build` (search for it in the picker; it appears
       after the first workflow run on `main`)

6. Click **Create** at the bottom.

## Optional follow-on: enable CodeQL

Settings → **Code security** → **Code scanning** → **Set up CodeQL** →
**Default**.

After the first CodeQL run completes:
- Return to **Settings → Rules → Rulesets → main-protection**
- Edit the ruleset, find **Require status checks**, add `CodeQL` to the list
- Save

## Acceptance

- The ruleset appears at **Settings → Rules** with status **Active**
- Opening a PR shows the required checks and reviewer requirements
- Attempting a direct push to `main` with bypass temporarily disabled is
  rejected (re-enable bypass after testing)
