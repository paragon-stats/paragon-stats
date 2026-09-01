using System.Globalization;

using ParagonStats.Core.Parsing;
using ParagonStats.Core.Sessions;
using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tests;

public sealed class ReplayTests
{
    private static string Fixture(string name) => Path.Join(AppContext.BaseDirectory, "Fixtures", name);

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

        // The timestamp-less continuation line is skipped by the reader entirely.
        Assert.True(session.Messages.TotalCaptured > 0);
    }

    [Fact]
    public void Attack_chain_fixture_folds_damage_and_activations()
    {
        ReplayResult result = LogReplayer.Replay([Fixture("real-session-banner.txt"), Fixture("real-attack-chain.txt")]);
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.Equal(1, session.Stats.Activations);
        Assert.True(session.Stats.TotalDamage > 0);
        Assert.Contains("Irradiated Ground: Irradiated Ground", (IDictionary<string, decimal>)session.Stats.DamageByPower);
    }

    [Fact]
    public void Same_second_storm_lines_all_count_no_dedupe()
    {
        ReplayResult result = LogReplayer.Replay([Fixture("real-session-banner.txt"), Fixture("real-same-second-storm.txt")]);
        CharacterSession session = Assert.Single(result.Sessions);

        // 45 storm lines land after the banner; every one must be captured even
        // though many are byte-identical within the same second (AoE + DoT + procs).
        Assert.True(session.Messages.TotalCaptured >= 45);
    }

    [Fact]
    public void Crlf_fixture_parses_identically_to_lf()
    {
        ReplayResult result = LogReplayer.Replay([Fixture("real-session-banner.txt"), Fixture("real-crlf-storm.txt")]);
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.All(session.Messages.Messages, m => Assert.False(m.Payload.EndsWith('\r')));
    }

    [Fact]
    public void Rewards_fixture_sums_experience_and_infamy()
    {
        ReplayResult result = LogReplayer.Replay([Fixture("real-session-banner.txt"), Fixture("real-rewards.txt")]);
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.True(session.Stats.Experience > 0);
        Assert.True(session.Stats.Influence > 0);
    }

    [Fact]
    public void Captured_chat_lines_retain_their_channel()
    {
        ReplayResult result = LogReplayer.Replay([Fixture("real-session-banner.txt"), Fixture("real-chat-channels.txt")]);
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.Contains(session.Messages.Messages, m => string.Equals(m.Channel, "Tell", StringComparison.Ordinal));
        Assert.Contains(session.Messages.Messages, m => m.Channel is null);
    }

    [Fact]
    public void Formatter_renders_ascii_summary()
    {
        ReplayResult result = LogReplayer.Replay([Fixture("real-session-banner.txt"), Fixture("real-attack-chain.txt")]);
        string text = SummaryFormatter.Format(result);
        Assert.Contains("Nova - PRIME", text, StringComparison.Ordinal);
        Assert.Contains("sessions 1", text, StringComparison.Ordinal);
        Assert.Contains("Damage", text, StringComparison.Ordinal); // per-category counts surface (#128 AC)
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
        // Uncategorized. A majority-uncategorized corpus means the parser no
        // longer recognizes the game's output.
        double ratio = (double)uncategorized / total;
        Assert.True(ratio < 0.75, string.Create(CultureInfo.InvariantCulture, $"uncategorized ratio {ratio:P1} ({uncategorized}/{total}) - grammar drift?"));
    }
}
