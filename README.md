# paragon-stats

A stats tool for *City of Heroes*. Clean-room reimplementation inspired by HeroStats.

**Status: alpha.** Batch chatlog analysis and live session watching work;
metrics, farm-economy tracking, and session identity are in. Product plan and
requirements: [docs/PRD.md](docs/PRD.md).

> **Built with AI assistance.** Developed with the help of AI coding tools
> (Claude Code); all contributions are human-reviewed before merge. You're
> welcome to use your own AI tools — committed AI meta-docs are not (see the
> [AI-assistance policy](docs/style-guides/ai-assistance-policy.md)).

## Getting started

- **Requirements**: .NET 10 SDK (Windows x64)
- **Build**: `dotnet build`
- **Test**: `dotnet test`

## Status

| | |
| --- | --- |
| Language | C# / .NET 10 LTS |
| Platform | Windows x64 (native AOT) |
| License | Apache-2.0 |

## Usage

```text
paragon-stats [--watch] [chatlog-file-or-game-directory]
```

Point it at a chatlog file or your Homecoming install; it prints per-character
session summaries (damage, defeats, XP, influence, tickets, market, rates, line
categories). `--watch` follows the live logs while you play and prints rolling
per-session rates. With no path it uses the saved game location, prompting on
first launch. Enable in-game logging first: Options > Windows > Chat > Log Chat.

Re-running recalculates from scratch over the logs on disk - nothing is cached,
so the same files always produce the same numbers.

### Collection policy

paragon-stats harvests **data channels only**, and refuses anything it
cannot positively classify as a data line. Communication channels (tells,
team, supergroup, league, local, broadcast, global channels) are not collected
at all: the parser dumps every bracketed communication line outright - no
event, no capture, no count, in memory or anywhere else. No use case justifies
collecting what players say to each other, and this tool cannot.

What data lines *do* carry, stated plainly: other players' character names
appear in the game's own combat text (a teammate's killing blow, a foe you
damaged, a pet's name), and those lines are kept because team and farm stats
are computed from them. Nothing is retained from a communication channel.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[Apache-2.0](LICENSE).

Inspired by HeroStats by `ineffablebob`, `lberger`, `msawczyn`, `lpfjones`,
`thesteinerd`.

*City of Heroes* is a trademark of NCSOFT. paragon-stats is not affiliated with or
endorsed by NCSOFT or Homecoming Servers, LLC.
