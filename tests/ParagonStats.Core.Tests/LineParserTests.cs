using ParagonStats.Core.Logging;
using ParagonStats.Core.Parsing;

namespace ParagonStats.Core.Tests;

public sealed class LineParserTests
{
    private static LogEvent? Parse(string payload) =>
        LineParser.TryParse(new LogLine(new DateTime(2024, 5, 12, 11, 34, 51), payload), out LogEvent logEvent) ? logEvent : null;

    [Fact]
    public void Banner_yields_session_start_with_punctuated_name()
    {
        LogEvent? e = Parse("Welcome to City of Heroes, Nova - PRIME!");
        SessionStart s = Assert.IsType<SessionStart>(e);
        Assert.Equal("Nova - PRIME", s.CharacterName);
    }

    [Theory]
    [InlineData("HIT Laser - QUANTUM! Your Health power is autohit.", "Laser - QUANTUM")]
    [InlineData("HIT Laser - SPARK! Your Stamina power is autohit.", "Laser - SPARK")]
    [InlineData("Laser - ALT F4 HITS you! Health power was autohit.", "Laser - ALT F4")]
    [InlineData("Nova HITS you! Stamina power was autohit.", "Nova")]
    public void Self_inherent_autohit_yields_identity_pulse(string payload, string name)
    {
        IdentityPulse pulse = Assert.IsType<IdentityPulse>(Parse(payload));
        Assert.Equal(name, pulse.CharacterName);
    }

    [Theory]
    [InlineData("HIT Laser - SPARK! Your Hasten power is autohit.")] // not a self-only inherent
    [InlineData("HIT Gravedigger Slicer! Your Atom Smasher power had a 95.00% chance to hit, you rolled a 12.04.")]
    public void Other_autohit_and_hit_roll_lines_are_not_identity(string payload)
    {
        Assert.IsNotType<IdentityPulse>(Parse(payload));
    }

    [Theory]
    [InlineData("You earned 12 architect tickets!", 12)]
    [InlineData("You have received 250 bonus architect tickets for completing the mission!", 250)]
    public void Architect_tickets_yield_reward(string payload, long count)
    {
        TicketsEarned t = Assert.IsType<TicketsEarned>(Parse(payload));
        Assert.Equal(count, t.Count);
    }

    [Theory]
    [InlineData("You got 4,500,000 influence from the Consignment House.", 4500000, true)]
    [InlineData("You got 971 infamy from the Black Market.", 971, true)]
    [InlineData("You paid 245,000 to the Consignment House.", 245000, false)]
    public void Market_transactions_track_direction(string payload, long amount, bool income)
    {
        MarketTransaction m = Assert.IsType<MarketTransaction>(Parse(payload));
        Assert.Equal(amount, m.Amount);
        Assert.Equal(income, m.Income);
    }

    [Theory]
    [InlineData("One or more architect tickets were not rewarded because you have reached your inventory cap.")]
    [InlineData("One or more architect tickets were not rewarded because you have reached the ticket limit for this map.")]
    public void Capped_ticket_lines_stay_uncategorized(string payload)
    {
        Assert.IsType<UncategorizedLine>(Parse(payload));
    }

    [Theory]
    [InlineData("Entering Bronze Way.", "Bronze Way")]
    [InlineData("Entering Architect Entertainment.", "Architect Entertainment")]
    public void Zone_entry_yields_the_map_name(string payload, string zone)
    {
        ZoneEntered z = Assert.IsType<ZoneEntered>(Parse(payload));
        Assert.Equal(zone, z.Zone);
    }

    [Fact]
    public void Zone_exit_warning_is_not_a_zone()
    {
        Assert.IsType<UncategorizedLine>(Parse("Entering WARNING: You are about to exit this zone."));
    }

    [Fact]
    public void Activation_yields_power()
    {
        PowerActivated p = Assert.IsType<PowerActivated>(Parse("You activated the Atom Smasher power."));
        Assert.Equal("Atom Smasher", p.Power);
    }

