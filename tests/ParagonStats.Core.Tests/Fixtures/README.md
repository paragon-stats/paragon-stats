# Log fixtures

Excerpts from real Homecoming chat logs (the operator's own play), sanitized:

- Character, global (`@handle`), other-player, supergroup, and custom-channel names are
  replaced with placeholders (`Nova - PRIME`, `@anon`, `Other Player`, `PlayerOne`,
  `AnonSG`, `ChannelA`/`B`/`C`).
- Player chat *content* is replaced with `redacted`; structure (channel tags, speaker
  markers, `<color #rrggbb>` markup) is preserved.
- Everything else is byte-for-byte real: NPC/foe names, power names, two-space pseudopet
  prefixes, thousands separators, `MISSES!`/`MISSED …!!` punctuation, same-second
  duplicate lines (AoE + DoT ticks + proc rolls — all legitimate, never dedupe), and
  timestamp-less MOTD continuation lines.
- `real-crlf-storm.txt` is a raw byte copy (CRLF intact, no sanitization needed — NPC
  names only) for line-ending handling tests. The other files are LF.

Naming: `real-<topic>.txt` = cut from real logs. Any future `synthetic-<topic>.txt` is a
constructed line for a grammar not yet captured in real play, and must be replaced with a
real excerpt when one is captured.

The full raw log data lives outside the repo (the game install is read-only reference).
The source-data smoke test (`ReplayTests.Source_smoke_reports_uncategorized_ratio_when_configured`) reads `PARAGON_SOURCE_DIR` when set and reports the uncategorized ratio
as the grammar-drift canary.
