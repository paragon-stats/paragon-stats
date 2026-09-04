using ParagonStats.Core.Parsing;
using ParagonStats.Core.Sessions;

namespace ParagonStats.Core.Tests;

/// <summary>
/// Who the earnings belong to. Every case here came from replaying real logs
/// against the shipped binary, and the counter-examples are real lines - the
/// enemy and bystander names are the ones the game actually wrote.
/// </summary>
public sealed class IdentityTests
{
    [Fact]
    public void A_non_self_power_autohit_names_whoever_it_reached_so_it_proves_nothing()
    {
        // Real line. Judgement powers name the enemies they land on, and across
        // one account's history a single vicinity buff named 778 distinct
        // people. Believing the name would open a session called "Angelbot".
        SessionTracker tracker = new();

        tracker.Accept("acct", "2026-01-01 10:00:00 HIT Angelbot v3.5! Your Ion Core Final Judgement power is autohit.");

        Assert.Empty(tracker.Open);
        Assert.Equal(1, tracker.UnattributedCount);
    }

    [Fact]
    public void A_name_from_this_accounts_own_banners_is_believed()
    {
        // The roster is the filter: a banner is the one line no enemy, pet or
        // other player can produce. Once a character has logged in normally
        // once, any autohit naming it identifies it - whatever the power, so a
        // build nobody anticipated is not invisible (#250).
        SessionTracker tracker = new();
        tracker.Accept("acct", "2026-01-01 10:00:00 Welcome to City of Heroes, Nova!");
        tracker.Accept("acct", "2026-01-01 12:00:00 You gain 5 experience.");

        tracker.Accept("acct", "2026-01-01 12:00:01 HIT Nova! Your Restoration power is autohit.");

        CharacterSession session = Assert.Single(tracker.Open);
        Assert.Equal("Nova", session.Character);
    }

    [Fact]
    public void A_roster_is_per_account_so_another_box_cannot_name_this_one()
    {
        // Real line: one of the operator's own boxes buffing another. The name
        // is a genuine character - just not one belonging to THIS account.
        SessionTracker tracker = new();
        tracker.Accept("boxone", "2026-01-01 10:00:00 Welcome to City of Heroes, Laser - HAZMAT!");

        tracker.Accept("boxtwo", "2026-01-01 10:00:01 Laser - HAZMAT HITS you! Ageless Core Epiphany power was autohit.");

        Assert.Single(tracker.Open); // boxone only
        Assert.DoesNotContain(tracker.Open, session => string.Equals(session.Account, "boxtwo", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unrelated_player_is_never_believed()
    {
        // Real line: a stranger's buff landing on the player mid-farm.
        SessionTracker tracker = new();

        tracker.Accept("acct", "2026-01-01 10:00:00 BRUTERAVEN HITS you! Call to Justice power was autohit.");

        Assert.Empty(tracker.Open);
    }

    [Fact]
    public void Health_and_Stamina_still_identify_without_a_roster()
    {
        // The universal inherents: every character has them and they affect
        // nobody else, which is why they need no corroboration. A character's
        // very first logged session depends on this.
        SessionTracker tracker = new();

        tracker.Accept("acct", "2026-01-01 10:00:00 HIT Nova! Your Health power is autohit.");

        Assert.Equal("Nova", Assert.Single(tracker.Open).Character);
    }

    [Fact]
    public void A_banner_refuses_to_adopt_what_came_before_it()
    {
        // A banner announces a login. Whatever was earned before it belongs to
        // whoever was playing previously, so it stays unattributed rather than
        // being credited to the arrival - and its VALUE is reported (#251).
        SessionTracker tracker = new();
        tracker.Accept("acct", "2026-01-01 10:00:00 You gain 900 experience and 100 influence.");

        tracker.Accept("acct", "2026-01-01 10:00:10 Welcome to City of Heroes, Nova!");

        Assert.Equal(0, Assert.Single(tracker.Open).Stats.Experience);
        Assert.Equal(1, tracker.UnattributedCount);
        Assert.Equal(900, tracker.UnattributedExperience);
        Assert.Equal(100, tracker.UnattributedInfluence);
    }

    [Fact]
    public void A_pulse_adopts_what_came_before_it_and_the_value_leaves_the_books()
    {
        // The measured failure: logging enabled mid-session leaves no banner, so
        // everything earned before the first autohit was discarded - 1,864,215
        // XP in one session. Held and adopted, the books balance.
        SessionTracker tracker = new();
        tracker.Accept("acct", "2026-01-01 10:00:00 You gain 900 experience and 100 influence.");

        tracker.Accept("acct", "2026-01-01 10:00:10 HIT Nova! Your Health power is autohit.");

        CharacterSession session = Assert.Single(tracker.Open);
        Assert.Equal(900, session.Stats.Experience);
        Assert.Equal(100, session.Stats.Influence);
        Assert.Equal(0, tracker.UnattributedCount);
        Assert.Equal(0, tracker.UnattributedExperience);
        Assert.Equal(0, tracker.UnattributedInfluence);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified), session.Start);
    }

    [Fact]
    public void A_gap_before_the_identifying_line_fences_off_what_precedes_it()
    {
        // The mirror of the gap check inside the held events. Hold only sees a
        // silence when another held event turns up; without the same test on
        // adoption, a log that fell quiet and was identified an hour later
        // swallowed everything before the silence and backdated its session to
        // match. That silence IS a logout by the rule this class closes
        // sessions on, so nothing on its far side belongs to whoever is named
        // next.
        SessionTracker tracker = new();
        tracker.Accept("acct", "2026-01-01 09:00:00 You gain 900 experience and 100 influence.");

        tracker.Accept("acct", "2026-01-01 10:00:00 HIT Nova! Your Health power is autohit.");

        CharacterSession session = Assert.Single(tracker.Open);
        Assert.Equal(0, session.Stats.Experience);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified), session.Start);
        Assert.Equal(1, tracker.UnattributedCount);
        Assert.Equal(900, tracker.UnattributedExperience);
    }

    [Fact]
    public void Held_events_are_capped_so_a_log_that_never_identifies_cannot_grow_forever()
    {
        // Oldest-out at the cap: the newest lines are the ones most likely to
        // belong to whoever is about to be named.
        SessionTracker tracker = new();
        for (int line = 0; line < 20_050; line++)
        {
            tracker.Accept("acct", "2026-01-01 10:00:00 You gain 1 experience.");
        }

        tracker.Accept("acct", "2026-01-01 10:00:01 HIT Nova! Your Health power is autohit.");

        // 50 were evicted before the pulse arrived, and stay on the unattributed
        // books; the rest were adopted.
        Assert.Equal(20_000, Assert.Single(tracker.Open).Stats.Experience);
        Assert.Equal(50, tracker.UnattributedCount);
        Assert.Equal(50, tracker.UnattributedExperience);
    }
}
