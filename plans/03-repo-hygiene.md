# 03 — Repo hygiene

## Goal

Standard files every public .NET repo needs. Keep them lean — Sonarr-style.

## Steps

1. **`.gitignore`** — generate via `dotnet new gitignore`. This produces the
   canonical Microsoft template, no need to hand-craft.

2. **`.gitattributes`** — single short file with cross-platform line
   ending normalization:

   ```text
   * text=auto eol=lf
   *.sln eol=crlf
   *.bat eol=crlf
   *.cmd eol=crlf
   *.ps1 eol=crlf
   *.sh eol=lf
   Makefile eol=lf
   ```

3. **`LICENSE`** — Apache-2.0 text. Fetch from the canonical source:

   ```text
   curl -fsSL https://www.apache.org/licenses/LICENSE-2.0.txt -o LICENSE
   ```

4. **`README.md`** — Sonarr-style. Target ~50 lines max. Structure:

   ```markdown
   # paragon-stats

   A stats tool for *City of Heroes*. Clean-room reimplementation inspired
   by HeroStats.

   **Status: pre-alpha.** Scaffolding only; parsing engine not yet implemented.

   > **Built with AI assistance.** This project is developed with the help of
   > AI coding tools (Claude Code). All contributions are human-reviewed before
   > merge. Mentioned up front so you can make an informed decision about using
   > or contributing to it.

   ## Getting started

   - **Requirements**: .NET 10 SDK (Windows x64)
   - **Build**: `dotnet build`
   - **Test**: `dotnet test`

   ## Status

   | | |
   |---|---|
   | Language | C# / .NET 10 LTS |
   | Platform | Windows x64 (native AOT) |
   | License | Apache-2.0 |

   ## Contributing

   See [CONTRIBUTING.md](CONTRIBUTING.md).

   ## License

   [Apache-2.0](LICENSE).

   Inspired by HeroStats by `ineffablebob`, `lberger`, `msawczyn`, `lpfjones`,
   `thesteinerd`.

   *City of Heroes* is a trademark of NCSOFT. paragon-stats is not affiliated
   with or endorsed by NCSOFT or Homecoming Servers, LLC.
   ```

   Resist the urge to add roadmap sections, feature lists, badges, or future
   plans. Those go on the wiki (when one exists) or in issues.

5. **`CONTRIBUTING.md`** — short and practical. Cover:

   - **Build and test**: clone, run `dotnet tool restore`, then `dotnet build`
     and `dotnet test`.
   - **Code style**: enforced by `.editorconfig` and analyzers. Run
     `dotnet format` before committing (the pre-commit hook does this too).
   - **PR process**: branch from `main`, signed commits required, all
     status checks must pass.
   - **House rules** — two short paragraphs:
     1. Clean-room: do not paste code from `pkloehn1/herostats-svn-archive`.
        The archive is reference for *concept* only.
     2. Dependencies: Apache-2.0-compatible licenses only. No GPL, AGPL,
        MPL, or source-available non-OSI.

   Target ~40 lines.

6. **`Makefile`** — four targets, no more:

   ```makefile
   .PHONY: build test format clean

   build:
    dotnet build

   test:
    dotnet test

   format:
    dotnet format

   clean:
    dotnet clean
    rm -rf publish/
   ```

   (Use tabs, not spaces, for the indented lines — Make requires tabs.)

## Acceptance

- `cat README.md | wc -l` is under 60
- `cat CONTRIBUTING.md | wc -l` is under 50
- `make build`, `make test`, `make format`, `make clean` all work

## Commit

```text
git add .
git commit -S -m "docs: add LICENSE, README, CONTRIBUTING, gitignore, gitattributes, Makefile"
```
