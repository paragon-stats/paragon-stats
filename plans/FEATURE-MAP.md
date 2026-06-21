# Feature map — HeroStats → paragon-stats (clean-room)

> **Clean-room, concept-only.** The original HeroStats is **GPLv2**; paragon-stats is an
> Apache-2.0 **clean-room** reimplementation. This map records *what* capabilities/
> statistics existed — never code, algorithms, or formulas. Feature facts are not
> copyrightable; implementation is.
>
> **The real clean-room risk is at implementation time, not here.** Do **not**
> reverse-engineer the original's data structures, category enums, save-file format, or
> metadata schema field-for-field from the GPLv2 source — design those independently.
> (Five inventory entries that drifted toward the original's internals — a proprietary
> format id, a category enum, an internal struct shape, an internal flag, and an exact
> metadata field roster — were paraphrased to concept level here.)

Assembled by the `herostats-feature-map` workflow (run `wf_c95299f0-4cc`) from the
SourceForge project (concept/names only), herostats.org, the local `.hsd` data schema,
and CoH community knowledge — **79 unique features** from 207 raw entries. A clean-room
audit confirmed no copied code or formulas.

## Surface split

paragon-stats is **one repo, two surfaces**: shared `ParagonStats.Core` + `ParagonStats.Cli`
first, `ParagonStats.Gui` later.

| Surface | Count | Meaning |
| --- | --: | --- |
| CLI-only | 0 | Nothing is command-line-exclusive |
| Core / both | 58 | Parse + compute + report — the CLI delivers all of these |
| GUI-only | 21 | Live / visual / interactive — needs a UI |
| **Total** | **79** | |

> The **Core/CLI track owns 58 features**; the **GUI adds 21** on top.
> HeroStats was GUI-only, so this CLI/GUI split is paragon-stats' own design choice.

## Roadmap implication (start-at-patch)

Under start-at-patch ([`RELEASE-MAP.md`](RELEASE-MAP.md)), each shipped feature/fix is a
`0.0.x` patch. The **58 Core/CLI features** are the body of the pre-`0.1.0` alpha
line; **`Release-As: 0.1.0`** marks the CLI milestone once the core set is usable. The
**21 GUI features** form the later GUI milestone (still `0.x`).

## Core / CLI features (58)

