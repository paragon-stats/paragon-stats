using System.Globalization;

using ParagonStats.Core.Logging;
using ParagonStats.Core.Parsing;
using ParagonStats.Core.Sessions;
using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tests;

/// <summary>
/// Recalculation (#131): the logs are the source of truth, so re-running the
/// fold over the same files must reproduce the same statistics - twice in a
/// row, whatever order unrelated accounts arrive in, and whether the lines
/// came from a batch replay or from the live watch. There is no separate
/// recompute engine to test: recalculation IS re-running the fold.
/// </summary>
public sealed class RecalculationTests : IDisposable
{
    private const int SessionsPerDay = 4;

    private static readonly string[] Accounts = ["acctA", "acctB", "acctC"];
    private static readonly string[] Days = ["2026-09-01", "2026-09-02"];

    private readonly string _root = Directory.CreateTempSubdirectory("ps-recalc-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Two_replays_of_the_same_files_are_identical_field_by_field()
    {
        string[] files = WriteSource();

        ReplayResult first = LogReplayer.Replay(files);
        ReplayResult second = LogReplayer.Replay(files);

        // Non-vacuity: the source must actually exercise sessions and the
        // unattributed path, or "identical" would be trivially true.
        Assert.Equal(Accounts.Length * Days.Length * SessionsPerDay, first.Sessions.Count);
        Assert.True(first.UnattributedCount > 0);
        AssertSameResult(first, second);
    }

    [Fact]
    public void Unrelated_accounts_interleaving_does_not_change_the_result()
    {
        string[] grouped = WriteSource();

        // Same files, interleaved by day instead of grouped by account. Each
        // account's own chronology is preserved, which is all LogReplayer
        // asks of a caller.
        string[] interleaved = [.. grouped.OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)];
        Assert.NotEqual(grouped, interleaved);

        AssertSameResult(LogReplayer.Replay(grouped), LogReplayer.Replay(interleaved));
    }

    [Fact]
    public void Sessions_sharing_a_start_second_order_by_account_then_character_then_open_order()
    {
        // Same-second sessions are routine, not exotic: a pulse-opened sliver
        // and the banner behind it land in one second, as do back-to-back
        // relogs. 18 sessions is past List.Sort's insertion-sort fallback, so
        // an unstable or partial ordering shows up here.
        string[] characters = ["Nova", "Luna", "Rex"];

        SessionTracker grouped = new();
        foreach (string account in Accounts)
        {
            foreach (string character in characters)
            {
                OpenTwiceInOneSecond(grouped, account, character);
            }
        }

        SessionTracker interleaved = new();
        foreach (string character in characters)
        {
            foreach (string account in Accounts)
            {
                OpenTwiceInOneSecond(interleaved, account, character);
            }
        }

        List<string> expected = [];
        foreach (string account in Accounts)
        {
            foreach (string character in characters.Order(StringComparer.Ordinal))
            {
                // Experience pins the tiebreak direction: the first session
                // opened carries 10, its relog carries 20.
                expected.Add(account + "/" + character + "/10");
                expected.Add(account + "/" + character + "/20");
            }
        }

        Assert.Equal(expected, Describe(grouped));
        Assert.Equal(expected, Describe(interleaved));
    }

    [Fact]
    public void Live_watch_reproduces_the_batch_replay_of_the_same_logs()
    {
        // The plumbing check: every file fits in one poll, so the live path
        // sees the same line order batch does. The ordering guarantee itself
        // is carried by the two tests below, which make the orders diverge.
        // One documented exception to "reproduces exactly" is out of scope
        // here: batch drains a final line with no trailing newline, live does
        // not, because live a newline-less tail is an in-progress write.
        string[] files = WriteSource();
        ReplayResult batch = LogReplayer.Replay(files);

        SessionTracker tracker = new();
        using LogWatcher watcher = new(_root, TimeSpan.FromDays(3650), discoveryInterval: 1);
        LiveMonitor monitor = new(watcher, tracker, static () => true);
        for (int tick = 0; tick < 3; tick++)
        {
            monitor.Tick();
        }

        AssertSameResult(batch, new ReplayResult(tracker.Sessions, tracker.UnattributedCount, [.. watcher.Unreadable]));
    }

    [Fact]
    public void Live_watch_keeps_an_accounts_files_in_order_across_the_line_cap()
    {
        // A tailer stops at its per-poll line cap, so an account whose older
        // log has a big backlog must not have its newer log read first: that
        // would hand the tracker one account's lines out of order and fold the
        // backlog into the wrong session. Watch started shortly after midnight
        // rollover, after a long farming day, is exactly this shape.
        List<string> busy = ["2026-09-01 10:00:00 Welcome to City of Heroes, Nova!"];
        for (int line = 0; line < 51_000; line++)
        {
            busy.Add("2026-09-01 10:00:01 You gain 1 experience.");
        }

        List<string> files =
        [
            Write("acctA", Days[0], busy),
            Write("acctA", Days[^1], DayLines(Days[^1], leadWithUnattributed: false)),
        ];

        SessionTracker tracker = new();
        using LogWatcher watcher = new(_root, TimeSpan.FromDays(3650), discoveryInterval: 1);
        LiveMonitor monitor = new(watcher, tracker, static () => true);
        for (int tick = 0; tick < 6; tick++)
        {
            monitor.Tick();
        }

        AssertSameResult(
            LogReplayer.Replay(files),
            new ReplayResult(tracker.Sessions, tracker.UnattributedCount, [.. watcher.Unreadable]));
    }

