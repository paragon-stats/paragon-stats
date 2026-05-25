# 05 — Husky.Net pre-commit hooks

## Goal

Catch formatting and analyzer issues before commits leave the local machine.
Use Husky.Net so the toolchain stays inside the .NET ecosystem (no Node or
Python dependency).

## Steps

1. **Create the local tool manifest** (if `01-dotnet-projects.md` didn't
   already):
   ```
   dotnet new tool-manifest
   ```

2. **Install Husky as a local tool**:
   ```
   dotnet tool install Husky
   ```

3. **Initialize Husky** in the repo:
   ```
   dotnet husky install
   ```

   This creates `.husky/` with hook scripts and registers the git hooks path.

4. **Define pre-commit tasks** in `.husky/task-runner.json`:

   ```json
   {
     "tasks": [
       {
         "name": "dotnet-format-staged",
         "group": "pre-commit",
         "command": "dotnet",
         "args": [
           "format",
           "--verify-no-changes",
           "--severity",
           "error",
           "--include",
           "${staged}"
         ],
         "include": ["**/*.cs"]
       },
       {
         "name": "build-warnaserror",
         "group": "pre-commit",
         "command": "dotnet",
         "args": [
           "build",
           "-warnaserror",
           "--nologo",
           "--verbosity",
           "quiet"
         ],
         "include": ["**/*.cs", "**/*.csproj", "**/Directory.Build.props", "**/Directory.Packages.props"]
       }
     ]
   }
   ```

5. **Wire the hook** in `.husky/pre-commit`:

   ```
   #!/bin/sh
   . "$(dirname -- "$0")/_/husky.sh"

   dotnet husky run --group pre-commit
   ```

   Ensure it's executable: `chmod +x .husky/pre-commit`

6. **Update `CONTRIBUTING.md`** to mention the onboarding step:
   ```
   After cloning, run: dotnet tool restore && dotnet husky install
   ```

## Acceptance

- Make a deliberate formatting violation in a staged `.cs` file and try
  to commit — the pre-commit hook fails.
- Restore the formatting (`dotnet format`) and the commit succeeds.

## Commit

```
git add .
git commit -S -m "chore: add Husky.Net pre-commit hooks (dotnet format, build -warnaserror)"
```
