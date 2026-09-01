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
    [GeneratedRegex(@"^(?<pet>[^:\[]{1,60}):  (?=\S)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PseudopetPrefix { get; }

    [GeneratedRegex(@"^Welcome to City of Heroes, (?<name>.+)!$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Banner { get; }

    // Self-only inherent autohits, both directions: the named character is
    // always the logged-in one (see IdentityPulse).
    [GeneratedRegex(@"^(?:HIT (?<name>.+)! Your (?:Health|Stamina) power is autohit|(?<name>.+) HITS you! (?:Health|Stamina) power was autohit)\.$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
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

        logEvent = Parse(payload, line.Payload);
        return true;
    }

    private static LogEvent Parse(string payload, string raw)
    {
        string? sourcePrefix = StripPseudopetDamagePrefix(ref payload);

        Match m = Banner.Match(payload);
        if (m.Success)
        {
            return new SessionStart(m.Groups["name"].Value);
        }

        m = Pulse.Match(payload);
        if (m.Success)
        {
            return new IdentityPulse(m.Groups["name"].Value);
        }

        m = Zone.Match(payload);
        if (m.Success)
        {
            return new ZoneEntered(m.Groups["zone"].Value);
        }

        m = Activation.Match(payload);
        if (m.Success)
        {
            return new PowerActivated(m.Groups["power"].Value);
        }

        m = Damage.Match(payload);
        if (m.Success)
        {
            return new DamageDealt(
                m.Groups["target"].Value,
                m.Groups["power"].Value,
                decimal.Parse(m.Groups["amount"].Value, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture),
                m.Groups["type"].Value,
                m.Groups["overtime"].Success,
                sourcePrefix);
        }

        m = DefeatLine.Match(payload);
        if (m.Success)
        {
            string? attacker = m.Groups["attacker"].Success ? m.Groups["attacker"].Value : null;
            return new Defeat(attacker, m.Groups["foe"].Value);
        }

        return ParseEconomy(payload) ?? new UncategorizedLine(raw);
    }

    /// <summary>The reward grammars: combat gains, architect tickets, market money.</summary>
    private static LogEvent? ParseEconomy(string payload)
    {
        Match m = Tickets.Match(payload);
        if (m.Success)
        {
            return new TicketsEarned(ParseCount(m.Groups["count"].Value));
        }

        m = Market.Match(payload);
        if (m.Success)
        {
            return new MarketTransaction(ParseCount(m.Groups["amount"].Value), m.Groups["got"].Success);
        }

        m = Reward.Match(payload);
        if (m.Success)
        {
            long? xp = m.Groups["xp"].Success ? ParseCount(m.Groups["xp"].Value) : null;
            long? inf = m.Groups["inf"].Success ? ParseCount(m.Groups["inf"].Value) : null;
            return new RewardGained(xp, inf);
        }

        return null;
    }

    /// <summary>
    /// A pseudopet source prefixes damage grammar with "Name:  " (two spaces).
    /// Only damage lines carry attribution this way in real logs (1.5M corpus
    /// lines; zero prefixed defeats/activations/rewards), so the prefix
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

    private static long ParseCount(string text) =>
        long.Parse(text, NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
}