    [Fact]
    public void Live_watch_matches_batch_when_the_logs_arrive_in_pieces()
    {
        // The live path closes sessions interleaved across accounts per tick,
        // while batch closes them a file at a time - so the two only agree if
        // session order is a property of content, not of arrival.
        // Only the newest file grows, which is all the game ever does: past
        // days are closed history. Delivering an older file's tail after a
        // newer file has been read would be out-of-order input, which no
        // ordering rule can reconcile and which never happens in practice.
        Dictionary<string, List<string>> pending = new(StringComparer.Ordinal);
        List<string> files = [];
        foreach (string account in Accounts)
        {
            files.Add(Write(account, Days[0], DayLines(Days[0], leadWithUnattributed: true)));

            List<string> latest = DayLines(Days[^1], leadWithUnattributed: false);
            string path = Write(account, Days[^1], latest.Take(latest.Count / 2));
            pending[path] = [.. latest.Skip(latest.Count / 2)];
            files.Add(path);
        }

        SessionTracker tracker = new();
        using LogWatcher watcher = new(_root, TimeSpan.FromDays(3650), discoveryInterval: 1);
        LiveMonitor monitor = new(watcher, tracker, static () => true);
        monitor.Tick();

        // Append immediately before the tick that reads it: never leave an
        // empty poll between a write and its read, which would arm the
        // tailer's stale-reopen counter.
        foreach ((string path, List<string> rest) in pending)
        {
            File.AppendAllText(path, string.Join('\n', rest) + '\n');
        }

        monitor.Tick();
        monitor.Tick();

        files.Sort(StringComparer.Ordinal);
        AssertSameResult(
            LogReplayer.Replay(files),
            new ReplayResult(tracker.Sessions, tracker.UnattributedCount, [.. watcher.Unreadable]));
    }

