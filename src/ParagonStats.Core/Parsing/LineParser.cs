using System.Globalization;
using System.Text.RegularExpressions;

using ParagonStats.Core.Logging;

namespace ParagonStats.Core.Parsing;

/// <summary>
/// Stateless single-line categorizer. Grammar is compiled in (source-generated
/// regex: AOT-safe, culture-invariant); unknown lines become
/// <see cref="UncategorizedLine"/> so the parser can never lose data or throw
/// on grammar drift.
/// </summary>
public static partial class LineParser
{
    // Speaker markers: incoming tells lead with ':', outgoing with '-->'.
    [GeneratedRegex(@"^\[(?<channel>[^\]]+)\] (?:-->|:)?(?<speaker>[^:]+): ?(?<text>.*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Chat { get; }

    [GeneratedRegex(@"^\[(?<channel>[^\]]+)\] (?<text>.*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ChatChannelOnly { get; }

    [GeneratedRegex(@"<b>|</b>|<color #[0-9A-Za-z]+>|<bgcolor #[0-9A-Za-z]+>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Markup { get; }

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

    [GeneratedRegex(@"^You (?:earned (?<count>[0-9,]+) architect tickets|have received (?<count>[0-9,]+) bonus architect tickets for completing the mission)!$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Tickets { get; }

    [GeneratedRegex(@"^You (?:(?<got>got) (?<amount>[0-9,]+) (?:influence|infamy) from|paid (?<amount>[0-9,]+) to) the (?:Consignment House|Black Market)\.$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Market { get; }

    [GeneratedRegex(@"^You gain (?:(?<xp>[0-9,]+) experience(?:, work off [0-9,]+ debt, and gain (?<inf>[0-9,]+) (?:influence|infamy)| and (?<inf>[0-9,]+) (?:influence|infamy))?|(?<inf>[0-9,]+) (?:influence|infamy))\.$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reward { get; }

    public static LogEvent Parse(in LogLine line)
    {
        string payload = line.Payload;
        if (payload.StartsWith('['))
        {
            return ParseChat(line.Payload);
        }

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

        return ParseEconomy(payload) ?? new UncategorizedLine(line.Payload);
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

    private static LogEvent ParseChat(string payload)
    {
        Match m = Chat.Match(payload);
        if (!m.Success)
        {
            // "[Channel] free text" without a speaker (system MOTD style).
            Match c = ChatChannelOnly.Match(payload);

            // A bracketed line matching no chat shape is a parse failure, not
            // a chat message - it must surface in the drift canary.
            return c.Success
                ? new ChatMessage(c.Groups["channel"].Value, string.Empty, StripMarkup(c.Groups["text"].Value))
                : new UncategorizedLine(payload);
        }

        return new ChatMessage(
            m.Groups["channel"].Value,
            m.Groups["speaker"].Value,
            StripMarkup(m.Groups["text"].Value));
    }

    private static string StripMarkup(string text) => Markup.Replace(text, string.Empty);
}
