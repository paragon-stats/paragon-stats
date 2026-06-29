# paragon-stats — Product Requirements (PRD)

Thin PRD for a public, single-maintainer project. The **live plan** (milestones,
issues, status) is tracked in the GitHub Project; this doc is the authoritative
**product narrative**. If a repo doc disagrees with this, this wins.

## What it is

A clean-room reimplementation of the concept behind HeroStats: a chat-log-driven
stats engine for *City of Heroes* (Homecoming). It reads the client chat logs on
disk and computes live and historical statistics. No code is copied from the
original — concept only.

## Who it's for

CoH players who want live performance and income stats while playing — especially
farmers and build-testers. Multibox-aware: monitor several accounts at once,
display one.

## Platform & shape

- .NET 10, native AOT, **Windows-only** (the game is Windows-in-practice; Linux dropped).
- Two binaries on a shared `ParagonStats.Core`:
  - `paragon-stats` — AOT CLI / TUI
  - `paragon-stats-gui` — Avalonia AOT GUI (tabbed; an overlay window follows in 2.0)

## Roadmap (capability milestones; Release Please owns minors)

Parsing engine → TUI (MVP readout) → GUI (tabbed, Avalonia) → **MVP (= 1.0)** →
**Overlay (= 2.0)**. Everything else lives in **Backlog** (operator-pull). Explicit
version targets exist only at deliberate majors (1.0, 2.0); all minors are emergent.

## MVP (1.0) — the ~7 metrics

infl/hr (kill income only), XP/hr, time-to-level, raw DPS, damage/activation per
power, procs/min (fired), plus free derivatives: defeats/hr, totals, session timer.
Delivered across engine + TUI + tabbed GUI. **The overlay is excluded from 1.0.**

## Key technical decisions

- **Save/export = JSON** (source-generated, AOT-safe). XML dropped to a maybe-later converter.
- **Storage = two-tier**: in-memory fold for the live path (bounded memory) + **SQLite**
  as a derived history/query store. The CoH chat logs are the source of truth; everything
  in SQLite is recomputable from them.
- **Identity = account → character → session**, segmented by the login banner. Multibox:
  monitor all active log streams, select one to display; attach new streams dynamically.
- **Income = kill income only** (`You gain … influence`); market/vendor (Consignment
  House) is a separate source → Backlog.
- **Build data is not in the logs** (respec/level-up emit no power/IO placement) →
  OCR / pop-menu-to-log only, post-1.0.
- **UI = Avalonia** (AOT production-ready, MIT). WPF (no AOT) and WinUI 3 (AOT preview) rejected.

## Out of scope for 1.0 (Backlog)

Market/vendor income, per-set / per-powerset aggregation, the in-game overlay (2.0),
graphs, sound alerts, OCR build parsing, and the long tail of HeroStats features.
Pulled in by operator decision; a pull with knock-on effects on already-identified
issues forces dependency evaluation.

## Releases

Release Please (Conventional Commits) + MinVer stamp the AOT binary from the git tag.
Mechanics: see [`../plans/10-release-automation.md`](../plans/10-release-automation.md).

## Attribution & legal

Apache-2.0. Inspired by HeroStats (`ineffablebob`, `lberger`, `msawczyn`, `lpfjones`,
`thesteinerd`). *City of Heroes* is a trademark of NCSOFT; paragon-stats is not
affiliated with or endorsed by NCSOFT or Homecoming Servers, LLC.
