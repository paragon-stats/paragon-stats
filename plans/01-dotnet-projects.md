# 01 — Create .NET solution and projects

## Goal

Generate the .NET solution and three projects using `dotnet new` so file
contents are canonical (not hand-written).

## Prerequisites

- .NET 10 SDK installed locally — verify with `dotnet --list-sdks`. If no
  10.x.x version appears, install it from <https://dotnet.microsoft.com/download>
  before continuing.
- Working directory is the (empty or near-empty) repo root.

## Steps

1. **Pin the SDK**. Take the installed 10.x.x version from
   `dotnet --list-sdks` and:
   ```
   dotnet new globaljson --sdk-version <installed-version> --roll-forward latestFeature
   ```

2. **Create the solution**:
   ```
   dotnet new sln --name ParagonStats
   ```

3. **Create the three projects** in their final layout:
   ```
   dotnet new classlib --name ParagonStats.Core       --output src/ParagonStats.Core       --framework net10.0
   dotnet new console  --name ParagonStats.Cli        --output src/ParagonStats.Cli        --framework net10.0
   dotnet new xunit    --name ParagonStats.Core.Tests --output tests/ParagonStats.Core.Tests --framework net10.0
   ```

   Verify the test project uses **xUnit v3**, not v2. If `dotnet new xunit`
   produces v2, install the v3 template and re-create:
   ```
   dotnet new install xunit.v3.templates
   dotnet new xunit3 --name ParagonStats.Core.Tests --output tests/ParagonStats.Core.Tests --framework net10.0 --force
   ```

4. **Add projects to the solution**:
   ```
   dotnet sln add src/ParagonStats.Core src/ParagonStats.Cli tests/ParagonStats.Core.Tests
   ```

5. **Wire project references**:
   ```
   dotnet add src/ParagonStats.Cli reference src/ParagonStats.Core
   dotnet add tests/ParagonStats.Core.Tests reference src/ParagonStats.Core
   ```

6. **Clean up `dotnet new` placeholders**:
   - Delete `src/ParagonStats.Core/Class1.cs`
   - Keep `src/ParagonStats.Cli/Program.cs` (will be replaced with real entry
     point in later phases; placeholder is fine for now)
   - Keep one xUnit example test or replace with a minimal smoke test —
     either is acceptable

7. **Verify the build**:
   ```
   dotnet build
   dotnet test
   ```

## Acceptance

- `dotnet build` exits 0
- `dotnet test` exits 0
- `src/` contains two project directories; `tests/` contains one
- `ParagonStats.sln` references all three

## Commit

```
git add .
git commit -S -m "chore: scaffold .NET 10 solution with Core, Cli, and Tests projects"
```
