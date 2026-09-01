using ParagonStats.Core.Logging;
using ParagonStats.Core.Parsing;

namespace ParagonStats.Core.Tests;

public sealed class LineParserTests
{
    private static LogEvent Parse(string payload) =>
        LineParser.Parse(new LogLine(new DateTime(2024, 5, 12, 11, 34, 51), payload));

    [Fact]
    public void Banner_yields_session_start_with_punctuated_name()
    {
        LogEvent e = Parse("Welcome to City of Heroes, Nova - PRIME!");
        SessionStart s = Assert.IsType<SessionStart>(e);
        Assert.Equal("Nova - PRIME", s.CharacterName);
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
    [InlineData("[Tell] :Other Player: redacted", "Tell", "Other Player")]
    [InlineData("[Tell] -->Other Player: redacted", "Tell", "Other Player")]
    [InlineData("[Looking For Group] PlayerOne: redacted", "Looking For Group", "PlayerOne")]
    public void Chat_channels_and_tell_markers_parse(string payload, string channel, string speaker)
    {
        ChatMessage c = Assert.IsType<ChatMessage>(Parse(payload));
        Assert.Equal(channel, c.Channel);
        Assert.Equal(speaker, c.Speaker);
        Assert.Equal("redacted", c.Text);
    }

    [Fact]
    public void Chat_color_markup_is_stripped()
    {
        ChatMessage c = Assert.IsType<ChatMessage>(
            Parse("[Looking For Group] PlayerOne: <color #010101><bgcolor #019aff>redacted"));
        Assert.Equal("redacted", c.Text);
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
    [InlineData("Using global chat handle @anon")]
    public void Non_mvp_lines_pass_through_uncategorized(string payload)
    {
        UncategorizedLine u = Assert.IsType<UncategorizedLine>(Parse(payload));
        Assert.Equal(payload, u.Payload);
    }
}