| Feature | What it does |
| --- | --- |
| Experience rate (per minute/hour) | Rate of XP earned over a running timer, expressed per minute and per hour. |
| Experience to next level | Remaining XP required to reach the next level. |
| Estimated time to level | Projected time remaining until next level based on current XP rate. |
| Last-kill experience | XP awarded by the most recently defeated enemy. |
| Influence/Infamy rate (currency earned) | Rate and total of in-game currency (influence/infamy) earned over time. |
| Prestige tracking | Records supergroup prestige earned as a statistic. |
| Architect (AE) ticket tracking | Tracks Architect Entertainment ticket gain. |
| Debt paid rate | Rate at which experience debt is being cleared, per minute and per hour. |
| Experience debt remaining | Outstanding XP debt still owed. |
| Estimated time to clear debt | Projected time to pay off remaining debt at current rate. |
| Villains defeated (total / team) | Count of enemies defeated including personal and team contributions. |
| Final-blow defeats | Count of enemies where the player landed the killing blow. |
| Specific villain defeat counts | Defeat tallies broken down per named villain or enemy rank/class. |
| Average damage per power | Mean damage dealt by each individual power. |
| Total damage per power (incl. DoT) | Total damage attributed to each power, including damage-over-time. |
| Damage by damage type | Outgoing damage categorized by its type (fire, smashing, etc.). |
| Power accuracy / to-hit rate | Hit/accuracy success rate measured per power. |
| Overall accuracy | Aggregate hit accuracy across all attacks. |
| Player hits and misses | Counts player attack outcomes per power: hits and number of to-hit rolls. |
| Critical hits per power (count and rate) | Number and percentage of critical hits for each power. |
| Power usage count | Number of times each power has been activated. |
| Power recharge time (last observed) | Most recent recharge duration recorded for each power. |
| Injuries / damage taken by villain | Damage taken by the player broken down by named attacking enemy. |
| Injuries / damage taken by type | Incoming damage to the player categorized by damage type. |
| Endurance drains received | Count of endurance-drain effects inflicted on the player by enemies. |
| Buffs given and received | Counts of buffs cast on others and received from others. |
| Healing given and received | Amounts of healing dispensed and received. |
| Status effects applied to player | Counts of hold/sleep/stun and similar status effects applied to the player. |
| Time spent in status effects | Total duration the player was held/slept/stunned, etc. |
| Enemy hits, misses, and accuracy | Per enemy rank and power: hits, misses, to-hit rolls, and computed accuracy percentage against the player. |
| Enemy to-hit chance and rolls | Tracks enemy to-hit chance and roll values per rank/power, as average and total. |
| Loot / drops tracking | Per-item drop tracking pairing each inspiration/enhancement/recipe/salvage name with a tally of how many dropped, plus an aggregate total. |
| Inspiration usage | Counts inspirations consumed, per named inspiration and as a total. |
| Powers used per name | Tracks activation counts keyed by power name. |
| Character defeats (deaths) | Count of times the player's character was defeated. |
| HP and endurance tracking | Tracks current HP and current endurance as statistics. |
| Level-up event tracking | Records each level-up event keyed by the level reached. |
| Per-minute / per-hour rate engine | Derives per-minute and per-hour throughput for applicable statistics over elapsed playtime. |
| Uniform metric model | Every tracked statistic exposes a value plus derived rates — a consistent shape, designed independently. |
| Resettable / pausable stats timer | Timer that can be reset to compute stats over a chosen window or paused to freeze rate calculations. |
| Selective stat reset | Clear a single statistic or every statistic at once. |
| Session duration / playtime | Length of the current play session; basis for rate stats. |
| Chat message log capture | Captures and stores all parsed chat/log messages, retaining timestamp, channel, category, and text. |
| Chat message categorization | Tags each logged message with an event category that classifies the event type. |
| Pass-through / raw logging mode | Writes chat directly to file without analysis, or logs raw messages with stat computation deferred. |
| Statistics recalculation | Regenerate statistics on demand from previously logged data. |
| XML save format | Stores statistics as well-formed XML for easy external-program integration. |
| Compressed (ZIP) save files | Optional zipped save format to reduce disk usage. |
| Save file merging | Consolidates multiple save files into one with recalculated totals. |
| Automatic save-file switching | Switches the active save file automatically when the character changes. |
| Auto-save | Periodically saves to prevent loss of collected statistics. |
| Character accounting / metadata | Tracks character and session metadata (identity, level/progression, resources, affiliation, and server). |
| Configurable buff durations & targeting | User-editable buff duration times and target types recorded in save files, adjustable if changed by the game developers. |
| Customizable message scanning | Tunable parameters controlling which game messages are parsed and logged. |
| Auto-calibration to game patches | Self-adjusts message parsing to the current CoH client, including after patches and on test servers. |
| Per-hero configuration | Hero-specific settings to tweak behavior differently per character. |
| Chat-log parsing engine | Derives all statistics from combat chat messages read from the client log/memory (no network interception). |
| External tracker integration | Interoperates with companion CoH tools: City Game Tracker (online status) and City Info Terminal (badge tracking). |

## GUI features (21)

| Feature | What it does |
| --- | --- |
| Power recharge countdown timers | Live countdown showing time until each power is available again. |
| Buff duration timers | Live countdown timers showing when active buffs/powers will expire. |
| Recent damage received | Latest damage taken, with damage type called out. |
| Status effect sound alerts | Audio cue when a status effect (sleep, hold, stun) is applied. |
| Power recharge sound notifications | Audio cue when a chosen power finishes recharging. |
| Drops by villain scoreboard | Scoreboard of loot drops attributed to the enemy that dropped them. |
| Villains scoreboard | Per enemy summary recording final blows, mutual damage dealt and injuries taken per villain. |
| Custom user-defined statistics | User-defined hero-specific metrics created, tracked, and graphed. |
| Built-in graphing | Plots hero performance statistics over time (e.g. XP and debt), including custom stats. |
| Tabbed statistics interface | Organizes statistics into customizable tabbed categories (Main, Battles, Powers, Graph, Misc, All). |
| In-game overlay UI | Always-on-top real-time overlay showing key stats and timers during fullscreen play. |
| Current system time display | Shows the current real-world clock time in the overlay. |
| Exemplar / sidekick status indicator | Color coding indicating exemplar/sidekick/equal-level state. |
| Leveled-but-not-trained alert | Flags (e.g. turns red) when the character has leveled but not yet trained. |
| Customizable timer filtering | Filter recharge/buff timers by power or minimum duration. |
| Chat message search | Search within chat log and scoreboards via context menu / detail windows. |
| Copy stats to clipboard | Copies statistics and detail windows to the clipboard for external use. |
| Print / report generation | Print and print-preview reports for statistics, chat, villains fought, and drops. |
| Window position memory & auto-launch | Remembers overlay window placement between sessions and can auto-start the window. |
| Automatic update checking | Checks for newer releases of the application. |
| Windows XP theme support | Optional Windows XP visual styling of the UI. |
