# 10 — Release automation (SemVer)

> Historical record, fully superseded. The bootstrap-era implementation this plan
> installed (a release-PR bot maintaining `CHANGELOG.md` and a version manifest) was
> retired org-wide in 2026-08 (#197, #221); its body is removed rather than preserved
> so no session mistakes it for live guidance.

## Goal (as originally stated)

Hands-off SemVer `major.minor.patch` releases driven by Conventional Commits, with
the binary version derived from the git tag. Matches the pattern in the maintainer's
plugin repos (`techdocs-authoring`, `unifi-netops`).

## Where release automation lives now

- **Mechanism**: the shared [`release` composite action](https://github.com/paragon-stats/github-actions/tree/main/release)
  in `paragon-stats/github-actions` — python-semantic-release, tag-only: the bump is
  computed from branch Conventional Commits since the last `v*` tag, the signed tag is
  pushed, and this repo's publish job creates the immutable GitHub Release complete
  with its binary.
- **Consumer wiring**: [`.github/workflows/release.yml`](../.github/workflows/release.yml);
  bump policy declared in [`pyproject.toml`](../pyproject.toml) and drift-checked
  against the vendored fact-set by the `commitlint` job.
- **Versioning policy**: [`docs/ROADMAP.md#versioning`](../docs/ROADMAP.md#versioning)
  (minors emergent; deliberate cuts at 1.0/2.0 via the action's planned force-level
  input, paragon-stats/github-actions#1).
- **MinVer** still stamps assembly/file/AOT versions from the git tag at build, no
  version literals in code — with one divergence from this plan's original step: live
  `Directory.Build.props` additionally sets `MinVerAutoIncrement: minor`, so between
  tags dev builds read `X.(Y+1).0-alpha`.

Deliberate non-goals of the replacement, for the record: no `CHANGELOG.md` file and no
release PR — main is PR-only with required signatures, so no bot commit can land; the
GitHub Release carries the notes.
