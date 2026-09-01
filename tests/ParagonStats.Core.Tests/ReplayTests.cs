using System.Globalization;

using ParagonStats.Core.Parsing;
using ParagonStats.Core.Sessions;
using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tests;

public sealed class ReplayTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ps-replay-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Fixture(string name) => Path.Join(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>
    /// The fixture files are byte-exact mid-session excerpts; a session needs a
    /// banner to attribute them (lines outside a banner-anchored session are
    /// unattributed by design). The harness supplies that context: a banner one
    /// minute before the excerpt's first line, followed by the untouched bytes.
    /// </summary>
    private string WithBanner(string name)
    {
        byte[] raw = File.ReadAllBytes(Fixture(name));
        string first = File.ReadLines(Fixture(name)).First();
        DateTime open = DateTime.ParseExact(first[..19], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture).AddMinutes(-1);
        string banner = string.Create(CultureInfo.InvariantCulture, $"{open:yyyy-MM-dd HH:mm:ss} Welcome to City of Heroes, Nova - PRIME!\n");
        string path = Path.Join(_root, name);
        using FileStream output = File.Create(path);
        output.Write(System.Text.Encoding.UTF8.GetBytes(banner));
        output.Write(raw);
        return path;
    }

    [Fact]
    public void Session_banner_fixture_attributes_lines_and_counts_preamble()
    {
        ReplayResult result = LogReplayer.Replay([Fixture("real-session-banner.txt")]);
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.Equal("Nova - PRIME", session.Character);

        // The MOTD chat line precedes the banner: unattributable by design.
        Assert.Equal(1, result.UnattributedCount);

        // The banner itself is counted and captured in the session it opens.
        Assert.Equal(1, session.Stats.CategoryCounts.GetValueOrDefault(EventCategory.Session));
        Assert.Contains(session.Messages.Messages, m => m.Payload.StartsWith("Welcome to City of Heroes", StringComparison.Ordinal));

        // The timestamp-less continuation line is skipped by the reader entirely:
        // 12 fixture lines - 1 continuation - 1 pre-banner MOTD = 10 captured.
        Assert.Equal(10, session.Messages.TotalCaptured);
        Assert.DoesNotContain(session.Messages.Messages, m => m.Payload.Contains("continuation", StringComparison.Ordinal));
    }

    [Fact]
    public void Attack_chain_fixture_folds_damage_and_activations()
    {
        ReplayResult result = LogReplayer.Replay([WithBanner("real-attack-chain.txt")]);
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.Equal("Nova - PRIME", session.Character);
        Assert.Equal(1, session.Stats.Activations);
        Assert.True(session.Stats.TotalDamage > 0);
        Assert.Contains("Irradiated Ground: Irradiated Ground", (IDictionary<string, decimal>)session.Stats.DamageByPower);
    }

    [Fact]
    public void Same_second_storm_lines_all_count_no_dedupe()
    {
        ReplayResult result = LogReplayer.Replay([WithBanner("real-same-second-storm.txt")]);
        CharacterSession session = Assert.Single(result.Sessions);

        // The banner plus all 45 storm lines: exact count, because ANY dedupe
        // of the byte-identical same-second lines (AoE + DoT ticks + proc
        // rolls) must fail this.
        Assert.Equal(46, session.Messages.TotalCaptured);
    }

    [Fact]
    public void Crlf_fixture_parses_identically_to_lf()
    {
        ReplayResult result = LogReplayer.Replay([WithBanner("real-crlf-storm.txt")]);
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.All(session.Messages.Messages, m => Assert.False(m.Payload.EndsWith('\r')));
    }

    [Fact]
    public void Rewards_fixture_sums_experience_and_infamy()
    {
        ReplayResult result = LogReplayer.Replay([WithBanner("real-rewards.txt")]);
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.True(session.Stats.Experience > 0);
        Assert.True(session.Stats.Influence > 0);
    }

    [Fact]
    public void Captured_chat_lines_retain_their_channel()
    {
        ReplayResult result = LogReplayer.Replay([WithBanner("real-chat-channels.txt")]);
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.Contains(session.Messages.Messages, m => string.Equals(m.Channel, "Tell", StringComparison.Ordinal));
        Assert.Contains(session.Messages.Messages, m => m.Channel is null);
    }

    [Fact]
    public void Formatter_renders_ascii_summary()
    {
        ReplayResult result = LogReplayer.Replay([WithBanner("real-attack-chain.txt")]);
        string text = SummaryFormatter.Format(result);
        Assert.Contains("Nova - PRIME", text, StringComparison.Ordinal);
        Assert.Contains("sessions 1", text, StringComparison.Ordinal);
        Assert.Contains("Damage", text, StringComparison.Ordinal); // per-category counts surface (#128 AC)
        Assert.Contains("rates/hr:", text, StringComparison.Ordinal); // uniform rate model surfaces (#123/#124)
        Assert.All(text, c => Assert.True(c is '\r' or '\n' || (c >= ' ' && c <= '~'), string.Create(CultureInfo.InvariantCulture, $"non-ASCII char: {(int)c}")));
    }

    [Fact]
    public void Corpus_smoke_reports_uncategorized_ratio_when_configured()
    {
        string? corpus = Environment.GetEnvironmentVariable("PARAGON_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(corpus), "PARAGON_CORPUS_DIR not set");

        string[] files = Directory.GetFiles(corpus!, "chatlog*.txt", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        ReplayResult result = LogReplayer.Replay(files);
        long total = result.Sessions.Sum(s => s.Messages.TotalCaptured);
        long uncategorized = result.Sessions.Sum(s => s.Stats.CategoryCounts.GetValueOrDefault(EventCategory.Uncategorized));
        Assert.True(total > 0);

        // The drift canary: if the grammar rots, everything degrades into
        // Uncategorized. A majority-uncategorized source means the parser no
        // longer recognizes the game's output.
        double ratio = (double)uncategorized / total;
        Assert.True(ratio < 0.75, string.Create(CultureInfo.InvariantCulture, $"uncategorized ratio {ratio:P1} ({uncategorized}/{total}) - grammar drift?"));
    }
}
