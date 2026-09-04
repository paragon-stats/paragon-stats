using System.Globalization;
using System.Text.RegularExpressions;

using ParagonStats.Core.Logging;

namespace ParagonStats.Core.Parsing;

/// <summary>
/// Stateless single-line categorizer. Grammar is compiled in (source-generated
/// regex: AOT-safe, culture-invariant). Unknown DATA lines become
/// <see cref="UncategorizedLine"/> so grammar drift surfaces in the canary;
/// communication-channel lines are refused entirely (see TryParse).
/// </summary>
public static partial class LineParser
{
    /// <summary>The universal inherents: every character has them, and they affect nobody else.</summary>
    private static readonly string[] SelfOnlyPowers = ["Health", "Stamina"];

    [GeneratedRegex(@"^(?<pet>[^:\[]{1,60}):  (?=\S)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PseudopetPrefix { get; }

    [GeneratedRegex(@"^Welcome to City of Heroes, (?<name>.+)!$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Banner { get; }

    // Any autohit line that names someone, in either direction. The power is
    // captured so the caller can tell proof from lead: Health and Stamina are
    // universal inherents affecting only their owner, so those name the player.
    // Everything else names whoever the power happened to reach - a Judgement
    // names the enemies it lands on, and a vicinity buff named 778 distinct
    // people across one account's history. Those become AutohitCandidate and
    // are only believed if the tracker has seen the name in a banner (#250).
    // The empty (?<inbound>) marks the second branch so the caller can tell the
    // directions apart without matching twice, because they are not
    // equivalent: "HIT X! Your P power is autohit." is YOUR power reaching X,
    // and a vicinity power reaches you too, so X can be you. "X HITS you! P
    // power was autohit." names the CASTER, and for a power that is not
    // self-only the caster is by definition somebody else.
    [GeneratedRegex(@"^(?:HIT (?<name>.+)! Your (?<power>.+) power is autohit|(?<inbound>)(?<name>.+) HITS you! (?<power>.+) power was autohit)\.$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pulse { get; }

    [GeneratedRegex(@"^You activated the (?<power>.+) power\.$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Activation { get; }

    [GeneratedRegex(@"^You hit (?<target>.+) with your (?<power>.+) for (?<amount>[0-9,]+(?:\.[0-9]+)?) points of (?<type>.+?) damage(?<overtime> over time)?\.$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Damage { get; }

    [GeneratedRegex(@"^(?:You have defeated (?<foe>.+)|(?<attacker>.+) has defeated (?<foe>.+))$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DefeatLine { get; }

    // Colon excluded: "Entering WARNING: You are about to exit this zone." is
    // the exit-warning popup, not a zone name.
    [GeneratedRegex(@"^Entering (?<zone>[^.:]+)\.$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Zone { get; }

    [GeneratedRegex(@"^You (?:earned (?<count>[0-9,]+) architect tickets|have received (?<count>[0-9,]+) bonus architect tickets for completing the mission)!$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Tickets { get; }

    [GeneratedRegex(@"^You (?:(?<got>got) (?<amount>[0-9,]+) (?:influence|infamy) from|paid (?<amount>[0-9,]+) to) the (?:Consignment House|Black Market)\.$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Market { get; }

    [GeneratedRegex(@"^You gain (?:(?<xp>[0-9,]+) experience(?:, work off [0-9,]+ debt, and gain (?<inf>[0-9,]+) (?:influence|infamy)| and (?<inf>[0-9,]+) (?:influence|infamy))?|(?<inf>[0-9,]+) (?:influence|infamy))\.$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reward { get; }

    /// <summary>
    /// False for lines the tool refuses to collect (mirrors
    /// <see cref="LogLineReader.TryParse"/> - one non-result idiom through
    /// the pipeline). Collection policy (operator ruling): communication
    /// channels are not harvested AT ALL - bracketed lines (allowlist empty;
    /// [NPC]/[Caption] would join only by deliberate decision on #225) and
    /// communication metadata (the player's global handle, channel
    /// membership) are dumped: no event, no capture, no count.
    /// </summary>
    public static bool TryParse(in LogLine line, out LogEvent logEvent)
    {
        string payload = line.Payload;
        if (CollectionPolicy.RefusesPayload(payload))
        {
            logEvent = UncategorizedLine.Empty;
            return false;
        }

        try
        {
            logEvent = Parse(payload, line.Payload);
        }
        catch (RegexMatchTimeoutException)
        {
            // A crafted line can make a grammar backtrack; the per-pattern
            // timeout turns that into a miss, never a crash.
            logEvent = new UncategorizedLine(line.Payload);
        }

        return true;
    }

    private static LogEvent Parse(string payload, string raw)
    {
        string? sourcePrefix = StripPseudopetDamagePrefix(ref payload);

        Match match = Banner.Match(payload);
        if (match.Success)
        {
            return new SessionStart(match.Groups["name"].Value);
        }

        match = Pulse.Match(payload);
        if (match.Success)
        {
            string named = match.Groups["name"].Value;
            return Array.Exists(SelfOnlyPowers, power => string.Equals(power, match.Groups["power"].Value, StringComparison.Ordinal))
                ? new IdentityPulse(named)
                : new AutohitCandidate(named, SelfDirected: !match.Groups["inbound"].Success);
        }

        match = Zone.Match(payload);
        if (match.Success)
        {
            return new ZoneEntered(match.Groups["zone"].Value);
        }

        match = Activation.Match(payload);
        if (match.Success)
        {
            return new PowerActivated(match.Groups["power"].Value);
        }

        match = Damage.Match(payload);
        if (match.Success
            && decimal.TryParse(match.Groups["amount"].Value, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount))
        {
            return new DamageDealt(
                match.Groups["target"].Value,
                match.Groups["power"].Value,
                amount,
                match.Groups["type"].Value,
                match.Groups["overtime"].Success,
                sourcePrefix);
        }

        match = DefeatLine.Match(payload);
        if (match.Success)
        {
            string? attacker = match.Groups["attacker"].Success ? match.Groups["attacker"].Value : null;
            return new Defeat(attacker, match.Groups["foe"].Value);
        }

        return ParseEconomy(payload) ?? new UncategorizedLine(raw);
    }

    /// <summary>The reward grammars: combat gains, architect tickets, market money.</summary>
    private static LogEvent? ParseEconomy(string payload)
    {
        Match match = Tickets.Match(payload);
        if (match.Success && TryCount(match.Groups["count"].Value, out long tickets))
        {
            return new TicketsEarned(tickets);
        }

        match = Market.Match(payload);
        if (match.Success && TryCount(match.Groups["amount"].Value, out long money))
        {
            return new MarketTransaction(money, match.Groups["got"].Success);
        }

        match = Reward.Match(payload);
        if (match.Success)
        {
            long? experience = TryCount(match.Groups["xp"], out long xp) ? xp : null;
            long? influence = TryCount(match.Groups["inf"], out long inf) ? inf : null;
            if (experience is not null || influence is not null)
            {
                return new RewardGained(experience, influence);
            }
        }

        return null;
    }

    /// <summary>
    /// A pseudopet source prefixes damage grammar with "Name:  " (two spaces).
    /// Only damage lines carry attribution this way in real logs (1.5M lines
    /// of them; zero prefixed defeats/activations/rewards), so the prefix
    /// applies to the damage grammar alone - anything else a prefixed line
    /// says falls through as its unprefixed self.
    /// </summary>
    private static string? StripPseudopetDamagePrefix(ref string payload)
    {
        Match pet = PseudopetPrefix.Match(payload);
        if (pet.Success && Damage.IsMatch(payload[pet.Length..]))
        {
            payload = payload[pet.Length..];
            return pet.Groups["pet"].Value;
        }

        return null;
    }

    /// <summary>
    /// A number too large for the game to have produced is treated as no
    /// number at all - a hand-edited log can never overflow the fold.
    /// </summary>
    private static bool TryCount(string text, out long value) =>
        long.TryParse(text, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);

    private static bool TryCount(Group group, out long value)
    {
        value = 0;
        return group.Success && TryCount(group.Value, out value);
    }
}