    [Fact]
    public void Cli_rerun_recomputes_from_disk_with_no_cached_state()
    {
        string[] files = WriteSource();
        CliEnvironment environment = new() { ConfigPath = Path.Join(_root, "config", "config.json") };
        using StringWriter error = new();
        using StringWriter first = new();
        using StringWriter second = new();

        Assert.Equal(0, CliRunner.Run([_root], first, error, environment));
        Assert.Equal(0, CliRunner.Run([_root], second, error, environment));
        Assert.Equal(first.ToString(), second.ToString());

        // Nothing is cached between runs: change the logs and the next run
        // reports the change. Append to the FIRST account's NEWEST file so the
        // fixture keeps the per-account chronology LogReplayer asks for.
        File.AppendAllText(
            files[1],
            "2026-09-03 12:00:00 Welcome to City of Heroes, Afterwards!\n2026-09-03 12:00:05 You gain 500 experience.\n");

        using StringWriter third = new();
        Assert.Equal(0, CliRunner.Run([_root], third, error, environment));
        Assert.NotEqual(second.ToString(), third.ToString(), StringComparer.Ordinal);
        Assert.Contains("Afterwards", third.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Source_replay_is_deterministic_when_configured()
    {
        string? source = Environment.GetEnvironmentVariable("PARAGON_SOURCE_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(source), "PARAGON_SOURCE_DIR not set");

        string[] files = Directory.GetFiles(source!, "chatlog*.txt", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);

        ReplayResult first = LogReplayer.Replay(files);
        Assert.True(first.Sessions.Count > 0);
        AssertSameResult(first, LogReplayer.Replay(files));
    }

    private static void OpenTwiceInOneSecond(SessionTracker tracker, string account, string character)
    {
        const string stamp = "2026-09-01 08:00:00 ";
        tracker.Accept(account, stamp + "Welcome to City of Heroes, " + character + "!");
        tracker.Accept(account, stamp + "You gain 10 experience.");
        tracker.Accept(account, stamp + "Welcome to City of Heroes, " + character + "!");
        tracker.Accept(account, stamp + "You gain 20 experience.");
    }

    private static List<string> Describe(SessionTracker tracker) =>
    [
        .. tracker.Sessions.Select(session => string.Create(
            CultureInfo.InvariantCulture,
            $"{session.Account}/{session.Character}/{session.Stats.Experience}")),
    ];

    private static string Stamp(string day, int hour, int minute, int second) =>
        string.Create(CultureInfo.InvariantCulture, $"{day} {hour:00}:{minute:00}:{second:00} ");

    private static List<string> DayLines(string day, bool leadWithUnattributed)
    {
        List<string> lines = [];
        if (leadWithUnattributed)
        {
            lines.Add(Stamp(day, 9, 59, 0) + "You gain 5 experience.");
        }

        for (int index = 0; index < SessionsPerDay; index++)
        {
            int minute = index * 10;
            string character = "Nova" + index.ToString(CultureInfo.InvariantCulture);
            lines.Add(Stamp(day, 10, minute, 0) + "Welcome to City of Heroes, " + character + "!");
            lines.Add(Stamp(day, 10, minute, 5) + "HIT " + character + "! Your Health power is autohit.");
            lines.Add(Stamp(day, 10, minute, 10) + "[Tell] :Someone: refused text");
            lines.Add(Stamp(day, 10, minute, 15) + "You hit Council Blaster with your Fire Blast for 12.5 points of Fire damage.");
            lines.Add(Stamp(day, 10, minute, 20) + "You have defeated Council Blaster");
            lines.Add(Stamp(day, 10, minute, 25) + "You gain 100 experience and 50 influence.");
            lines.Add(Stamp(day, 10, minute, 30) + "You earned 7 architect tickets!");
            lines.Add(Stamp(day, 10, minute, 35) + "You got 1,000 influence from the Consignment House.");
            lines.Add(Stamp(day, 10, minute, 40) + "You paid 250 to the Consignment House.");
            lines.Add(Stamp(day, 10, minute, 45) + "Entering Bronze Way.");
            lines.Add(Stamp(day, 10, minute, 50) + "You are flying!");
            lines.Add(Stamp(day, 10, minute, 55) + "You activated the Hasten power.");
        }

        return lines;
    }

    private static void AssertSameResult(ReplayResult expected, ReplayResult actual)
    {
        Assert.Equal(expected.UnattributedCount, actual.UnattributedCount);
        Assert.Equal(expected.SkippedFiles, actual.SkippedFiles);
        Assert.Equal(expected.Sessions.Count, actual.Sessions.Count);

        for (int index = 0; index < expected.Sessions.Count; index++)
        {
            CharacterSession left = expected.Sessions[index];
            CharacterSession right = actual.Sessions[index];
            Assert.Equal(left.Account, right.Account);
            Assert.Equal(left.Character, right.Character);
            Assert.Equal(left.Start, right.Start);
            Assert.Equal(left.LastSeen, right.LastSeen);
            Assert.Equal(left.Stats.Experience, right.Stats.Experience);
            Assert.Equal(left.Stats.Influence, right.Stats.Influence);
            Assert.Equal(left.Stats.Defeats, right.Stats.Defeats);
            Assert.Equal(left.Stats.Activations, right.Stats.Activations);
            Assert.Equal(left.Stats.Tickets, right.Stats.Tickets);
            Assert.Equal(left.Stats.MarketIncome, right.Stats.MarketIncome);
            Assert.Equal(left.Stats.MarketSpent, right.Stats.MarketSpent);
            Assert.Equal(left.Stats.TotalDamage, right.Stats.TotalDamage);
            Assert.Equal(Categories(left.Stats.CategoryCounts), Categories(right.Stats.CategoryCounts));
            Assert.Equal(Powers(left.Stats.DamageByPower), Powers(right.Stats.DamageByPower));
            Assert.Equal(left.Messages.TotalCaptured, right.Messages.TotalCaptured);
            Assert.Equal(left.Messages.Messages, right.Messages.Messages);
        }

        // The user-visible oracle, on top of the field-by-field comparison.
        Assert.Equal(SummaryFormatter.Format(expected), SummaryFormatter.Format(actual));
    }

    /// <summary>Dictionary iteration order is an implementation detail; the counts are the contract.</summary>
    private static List<KeyValuePair<EventCategory, long>> Categories(IReadOnlyDictionary<EventCategory, long> counts) =>
        [.. counts.OrderBy(entry => entry.Key)];

    /// <summary>Never rendered anywhere, so these assertions are its only guard.</summary>
    private static List<KeyValuePair<string, decimal>> Powers(IReadOnlyDictionary<string, decimal> damage) =>
        [.. damage.OrderBy(entry => entry.Key, StringComparer.Ordinal)];

    private string Write(string account, string day, IEnumerable<string> lines)
    {
        string directory = Path.Join(_root, account, "Logs");
        Directory.CreateDirectory(directory);
        string path = Path.Join(directory, "chatlog " + day + ".txt");
        File.WriteAllText(path, string.Join('\n', lines) + '\n');
        return path;
    }

    private string[] WriteSource()
    {
        List<string> files = [];
        foreach (string account in Accounts)
        {
            foreach (string day in Days)
            {
                bool lead = string.Equals(day, Days[0], StringComparison.Ordinal);
                files.Add(Write(account, day, DayLines(day, lead)));
            }
        }

        files.Sort(StringComparer.Ordinal);
        return [.. files];
    }
}
