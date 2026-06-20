# 10 — Release automation (SemVer)

## Goal

Hands-off SemVer `major.minor.patch` releases driven by Conventional Commits, with
the binary version derived from the git tag. Matches the pattern in the maintainer's
plugin repos (`techdocs-authoring`, `unifi-netops`).

## Mechanism

- **Release Please** = version source of truth. Reads Conventional Commits since the
  last tag, maintains a **release PR**; merging it tags `vX.Y.Z`, cuts the GitHub
  Release, and updates `CHANGELOG.md`.
- **MinVer** = stamps `AssemblyVersion`/`FileVersion`/`InformationalVersion` (and the
  AOT binary) from the git tag at build. No version literals in code.
- Conventional Commits are enforced at commit time ([`05`](05-precommit-hooks.md)) and
  in CI (`commitlint.yml`), so Release Please always has clean input.

## Steps

1. **MinVer** — add to `Directory.Build.props` as an analyzer-style dev dependency:

   ```xml
   <ItemGroup>
     <PackageReference Include="MinVer" PrivateAssets="all" />
   </ItemGroup>
   <PropertyGroup>
     <MinVerTagPrefix>v</MinVerTagPrefix>
   </PropertyGroup>
   ```

   Pin the version in `Directory.Packages.props`. With no tags yet, MinVer reports
   `0.0.0-alpha.0.N`; after `v0.1.0` it reports `0.1.0`.

2. **Release Please config** at the repo root:

   `release-please-config.json`:
   ```json
   {
     "$schema": "https://raw.githubusercontent.com/googleapis/release-please/main/schemas/config.json",
     "release-type": "simple",
     "packages": { ".": { "package-name": "paragon-stats" } },
     "include-component-in-tag": false,
     "bump-minor-pre-major": true,
     "bump-patch-for-minor-pre-major": false
   }
   ```

   `.release-please-manifest.json`:
   ```json
   { ".": "0.0.0" }
   ```

   `release-type: simple` keeps the version in the manifest + a `version.txt`
   (MinVer ignores it; the **tag** is what stamps the build, so they stay aligned).

3. **Workflow** `.github/workflows/release-please.yml`:

   ```yaml
   name: release-please
   on:
     push:
       branches: [main]
   permissions:
     contents: write
     pull-requests: write
   jobs:
     release-please:
       runs-on: ubuntu-latest
       steps:
         - uses: googleapis/release-please-action@v4
           with:
             config-file: release-please-config.json
             manifest-file: .release-please-manifest.json
   ```

4. **commitlint CI** `.github/workflows/commitlint.yml` — validate each PR commit
   subject against Conventional Commits (reuse `scripts/check_commit_message.py`).

## Acceptance

- Merging a `feat:` commit makes Release Please open/refresh a release PR proposing
  the next minor (`v0.1.0` first).
- Merging that release PR creates the tag, GitHub Release, and CHANGELOG entry.
- `dotnet publish` after the tag produces a binary whose `--version` reports `X.Y.Z`
  (MinVer).

## Commit

```
git add .
git commit -S -m "ci: add Release Please + MinVer for SemVer releases"
```
