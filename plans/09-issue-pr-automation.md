# 09 — Full issue/PR automation

> Historical record; superseded where it disagrees with live config. Known divergences: the
> `dependabot-auto-merge.yml` workflow and PR label-sync step were never built (auto-merge is
> off and squash merging is disabled, so the prescription no longer applies as written); the
> label set grew past the six types listed here (see `.github/labels.yml`).

## Goal

Reduce manual triage to near zero: structured intake, automatic labeling, board
placement, and dependency hygiene. Adapted from the house pattern, .NET-scoped.

## Steps

1. **Issue forms** under `.github/ISSUE_TEMPLATE/`:
   - `work-package.yml` — Type (work-package | incident-rca), Objective, Risk,
     Affected paths, Deliverables, Acceptance criteria, Dependencies, Estimate.
   - `bug.yml` — repro, expected/actual, version (from `paragon-stats --version`), logs.
   - `feature.yml` — problem, proposal, alternatives.
   - `config.yml` — `blank_issues_enabled: false`; link to Discussions.

2. **PR template** `.github/pull_request_template.md` — Summary (with
   `<!-- auto-summary:start -->`/`:end` markers), Linked issues (`Closes #N`),
   Checklist (tests, docs, conventional title).

3. **Auto-labeler** `.github/workflows/auto-labeler.yml` (`pull_request_target` +
   `issues`):
   - Path labels via `actions/labeler@v6` + `.github/labeler.yml`:
     `area/core` → `src/ParagonStats.Core/**`, `area/cli` → `src/ParagonStats.Cli/**`,
     `area/tests` → `tests/**`, `area/ci` → `.github/**`, `area/docs` → `docs/**` `**/*.md`,
     `area/build` → `**/*.props` `**/*.csproj` `global.json`.
   - Sync labels from linked issues onto the PR (github-script step).

4. **Label taxonomy** `.github/labels.yml` + `labels-sync.yml`
   (`crazy-max/ghaction-github-labeler@v5`, weekly + dispatch): `area/*`,
   `type/{feat,fix,chore,docs,refactor,test}`, `priority/{P0,P1,P2,P3}`,
   `status/*`, `dependencies`.

5. **Issue priority triage** `.github/workflows/issue-triage.yml` — deterministic
   `priority/*` from the form's Risk field.

6. **Project auto-add** `.github/workflows/add-to-project.yml`
   (`actions/add-to-project@v1`) — auto-add new issues/PRs to the project board.
   Requires a `read:project`+`project` token; create a classic/v2 Project first and
   set `PROJECT_URL`.

7. **Dependabot** `.github/dependabot.yml` — `nuget` (grouped: analyzers,
   Microsoft.Extensions.*) + `github-actions`, weekly. Plus
   `dependabot-auto-merge.yml`: auto-approve + squash patch/security; comment + skip
   on major.

8. **CODEOWNERS** `.github/CODEOWNERS` — `* @pkloehn1`.

## Acceptance

- Opening an issue via a form applies the right type labels.
- A PR touching `src/ParagonStats.Core/**` gets `area/core`; linking `Closes #N`
  copies the issue's labels and (on merge) closes it.
- New issues/PRs appear on the Project board automatically.
- A Dependabot patch PR auto-merges once checks pass.

## Notes / blockers

- Pushing files under `.github/workflows/` via `gh`/API needs the **`workflow`**
  token scope; the current `gh` token lacks it. Either `gh auth refresh -s workflow`
  or push workflow files over SSH git (no scope needed).

## Commit

```text
git add .
git commit -S -m "ci: add issue/PR templates, auto-labeler, triage, project automation, Dependabot"
```
