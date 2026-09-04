# Gameplay mechanics, as the logs record them

Findings from replaying real Homecoming logs and from guided sessions run to
answer specific questions. Everything here is measured, not looked up: the
figures come from the operator's own logs, and where a claim rests on a single
controlled run that is said out loud.

This exists because the numbers the tool reports only mean something if you know
what the game was doing when it wrote them.

## Rewards

### Awards come in fixed multiples of the minion award

Across 244 log files, XP awards cluster hard onto multiples of the smallest
common award, matching the game's spawn ranks:

| Multiple | Example (XP) | Rank |
| --- | --- | --- |
| ×1 | 2,024 | Minion |
| ×2 | 4,047 | Lieutenant |
| ×4 | 8,094 | — |
| ×6 | 12,142 | Boss |
| ×12 | 24,283 | Elite boss / AV |

The base value scales with your level and the enemy's, so the absolute numbers
move; the ladder does not.

### Influence is not a fixed ratio of XP

The same XP award appears with different influence attached — 4,047 XP pairs
with 5,664 influence 397 times and with 9,713 influence 891 times in the same
corpus (1.4× and 2.4×). Influence and XP are rolled against separate tables, so
an influence-per-hour figure that looks off against XP-per-hour is not
necessarily a defect. Reward-table randomisation accounts for small
session-to-session deltas that no mechanic explains.

### Team drops divide by team size

Measured directly: the same map, same missions, run solo and then two-boxed. Per
character, rewards fell by **2.0001×** on the two-box run — an exact halving
within measurement noise.

The consequence is blunt and worth stating, because it is the opposite of what
multiboxing intuitively promises: **two boxes on the same team farm no faster
than one.** The reward is split, not duplicated. Multiboxing pays only when the
boxes are on separate teams, or for reasons other than throughput.

## Damage

### Powers with multiple effects log each effect separately

A single activation of a power like Ion Core Final Judgement writes several
damage lines, one per damage type it deals. A per-power total is the sum of its
effect lines; a per-effect breakdown is available from the same data. Both are
post-session analysis — deriving them live would cost the slim always-running
binary far more than the readout is worth.

### What the logs will not give you

- **Teammate damage is never written.** Not at any range, not in any channel.
- **Teammate defeats are 61–73% observable**, varying with team spread.
- **Per-hit detail needs proximity.** Characters far apart stop producing the
  detailed damage lines, so a distant box under-reports.

A "damage" total from these logs is therefore *your* damage plus *your pets'*,
and comparing two boxes' totals only works when they were fighting together.

## Incarnate powers

Questions asked during guided sessions, answered from the logs:

- **Musculature Core Paragon** (always-on damage boost) shows up as a uniform
  lift across every damage line, which is what an always-on enhancement should
  look like.
- **Assault Radial Embodiment** (toggle, partially bypasses Enhancement
  Diversification) is not separable from the baseline by log inspection alone.
  The logs record damage dealt, not the enhancement stack that produced it, so
  isolating its contribution needs a controlled A/B run with the toggle off.
- **Ageless** is a click PBAoE that buffs everyone in the vicinity regardless of
  team or league membership. This is why its autohit lines name strangers, and
  why they are useless for identity — see
  [who earned it](log-pipeline.md#who-earned-it).

## Powers, damage types and geometry

Typing every power to a damage geometry — melee single-target, cone, PBAoE — and
to its damage types is what turns per-power damage into something comparable
across builds. Deriving that from log observation alone is possible but
expensive and gap-prone.

The provable route is the game's own data: extract the power tables from the
PIGG archives for every archetype and powerset variant, and type each power from
the source. That also yields max-targets-hit for the AoE geometries. It is a
post-session analysis component, not a live one, and is tracked as part of the
metrics checkpoint.

## Stability

The v0.6.0 binary was monitored across a full evening of multibox farming —
103,000 lines processed — with no growth in memory, handle count or thread
count. Attribution was the only class of defect found in that testing; the
arithmetic reconciled exactly against independently written regexes over the same
logs, for XP, influence, damage and activations.
