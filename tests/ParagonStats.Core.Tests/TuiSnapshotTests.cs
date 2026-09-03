using ParagonStats.Core.Parsing;
using ParagonStats.Core.Sessions;
using ParagonStats.Core.Tui;

namespace ParagonStats.Core.Tests;

/// <summary>
/// The snapshot is what every screen renders from, so the things it guarantees
/// - a stable copy, the tracker's own ordering, clamped spans, and a combined
/// row that does not double-count overlapping boxes - are pinned here rather
/// than assumed by each screen.
/// </summary>
public sealed class TuiSnapshotTests
{
    private static readonly DateTime Noon = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void An_empty_tracker_snapshots_to_an_empty_readout()
    {
        Snapshot snapshot = Snapshot.Capture([], 0);

        Assert.True(snapshot.IsEmpty);
        Assert.Empty(snapshot.Rows);
        Assert.Equal("ALL BOXES", snapshot.Combined.Character);
        Assert.Equal(TimeSpan.Zero, snapshot.Combined.Clock);
        Assert.Equal(0, snapshot.Combined.Experience);
    }

    [Fact]
    public void Rows_carry_every_scalar_the_engine_folds()
    {
        CharacterSession session = Session("acct", "Nova", Noon, TimeSpan.FromMinutes(30));
        session.Stats.Apply(new RewardGained(1200, 3400));
        session.Stats.Apply(new TicketsEarned(12));
        session.Stats.Apply(new Defeat(Attacker: null, "Gravedigger"));
        session.Stats.Apply(new PowerActivated("Blazing Aura"));
        session.Stats.Apply(new DamageDealt("Foe", "Blazing Aura", 16.98m, "Fire", OverTime: false, SourcePrefix: null));
        session.Stats.Apply(new MarketTransaction(500, Income: true));
        session.Stats.Apply(new MarketTransaction(200, Income: false));

        SessionRow row = Assert.Single(Snapshot.Capture([session], 0).Rows);

        Assert.Equal("Nova", row.Character);
        Assert.Equal("acct", row.Account);
        Assert.Equal(TimeSpan.FromMinutes(30), row.Clock);
        Assert.Equal(1200, row.Experience);
        Assert.Equal(3400, row.Influence);
        Assert.Equal(12, row.Tickets);
        Assert.Equal(1, row.Defeats);
        Assert.Equal(1, row.Activations);
        Assert.Equal(16.98m, row.Damage);
        Assert.Equal(500, row.MarketIncome);
        Assert.Equal(200, row.MarketSpent);
    }

    [Fact]
    public void Rows_come_back_in_the_trackers_own_order_not_the_dictionarys()
    {
        // Same start second on purpose: account then character then open order
        // is the total order the engine uses, and the readout must not invent
        // its own.
        CharacterSession later = Session("zeta", "Aaa", Noon, TimeSpan.FromMinutes(1));
        CharacterSession earlier = Session("alpha", "Zzz", Noon, TimeSpan.FromMinutes(1));
        CharacterSession first = Session("alpha", "Aaa", Noon, TimeSpan.FromMinutes(1));

        Snapshot snapshot = Snapshot.Capture([later, earlier, first], 0);

        Assert.Equal(["Aaa", "Zzz", "Aaa"], snapshot.Rows.Select(row => row.Character), StringComparer.Ordinal);
        Assert.Equal(["alpha", "alpha", "zeta"], snapshot.Rows.Select(row => row.Account), StringComparer.Ordinal);
    }

    [Fact]
    public void A_backwards_span_clamps_to_zero_rather_than_reporting_negative_time()
    {
        // Naive local timestamps run backwards over a DST change. The batch
        // summary clamped; the live line did not. Every surface reads this now.
        CharacterSession session = Session("acct", "Nova", Noon, TimeSpan.FromMinutes(-45));

        SessionRow row = Assert.Single(Snapshot.Capture([session], 0).Rows);

        Assert.Equal(TimeSpan.Zero, row.Clock);
    }

    [Fact]
    public void The_combined_row_sums_counters_but_spans_the_window()
    {
        // Multiboxing overlaps: summing per-box clocks would report more
        // elapsed time than actually passed.
        CharacterSession one = Session("acct", "Nova", Noon, TimeSpan.FromHours(1));
        CharacterSession two = Session("acct2", "Pulse", Noon.AddMinutes(30), TimeSpan.FromHours(1));
        one.Stats.Apply(new RewardGained(100, 10));
        two.Stats.Apply(new RewardGained(200, 20));

        Snapshot snapshot = Snapshot.Capture([one, two], 0);

        Assert.Equal(300, snapshot.Combined.Experience);
        Assert.Equal(30, snapshot.Combined.Influence);

        // Noon to 13:30 is 90 minutes of wall clock, not the 120 the rows sum to.
        Assert.Equal(TimeSpan.FromMinutes(90), snapshot.Combined.Clock);
        Assert.Equal(TimeSpan.FromHours(2), snapshot.Rows.Aggregate(TimeSpan.Zero, (sum, row) => sum + row.Clock));
    }

    [Fact]
    public void Unattributed_lines_are_carried_through()
    {
        Snapshot snapshot = Snapshot.Capture([Session("acct", "Nova", Noon, TimeSpan.FromMinutes(5))], 42);

        Assert.Equal(42, snapshot.Unattributed);
        Assert.False(snapshot.IsEmpty);
    }

    [Fact]
    public void Capturing_from_a_tracker_reads_its_open_sessions_and_count()
    {
        SessionTracker tracker = new();
        tracker.Accept("acct", "2026-01-01 12:00:00 You gain 5 experience.");
        tracker.Accept("acct", "2026-01-01 12:00:10 Welcome to City of Heroes, Nova!");
        tracker.Accept("acct", "2026-01-01 12:05:00 You gain 900 experience.");

        Snapshot snapshot = Snapshot.Capture(tracker);

        SessionRow row = Assert.Single(snapshot.Rows);
        Assert.Equal("Nova", row.Character);
        Assert.Equal(900, row.Experience);
        Assert.Equal(1, snapshot.Unattributed); // the line before the banner
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        SessionTracker? tracker = null;
        IReadOnlyCollection<CharacterSession>? sessions = null;

        Assert.Throws<ArgumentNullException>(() => Snapshot.Capture(tracker!));
        Assert.Throws<ArgumentNullException>(() => Snapshot.Capture(sessions!, 0));
    }

    private static CharacterSession Session(string account, string character, DateTime start, TimeSpan ran)
    {
        CharacterSession session = new(account, character, start, 0)
        {
            LastSeen = start + ran,
        };
        return session;
    }
}
