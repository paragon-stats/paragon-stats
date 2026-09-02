using System.Diagnostics;
using System.Globalization;
using System.Text;

using ParagonStats.Core.Logging;
using ParagonStats.Core.Parsing;
using ParagonStats.Core.Sessions;
using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tests;

/// <summary>
/// Hostile-input regression: a chatlog is untrusted input (a player may be
/// handed one, or edit their own), so every test here feeds a crafted file and
/// asserts the tool degrades instead of crashing, hanging, over-consuming, or
/// leaking. Cases are named for the weakness they cover - CWE first, with the
/// matching MITRE ATT&amp;CK technique where a real adversary behaviour applies,
/// and OWASP ASVS V5 (validation) / V12 (files) as the control family.
/// Stated limitation, not a defect: the logs ARE the source of truth, so a
/// forged log yields forged statistics. The guarantee is safety of the
/// process and the user, never authenticity of another party's log.
/// </summary>
public sealed class MaliciousLogTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ps-malicious-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>CWE-1333 inefficient regular expression complexity / ATT&amp;CK T1499 endpoint DoS.</summary>
    [Fact]
    public void Backtracking_bait_lines_cannot_hang_the_parser()
    {
        // Verified against the source-generated matcher, not guessed:
        // repeating the damage grammar's own separator explodes the split
        // space (60k repeats measured at 996ms, just under the 1s ceiling -
        // this one is far past it), while a single long run finishes in
        // milliseconds. The per-pattern timeout must surface as a miss, not
        // as an uncaught RegexMatchTimeoutException that kills the run.
        string bait = "You hit " + string.Concat(Enumerable.Repeat("a with your ", 150_000)) + " for 1 points of X damage";

        Stopwatch clock = Stopwatch.StartNew();
        Assert.True(LineParser.TryParse(new LogLine(new DateTime(2026, 9, 1, 12, 0, 0), bait), out LogEvent parsed));
        Assert.IsType<UncategorizedLine>(parsed);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"parsing took {clock.Elapsed}");
    }

    /// <summary>CWE-190 integer overflow or wraparound (ASVS V5.2).</summary>
    [Theory]
    [InlineData("You gain 99999999999999999999999999 experience.")]
    [InlineData("You gain 999999999999999999999999 experience and 888888888888888888888888 influence.")]
    [InlineData("You earned 99999999999999999999999999 architect tickets!")]
    [InlineData("You got 99999999999999999999999999 influence from the Consignment House.")]
    [InlineData("You hit Foe with your Power for 99999999999999999999999999999999 points of Fire damage.")]
    public void Numbers_too_large_to_be_real_do_not_overflow_the_fold(string payload)
    {
        Assert.True(LineParser.TryParse(new LogLine(new DateTime(2026, 9, 1, 12, 0, 0), payload), out LogEvent parsed));
        Assert.IsType<UncategorizedLine>(parsed);

        SessionStats stats = new();
        stats.Apply(parsed);
        Assert.Equal(0, stats.Experience);
        Assert.Equal(0, stats.Influence);
        Assert.Equal(0, stats.Tickets);
        Assert.Equal(0m, stats.TotalDamage);
    }

    /// <summary>CWE-400 uncontrolled resource consumption / ATT&amp;CK T1499 (ASVS V12.1).</summary>
    [Fact]
    public void A_huge_file_is_read_in_bounded_slices()
    {
        string path = Path.Join(_root, "chatlog 2026-09-01.txt");
        StringBuilder content = new();
        content.AppendLine("2026-09-01 12:00:00 Welcome to City of Heroes, Nova!");
        for (int i = 0; i < 60_000; i++)
        {
            content.AppendLine("2026-09-01 12:00:01 You gain 1 experience.");
        }

        File.WriteAllText(path, content.ToString());

        using ChatLogTailer tailer = new(path);

        // Bounded per poll: the cap plus at most one 8KB read chunk, never
        // the whole file.
        IReadOnlyList<string> first = tailer.Poll();
        Assert.True(first.Count <= 50_000 + 8192, $"one poll materialized {first.Count} lines");

        long total = first.Count;
        for (IReadOnlyList<string> next = tailer.Poll(); next.Count > 0; next = tailer.Poll())
        {
            total += next.Count;
        }

        Assert.Equal(60_001, total); // every line still arrives, just not at once
    }

    /// <summary>CWE-117 improper output neutralization for logs / terminal escape injection (ASVS V5.3).</summary>
    [Fact]
    public void Control_sequences_in_a_name_cannot_reach_the_terminal()
    {
        // A crafted character name carrying ANSI escapes would otherwise be
        // replayed into the user's terminal by the summary.
        string path = Path.Join(_root, "acct", "Logs", "chatlog 2026-09-01.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            "2026-09-01 12:00:00 Welcome to City of Heroes, \u001B[2J\u001B]0;pwned\u0007Nova\u0000!\n2026-09-01 12:00:05 You gain 10 experience.\n");

        string text = SummaryFormatter.Format(LogReplayer.Replay([path]));

        Assert.DoesNotContain('\u001B', text); // ESC
        Assert.DoesNotContain('\u0000', text);
        Assert.All(text, symbol => Assert.True(symbol is '\r' or '\n' || (symbol >= ' ' && symbol <= '~'), "non-printable in output"));
    }

    /// <summary>CWE-20 improper input validation (ASVS V5.1): shapes that look like data but are not.</summary>
    [Theory]
    [InlineData("2026-13-45 99:99:99 You gain 10 experience.")] // impossible date
    [InlineData("0000-00-00 00:00:00 You gain 10 experience.")]
    [InlineData("2026-09-01T12:00:00 You gain 10 experience.")] // wrong separator
    [InlineData("2026-09-01 12:00:00")] // timestamp only
    [InlineData("")]
    public void Malformed_lines_are_skipped_without_throwing(string raw)
    {
        SessionTracker tracker = new();
        Assert.False(tracker.Accept("acct", raw));
        Assert.Empty(tracker.Sessions);
    }

    /// <summary>CWE-20 (ASVS V5.1): binary and invalid-encoding payloads.</summary>
    [Fact]
    public void Binary_garbage_and_invalid_utf8_do_not_throw()
    {
        string path = Path.Join(_root, "acct", "Logs", "chatlog 2026-09-02.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        List<byte> bytes =
        [
            .. Encoding.UTF8.GetBytes("2026-09-01 12:00:00 Welcome to City of Heroes, Nova!\n"),
            0xFF, 0xFE, 0x00, 0x01, 0x80, 0x81, (byte)'\n', // lone surrogates / invalid sequences
            .. Encoding.UTF8.GetBytes("2026-09-01 12:00:05 You gain 10 experience.\n"),
        ];
        File.WriteAllBytes(path, [.. bytes]);

        ReplayResult result = LogReplayer.Replay([path]);
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.Equal(10, session.Stats.Experience); // the real lines still parse
    }

    /// <summary>CWE-772 missing release of resource (ASVS V12.4).</summary>
    [Fact]
    public void Disposing_the_reader_releases_the_file()
    {
        string path = Path.Join(_root, "chatlog 2026-09-03.txt");
        File.WriteAllText(path, "2026-09-01 12:00:00 You gain 10 experience.\n");

        ChatLogTailer tailer = new(path);
        tailer.Poll();
        tailer.Dispose();

        File.Delete(path); // throws if a handle leaked
        Assert.False(File.Exists(path));
    }

    /// <summary>CWE-359 exposure of private information / ATT&amp;CK T1005 (the zero-collection ruling).</summary>
    [Fact]
    public void A_log_stuffed_with_communications_yields_none_of_it()
    {
        string path = Path.Join(_root, "acct", "Logs", "chatlog 2026-09-04.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        StringBuilder content = new();
        content.AppendLine("2026-09-01 12:00:00 Welcome to City of Heroes, Nova!");
        for (int i = 0; i < 500; i++)
        {
            content.AppendLine(string.Create(CultureInfo.InvariantCulture, $"2026-09-01 12:00:0{i % 10} [Tell] :Someone: SECRET-{i}"));
            content.AppendLine(string.Create(CultureInfo.InvariantCulture, $"2026-09-01 12:00:0{i % 10}  Using global chat handle @SECRET-{i}"));
            content.AppendLine(string.Create(CultureInfo.InvariantCulture, $"2026-09-01 12:00:0{i % 10} \tJoined channel 'SECRET-{i}'"));
        }

        content.AppendLine("2026-09-01 12:00:09 You gain 10 experience.");
        File.WriteAllText(path, content.ToString());

        ReplayResult result = LogReplayer.Replay([path]);
        CharacterSession session = Assert.Single(result.Sessions);

        Assert.Equal(2, session.Messages.TotalCaptured); // banner + reward, nothing else
        Assert.DoesNotContain(session.Messages.Messages, message => message.Payload.Contains("SECRET", StringComparison.Ordinal));
        Assert.DoesNotContain("SECRET", SummaryFormatter.Format(result), StringComparison.Ordinal);
        Assert.Equal(0, result.UnattributedCount); // refused lines are not even counted
    }

    /// <summary>ATT&amp;CK T1565.001 stored data manipulation - documented behaviour, not a defect.</summary>
    [Fact]
    public void A_forged_log_produces_forged_stats_but_never_unsafe_behaviour()
    {
        // The logs are the source of truth by design: someone who edits a log
        // changes the numbers it reports. What must hold is that the tool
        // stays well-behaved - attribution follows the identity triggers and
        // nothing throws.
        string path = Path.Join(_root, "acct", "Logs", "chatlog 2026-09-05.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string forged = "2026-09-01 12:00:00 Welcome to City of Heroes, Nova!\n"
            + "2026-09-01 12:00:01 You gain 999999 experience.\n"
            + "2026-09-01 12:00:02 HIT Impostor! Your Health power is autohit.\n"
            + "2026-09-01 12:00:03 You gain 5 experience.\n";
        File.WriteAllText(path, forged);

        ReplayResult result = LogReplayer.Replay([path]);

        Assert.Equal(2, result.Sessions.Count);
        Assert.Equal(999999, result.Sessions[0].Stats.Experience);
        Assert.Equal("Impostor", result.Sessions[1].Character); // trigger honoured, no crash
        Assert.Equal(5, result.Sessions[1].Stats.Experience);
    }
}