    [Theory]
    [InlineData("You hit Gravedigger Slammer with your Blazing Aura for 16.98 points of Fire damage.", "Gravedigger Slammer", "Blazing Aura", "16.98", "Fire", false)]
    [InlineData("You hit Gravedigger Slammer with your Degenerative Interface for 7 points of Toxic damage over time.", "Gravedigger Slammer", "Degenerative Interface", "7", "Toxic", true)]
    [InlineData("You hit Vigilant with your Executioner's Shot for 1,135.23 points of Lethal damage.", "Vigilant", "Executioner's Shot", "1135.23", "Lethal", false)]
    public void Damage_dealt_parses_amount_type_and_overtime(string payload, string target, string power, string amount, string type, bool overTime)
    {
        DamageDealt d = Assert.IsType<DamageDealt>(Parse(payload));
        Assert.Equal(target, d.Target);
        Assert.Equal(power, d.Power);
        Assert.Equal(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), d.Amount);
        Assert.Equal(type, d.DamageType);
        Assert.Equal(overTime, d.OverTime);
        Assert.Null(d.SourcePrefix);
    }

    [Fact]
    public void Pseudopet_prefix_is_captured_with_two_space_marker()
    {
        DamageDealt d = Assert.IsType<DamageDealt>(
            Parse("Irradiated Ground:  You hit Gravedigger Slammer with your Irradiated Ground for 4.67 points of Fire damage."));
        Assert.Equal("Irradiated Ground", d.SourcePrefix);
        Assert.Equal(4.67m, d.Amount);
    }

    [Theory]
    [InlineData("You have defeated Blood Brother Slammer", null, "Blood Brother Slammer")]
    [InlineData("Pat1 has defeated Fallen Buckshot", "Pat1", "Fallen Buckshot")]
    public void Defeats_distinguish_own_and_teammate(string payload, string? attacker, string foe)
    {
        Defeat d = Assert.IsType<Defeat>(Parse(payload));
        Assert.Equal(attacker, d.Attacker);
        Assert.Equal(foe, d.Foe);
    }

    [Theory]
    [InlineData("You gain 832 experience and 388 infamy.", 832L, 388L)]
    [InlineData("You gain 84,057 experience and 28,019 influence.", 84057L, 28019L)]
    [InlineData("You gain 17,940 experience.", 17940L, null)]
    [InlineData("You gain 218 influence.", null, 218L)]
    [InlineData("You gain 638 experience, work off 638 debt, and gain 1,786 influence.", 638L, 1786L)]
    [InlineData("You gain 638 experience, work off 638 debt, and gain 1,786 infamy.", 638L, 1786L)]
    public void Rewards_parse_experience_influence_and_infamy(string payload, long? xp, long? inf)
    {
        RewardGained r = Assert.IsType<RewardGained>(Parse(payload));
        Assert.Equal(xp, r.Experience);
        Assert.Equal(inf, r.Influence);
    }

    [Theory]
    [InlineData("[Tell] :Other Player: private words")]
    [InlineData("[Tell] -->Other Player: private words")]
    [InlineData("[Looking For Group] PlayerOne: recruiting text")]
    [InlineData("[SuperGroup] AnonSG Message of the Day -- greetings")]
    [InlineData("[unclosed bracket garbage")]
    [InlineData("Using global chat handle @anon")]
    [InlineData("Joined channel 'ChannelA'")]
    [InlineData("Left channel 'ChannelA'")]
    public void Communication_channel_lines_are_dumped_entirely(string payload)
    {
        // Collection policy (operator ruling): zero collection - no event,
        // no capture, no count. The parser returns nothing at all.
        Assert.Null(Parse(payload));
    }

    [Theory]
    [InlineData("HIT Gravedigger Slicer! Your Atom Smasher power had a 95.00% chance to hit, you rolled a 12.04.")]
    [InlineData("Gravedigger Slicer MISSES! Revolver power had a 20.27% chance to hit, but rolled a 55.54.")]
    [InlineData("Rain of Arrows:  MISSED Test Dummy!! Your RainofArrows power had a 75.00% chance to hit, you rolled a 75.73.")]
    [InlineData("You hit Little Big Dombloo with your Ball Lightning for 13.99 points of their endurance.")]
    [InlineData("You Taunt Gravedigger Slammer with your Fury.")]
    [InlineData("Your combat improves to level 50! Seek a trainer to further your abilities.")]
    [InlineData("You are now fighting at level 17.")]
    [InlineData("You received Nanotech Growth Medium.")]
    public void Non_mvp_lines_pass_through_uncategorized(string payload)
    {
        UncategorizedLine u = Assert.IsType<UncategorizedLine>(Parse(payload));
        Assert.Equal(payload, u.Payload);
    }
}
