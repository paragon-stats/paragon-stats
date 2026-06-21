# 02 — Build configuration

## Goal

Apply solution-wide MSBuild settings so all projects share consistent build
behavior. Move package version pins out of individual `.csproj` files into
Central Package Management.

## Steps

1. **Create `Directory.Build.props`** at the repo root with these properties
   in a single `<PropertyGroup>`:

   - `<LangVersion>latest</LangVersion>`
   - `<Nullable>enable</Nullable>`
   - `<ImplicitUsings>enable</ImplicitUsings>`
   - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
   - `<AnalysisLevel>latest-recommended</AnalysisLevel>`
   - `<AnalysisMode>AllEnabledByDefault</AnalysisMode>`
   - `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`

   And package metadata:
   - `<Authors>paragon-stats contributors</Authors>`
   - `<Copyright>Copyright (c) 2026 paragon-stats contributors</Copyright>`
   - `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>`

   And a global `<ItemGroup>` adding analyzers as `PrivateAssets="all"`:
   - `StyleCop.Analyzers`
   - `Meziantou.Analyzer`

   (Versions go in `Directory.Packages.props`, not here.)

2. **Create `Directory.Packages.props`** at the repo root:

   ```xml
   <Project>
     <PropertyGroup>
       <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
     </PropertyGroup>
     <ItemGroup>
       <!-- Pin versions discovered during build. Examples: -->
       <!-- <PackageVersion Include="StyleCop.Analyzers" Version="..." /> -->
       <!-- <PackageVersion Include="Meziantou.Analyzer" Version="..." /> -->
     </ItemGroup>
   </Project>
   ```

   Then run `dotnet add package StyleCop.Analyzers` and
   `dotnet add package Meziantou.Analyzer` against any one project — CPM
   will move the version pins into `Directory.Packages.props` automatically.
   Use the latest stable releases at the time of bootstrap.

3. **Remove duplicates from individual `.csproj` files**:
   - Drop `<TargetFramework>` from each (Directory.Build.props sets it
     globally — or leave it project-by-project if you prefer per-project
     overrides)
   - Drop `<Nullable>`, `<ImplicitUsings>` if duplicated

4. **Configure `ParagonStats.Cli` for native AOT publish**. In
   `src/ParagonStats.Cli/ParagonStats.Cli.csproj`:
   - `<PublishAot>true</PublishAot>`
   - `<InvariantGlobalization>true</InvariantGlobalization>`
   - `<RuntimeIdentifiers>win-x64;linux-x64</RuntimeIdentifiers>`

5. **Create `.editorconfig`** via `dotnet new editorconfig` to get the
   canonical Microsoft .NET defaults. Do not add speculative analyzer
   suppression rules. Add overrides only when a specific need arises during
   development.

## Acceptance

- `dotnet build -warnaserror` exits 0
- `dotnet format --verify-no-changes` exits 0
- A deliberate test (e.g., introducing an unused variable) produces a build
  error, confirming `TreatWarningsAsErrors` is active

## Commit

```text
git add .
git commit -S -m "chore: add Directory.Build.props, CPM, and .editorconfig"
```
