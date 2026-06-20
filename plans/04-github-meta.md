# 04 — GitHub metadata files

## Goal

CODEOWNERS, Dependabot config, and a single CI workflow. Keep it minimal —
no Super-Linter (overkill for now), no separate lint workflow.

## Steps

1. **`.github/CODEOWNERS`** — one line:

   ```text
   * @pkloehn1
   ```

2. **`.github/dependabot.yml`** — two ecosystems, weekly cadence:

   ```yaml
   version: 2
   updates:
     - package-ecosystem: nuget
       directory: /
       schedule:
         interval: weekly
       open-pull-requests-limit: 5
       groups:
         analyzers:
           patterns:
             - "StyleCop.Analyzers"
             - "Meziantou.Analyzer"
             - "Microsoft.CodeAnalysis.*"
         microsoft-extensions:
           patterns:
             - "Microsoft.Extensions.*"

     - package-ecosystem: github-actions
       directory: /
       schedule:
         interval: weekly
   ```

3. **`.github/workflows/build.yml`** — single workflow, Windows-only:

   ```yaml
   name: build

   on:
     push:
       branches: [main]
     pull_request:
       branches: [main]
     workflow_dispatch:

   permissions:
     contents: read

   concurrency:
     group: ${{ github.workflow }}-${{ github.ref }}
     cancel-in-progress: ${{ github.event_name == 'pull_request' }}

   jobs:
     build:
       runs-on: windows-latest
       timeout-minutes: 15
       steps:
         - uses: actions/checkout@v4
         - uses: actions/setup-dotnet@v4
           with:
             global-json-file: global.json
         - run: dotnet restore
         - run: dotnet format --verify-no-changes --severity error --no-restore
         - run: dotnet build --no-restore -warnaserror
         - run: dotnet test --no-build
   ```

   Before committing: verify `actions/checkout` and `actions/setup-dotnet`
   major versions are still current. Bump if newer majors exist.

## Acceptance

- `yamllint .github/workflows/build.yml` and `yamllint .github/dependabot.yml`
  produce no errors (install yamllint if not present: `pip install yamllint`)
- After push, the `build` workflow appears on the Actions tab and goes green

## Commit

```text
git add .
git commit -S -m "ci: add CODEOWNERS, Dependabot, and Windows build workflow"
```
