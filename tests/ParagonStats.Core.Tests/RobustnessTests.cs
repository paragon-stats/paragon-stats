using System.Globalization;

using ParagonStats.Core.Logging;
using ParagonStats.Core.Parsing;
using ParagonStats.Core.Sessions;
using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tests;

/// <summary>Review-wave behaviors: unreadable files, account keying, parse fallbacks, formatter edges.</summary>
public sealed class RobustnessTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ps-tests-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteLog(string relative, params string[] lines)
    {
        string path = Path.Join(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void Locked_file_is_skipped_and_reported_not_fatal()
    {
        string good = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2024-05-12.txt"),
            "2024-05-12 08:00:00 Welcome to City of Heroes, Nova!",
            "2024-05-12 08:00:05 You gain 100 experience.");
        string locked = WriteLog(Path.Join("acct", "Logs", "chatlog 2024-05-13.txt"), "2024-05-13 08:00:00 locked");

        using FileStream writer = new(locked, FileMode.Open, FileAccess.Write, FileShare.None);
        ReplayResult result = LogReplayer.Replay([good, locked]);

        Assert.Equal(locked, Assert.Single(result.SkippedFiles));
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.Equal(100, session.Stats.Experience);
        Assert.Contains("skipped (unreadable)", SummaryFormatter.Format(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_directory_reports_no_files()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        string empty = Path.Join(_root, "empty");
        Directory.CreateDirectory(empty);

        Assert.Equal(1, CliRunner.Run([empty], output, error));
        Assert.Contains("no chatlog files", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void File_held_open_for_writing_by_the_game_still_replays()
    {
        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2024-05-12.txt"),
            "2024-05-12 08:00:00 Welcome to City of Heroes, Nova!",
            "2024-05-12 08:00:05 You gain 100 experience.");

        // The live client keeps today's log open for appending; the replay's
        // ReadWrite|Delete share must coexist with that writer handle.
        using FileStream writer = new(log, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        ReplayResult result = LogReplayer.Replay([log]);

        Assert.Empty(result.SkippedFiles);
        Assert.Equal(100, Assert.Single(result.Sessions).Stats.Experience);
    }

    [Fact]
    public void Unreadable_path_is_skipped_like_a_locked_file()
    {
        // A directory path in the file list throws UnauthorizedAccessException on open.
        string dir = Path.Join(_root, "not-a-file");
        Directory.CreateDirectory(dir);

        ReplayResult result = LogReplayer.Replay([dir]);
        Assert.Equal(dir, Assert.Single(result.SkippedFiles));
    }

    [Fact]
    public void Files_outside_the_logs_shape_key_on_their_own_directory()
    {
        string first = WriteLog(Path.Join("flatA", "chatlog 2024-05-12.txt"), "2024-05-12 08:00:00 Welcome to City of Heroes, Alpha!");
        string second = WriteLog(Path.Join("flatB", "chatlog 2024-05-12.txt"), "2024-05-12 09:00:00 Welcome to City of Heroes, Beta!");

        ReplayResult result = LogReplayer.Replay([first, second]);

        Assert.Equal(2, result.Sessions.Count);
        Assert.NotEqual(result.Sessions[0].Account, result.Sessions[1].Account, StringComparer.Ordinal);
    }

    [Fact]
    public void Second_banner_closes_the_first_session()
    {
        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2024-05-12.txt"),
            "2024-05-12 08:00:00 Welcome to City of Heroes, Nova!",
            "2024-05-12 08:10:00 You gain 10 experience.",
            "2024-05-12 09:00:00 Welcome to City of Heroes, Luna!",
            "2024-05-12 09:10:00 You gain 20 experience.");

        ReplayResult result = LogReplayer.Replay([log]);

        Assert.Equal(2, result.Sessions.Count);
        Assert.Equal(10, result.Sessions[0].Stats.Experience);
        Assert.Equal(20, result.Sessions[1].Stats.Experience);
    }

    [Fact]
    public void Idle_gap_closes_the_session_and_bannerless_lines_wait_unattributed()
    {
        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2024-05-12.txt"),
            "2024-05-12 08:00:00 Welcome to City of Heroes, Nova!",
            "2024-05-12 08:05:00 You gain 10 experience.",
            "2024-05-12 09:00:00 You gain 20 experience.", // 55 min silent: logged out; who is this? Wait for a banner.
            "2024-05-12 09:01:00 Welcome to City of Heroes, Luna!",
            "2024-05-12 09:02:00 You gain 30 experience.");

        ReplayResult result = LogReplayer.Replay([log]);

        Assert.Equal(2, result.Sessions.Count);
        Assert.Equal(10, result.Sessions[0].Stats.Experience);
        Assert.Equal(new DateTime(2024, 5, 12, 8, 5, 0), result.Sessions[0].LastSeen);
        Assert.Equal(1, result.UnattributedCount);
        Assert.Equal("Luna", result.Sessions[1].Character);
        Assert.Equal(30, result.Sessions[1].Stats.Experience);
    }

    [Fact]
    public void Gap_under_the_idle_timeout_stays_one_session()
    {
        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2024-05-12.txt"),
            "2024-05-12 08:00:00 Welcome to City of Heroes, Nova!",
            "2024-05-12 08:29:00 You gain 10 experience."); // 29 min: in-game idle, not a logout

        ReplayResult result = LogReplayer.Replay([log]);
        Assert.Equal(10, Assert.Single(result.Sessions).Stats.Experience);
    }

    [Fact]
    public void Heartbeat_after_idle_gap_opens_the_session_it_identifies()
    {
        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2026-08-31.txt"),
            "2026-08-31 08:00:00 Welcome to City of Heroes, Nova!",
            "2026-08-31 08:05:00 You gain 10 experience.",
            "2026-08-31 09:00:00 You are flying!",                          // post-gap: Nova is provably gone
            "2026-08-31 09:00:01 HIT Luna! Your Health power is autohit.",  // proof: Luna is active
            "2026-08-31 09:00:02 You gain 20 experience.");

        ReplayResult result = LogReplayer.Replay([log]);

        // The 55-minute silence closed Nova by the rule this tracker applies
        // everywhere else: past IdleTimeout the character is logged out. So the
        // 09:00:00 line cannot be Nova's, and the pulse a second later names who
        // was seated. It is adopted, and the session starts where the play began
        // rather than where the proof landed (#251).
        Assert.Equal(2, result.Sessions.Count);
        Assert.Equal(0, result.UnattributedCount);
        Assert.Equal("Luna", result.Sessions[1].Character);
        Assert.Equal(20, result.Sessions[1].Stats.Experience);
        Assert.Equal(new DateTime(2026, 8, 31, 9, 0, 0), result.Sessions[1].Start);
    }

    [Fact]
    public void An_idle_gap_inside_the_held_events_fences_off_what_precedes_it()
    {
        // The mirror of the case above. Activity, a proven logout, more
        // activity, then a pulse: only the burst on the pulse's side of the
        // fence is adopted. The far side stays unattributed, because a silence
        // past IdleTimeout means whoever earned it had already gone.
        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2026-08-31.txt"),
            "2026-08-31 08:00:00 You gain 11 experience.",
            "2026-08-31 09:30:00 You gain 22 experience.",
            "2026-08-31 09:30:01 HIT Luna! Your Health power is autohit.");

        ReplayResult result = LogReplayer.Replay([log]);

        CharacterSession session = Assert.Single(result.Sessions);
        Assert.Equal("Luna", session.Character);
        Assert.Equal(22, session.Stats.Experience);
        Assert.Equal(new DateTime(2026, 8, 31, 9, 30, 0), session.Start);
        Assert.Equal(1, result.UnattributedCount);
    }

    [Fact]
    public void Heartbeat_naming_a_different_character_switches_the_session()
    {
        // The observed banner-lag swap: the new character's lines precede its
        // banner; the heartbeat closes the old session at the swap point.
        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2026-08-31.txt"),
            "2026-08-31 20:00:07 Welcome to City of Heroes, Laser - PRIME!",
            "2026-08-31 20:00:55 You gain 10 experience.",
            "2026-08-31 20:01:21 HIT Laser - SPARK! Your Health power is autohit.",
            "2026-08-31 20:01:21 Welcome to City of Heroes, Laser - SPARK!",
            "2026-08-31 20:01:23 You gain 20 experience.");

        ReplayResult result = LogReplayer.Replay([log]);

        // PRIME, the pulse-opened SPARK sliver, and the banner-opened SPARK session.
        Assert.Equal(3, result.Sessions.Count);
        Assert.Equal(10, result.Sessions[0].Stats.Experience);
        Assert.Equal(new DateTime(2026, 8, 31, 20, 0, 55), result.Sessions[0].LastSeen);
        Assert.Equal("Laser - SPARK", result.Sessions[1].Character);
        Assert.Equal("Laser - SPARK", result.Sessions[2].Character);
        Assert.Equal(20, result.Sessions[2].Stats.Experience);
    }

    [Fact]
    public void Heartbeat_naming_the_current_character_does_not_split_the_session()
    {
        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2026-08-31.txt"),
            "2026-08-31 08:00:00 Welcome to City of Heroes, Nova!",
            "2026-08-31 08:00:15 HIT Nova! Your Health power is autohit.",
            "2026-08-31 08:00:30 Nova HITS you! Stamina power was autohit.",
            "2026-08-31 08:00:45 You gain 10 experience.");

        ReplayResult result = LogReplayer.Replay([log]);

        CharacterSession session = Assert.Single(result.Sessions);
        Assert.Equal(10, session.Stats.Experience);
        Assert.Equal(2, session.Stats.CategoryCounts[EventCategory.Identity]);
    }

    [Fact]
    public void Replay_counts_a_final_line_without_a_trailing_newline()
    {
        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2024-05-12.txt"),
            "2024-05-12 08:00:00 Welcome to City of Heroes, Nova!");
        File.AppendAllText(log, "2024-05-12 08:05:00 You gain 10 experience."); // no newline: still a complete line on disk

        ReplayResult result = LogReplayer.Replay([log]);
        Assert.Equal(10, Assert.Single(result.Sessions).Stats.Experience);
    }

    [Fact]
    public void Raw_line_feed_reports_refused_and_skipped_lines_as_uncollected()
    {
        SessionTracker tracker = new();
        Assert.False(tracker.Accept("acct", "no timestamp here"));
        Assert.False(tracker.Accept("acct", "2024-05-12 08:00:00 [Tell] :x: y"));
        Assert.True(tracker.Accept("acct", "2024-05-12 08:00:00 Welcome to City of Heroes, Nova!"));
    }

    [Fact]
    public void Backwards_timestamps_render_a_clamped_duration()
    {
        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2024-11-03.txt"),
            "2024-11-03 01:30:00 Welcome to City of Heroes, Nova!",
            "2024-11-03 01:05:00 You gain 10 experience."); // DST fall-back: naive local time runs backwards

        string text = SummaryFormatter.Format(LogReplayer.Replay([log]));
        Assert.Contains("+00:00:00", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_ascii_names_are_sanitized_in_the_summary()
    {
        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2024-05-12.txt"),
            "2024-05-12 08:00:00 Welcome to City of Heroes, Ñova!");

        string text = SummaryFormatter.Format(LogReplayer.Replay([log]));
        Assert.Contains("?ova", text, StringComparison.Ordinal);
        Assert.All(text, symbol => Assert.True(symbol is '\r' or '\n' || (symbol >= ' ' && symbol <= '~'), string.Create(CultureInfo.InvariantCulture, $"non-ASCII char: {(int)symbol}")));
    }

    [Fact]
    public void Pseudopet_prefix_applies_to_damage_only()
    {
        Assert.True(LineParser.TryParse(new LogLine(new DateTime(2024, 5, 12, 8, 0, 0), "Fire Imp:  You have defeated Council Blaster"), out LogEvent parsed));
        Assert.IsType<UncategorizedLine>(parsed); // never credited to the player
    }

    [Fact]
    public void Message_log_ring_drops_oldest_beyond_capacity()
    {
        MessageLog log = new();
        for (int i = 0; i <= MessageLog.Capacity; i++)
        {
            log.Add(new DateTime(2024, 5, 12, 8, 0, 0), EventCategory.Uncategorized, string.Create(CultureInfo.InvariantCulture, $"line {i}"));
        }

        Assert.Equal(MessageLog.Capacity, log.Messages.Count);
        Assert.Equal(MessageLog.Capacity + 1, log.TotalCaptured);

        // The OLDEST entry is the one dropped: "line 0" gone, newest retained.
        Assert.Equal("line 1", log.Messages.First().Payload);
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"line {MessageLog.Capacity}"), log.Messages.Last().Payload);
    }

    [Fact]
    public void Stats_fold_covers_teammate_defeats_and_unknown_events()
    {
        SessionStats stats = new();
        stats.Apply(new Defeat("Teammate", "Foe"));
        stats.Apply(new Defeat(Attacker: null, "Foe"));
        stats.Apply(new UncategorizedLine("anything"));
        stats.Apply(new TicketsEarned(12));
        stats.Apply(new MarketTransaction(1000, Income: true));
        stats.Apply(new MarketTransaction(400, Income: false));

        Assert.Equal(1, stats.Defeats); // own killing blows only
        Assert.Equal(2, stats.CategoryCounts[EventCategory.Defeat]);
        Assert.Equal(12, stats.Tickets);
        Assert.Equal(1000, stats.MarketIncome); // never folded into combat influence
        Assert.Equal(400, stats.MarketSpent);
        Assert.Equal(0, stats.Influence);
    }

    [Fact]
    public void Cli_runner_handles_usage_missing_and_happy_paths()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        CliEnvironment env = new() { ConfigPath = Path.Join(_root, "config.json") };

        Assert.Equal(2, CliRunner.Run(["a", "b"], output, error, env));
        Assert.Contains("usage:", error.ToString(), StringComparison.Ordinal);

        Assert.Equal(1, CliRunner.Run([Path.Join(_root, "nope")], output, error, env));

        string log = WriteLog(
            Path.Join("acct", "Logs", "chatlog 2024-05-12.txt"),
            "2024-05-12 08:00:00 Welcome to City of Heroes, Nova!");
        Assert.Equal(0, CliRunner.Run([log], output, error, env));
        Assert.Equal(0, CliRunner.Run([_root], output, error, env));
        Assert.Contains("Nova", output.ToString(), StringComparison.Ordinal);
    }
}
