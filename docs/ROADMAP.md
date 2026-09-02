# Roadmap

The sequence from empty `src/` to v1.0.0, as checkpoints (CP). The
[PRD](PRD.md) is the source of truth for *what* the product is; this document
orders *when*, and links each checkpoint to its milestone. The GitHub Project
board tracks day-to-day state.

## Versioning

Minors are emergent: Conventional Commits drive them (`feat:` → minor;
`fix:`/`perf:`/`security:`/`revert:` → patch), cut automatically as signed
tags + GitHub Releases by python-semantic-release running tag-only in CI
(#197). Explicit version targets exist only at deliberate majors: **1.0**
(#168, the MVP gate) and **2.0** (Overlay). Shipped versions live on the
[Releases page](https://github.com/paragon-stats/paragon-stats/releases); the
checkpoint table below is the live view of where the work is.

## Checkpoints

| CP | Milestone | Spine | Exit gate |
| --- | --- | --- | --- |
| **CP0 Groundwork** | [Release pipeline (v0.1.0)](https://github.com/paragon-stats/paragon-stats/milestone/16) | #211, #197, #73 | Signed win-x64 AOT binary on a real GitHub Release, MinVer-stamped, cut automatically |
| **CP1 Parsing engine** | [Parsing engine](https://github.com/paragon-stats/paragon-stats/milestone/9) | #142 capture/watch/parse + #123–#131 mechanics | Live Homecoming chat logs parsed into the account → character → session model, replaying those logs reproduces the same statistics exactly, and the published binary demonstrates it with a usable CLI surface (#238, #236) |
| **CP2 MVP metrics** | [MVP](https://github.com/paragon-stats/paragon-stats/milestone/12) | #86 #87 #88 #90 #96 #99 #100 #127 | The ~7 metrics verified against a captured log source, demonstrated by running the published binary, not only by tests (#240) |
| **CP3 TUI** | [TUI - MVP readout](https://github.com/paragon-stats/paragon-stats/milestone/10) | #213 | Live metric readout usable during play, multibox-aware, demonstrated by running the published binary, not only by tests (#241) |
| **CP4 GUI** | [GUI - tabbed (Avalonia)](https://github.com/paragon-stats/paragon-stats/milestone/11) | #214 scaffold + #153 tabs | Tabbed Avalonia readout at parity with the TUI, demonstrated by running the published binary, not only by tests (#242) |
| **CP5 v1 gates** | [MVP](https://github.com/paragon-stats/paragon-stats/milestone/12) | #11, #37, #168 | **v1.0.0**: frozen save format, signed binaries, analyzer + coverage gates re-armed, all demonstrated by running the published binary, not only by tests (#243) |

CP1 and CP2 overlap naturally (each metric lands as its own `feat:` minor on
the engine). CP3 starts once the presentation model exists; CP4 shares it.

### Closing a checkpoint

CP1 was once declared closed on a green test suite and a locally built CLI.
The published artifact had never been run; when it finally was, it had no
`--help` and no `--version`, and the C# style guide was claiming a `linux-x64`
build that has never existed. Nothing was wrong with the engine. What was
wrong was accepting "the tests pass" as proof about a binary nobody had
executed.

So closing any checkpoint means recording, in the milestone's closing comment:

1. The release asset downloaded and its **sha256 verified against the release
   digest** - confirming the bits tested are the bits shipped.
2. That binary run against the real source, read-only, with the headline
   numbers recorded.
3. Its output diffed against the previous release's, **every delta explained**.
   An unexplained delta blocks the close.
4. The CLI surface the checkpoint claims to deliver, exercised from the
   artifact - including `--help`, `--version` and the start banner.

Steps 2-4 also run on every pull request via the `binary` job in `build.yml`
(#236), so the pass at milestone close confirms rather than being the only
line of defence. Tracking: **#239**.

## After 1.0

- **Overlay (2.0)** — the in-game always-on-top window (#154), milestone
  [Overlay](https://github.com/paragon-stats/paragon-stats/milestone/13).
- **Backlog** — operator-pull only; nothing in it blocks a checkpoint.

Off the critical path, any time: #77 (plugin pin freshness), #203 (workflow
plugin), #198 (closes when the sibling repo finishes its repoint).

## Feature map

The bootstrap-era `plans/FEATURE-MAP.md` and `docs/release-strategy.md`
(the "version ladder") were consolidated away when the Project board and the
PRD became the source of truth; issue bodies that once linked them point
here instead. The catalog they held is the open issue list itself: every
feature is a one-line issue (#86–#164), milestoned per the table above, and
elaborated only when its checkpoint is picked up. The ladder's pinned
pre-1.0 versions are retired — see [Versioning](#versioning).
