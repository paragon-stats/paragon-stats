using System.Globalization;
using System.Text;

using ParagonStats.Core.Parsing;
using ParagonStats.Core.Sessions;

namespace ParagonStats.Core.Stats;

/// <summary>Renders replay results as printable-ASCII text (see docs/style-guides/encoding.md).</summary>
public static class SummaryFormatter
{
    public static string Format(ReplayResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        StringBuilder text = new();
        foreach (CharacterSession session in result.Sessions)
        {
            TimeSpan span = session.LastSeen - session.Start;
            if (span < TimeSpan.Zero)
            {
                span = TimeSpan.Zero; // naive local timestamps can run backwards (DST)
            }

            text.AppendLine(CultureInfo.InvariantCulture, $"{Ascii(session.Character)} ({Ascii(session.Account)}) {session.Start:yyyy-MM-dd HH:mm} +{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}");
            text.AppendLine(CultureInfo.InvariantCulture, $"  damage {session.Stats.TotalDamage:0.##} | defeats {session.Stats.Defeats} | xp {session.Stats.Experience} | inf {session.Stats.Influence} | activations {session.Stats.Activations} | tickets {session.Stats.Tickets} | market +{session.Stats.MarketIncome}/-{session.Stats.MarketSpent}");
            text.AppendLine(CultureInfo.InvariantCulture, $"  rates/hr: damage {Rate(session.Stats.TotalDamage, span)} | defeats {Rate(session.Stats.Defeats, span)} | xp {Rate(session.Stats.Experience, span)} | inf {Rate(session.Stats.Influence, span)} | activations {Rate(session.Stats.Activations, span)} | tickets {Rate(session.Stats.Tickets, span)}");
            string categories = string.Join(
                " | ",
                session.Stats.CategoryCounts
                    .OrderBy(entry => entry.Key)
                    .Select(entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Key} {entry.Value}")));
            text.AppendLine(CultureInfo.InvariantCulture, $"  lines {session.Messages.TotalCaptured}: {categories}");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"sessions {result.Sessions.Count} | unattributed lines {result.UnattributedCount}");
        foreach (string skipped in result.SkippedFiles)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"skipped (unreadable): {Ascii(skipped)}");
        }

        return text.ToString();
    }

    /// <summary>One rolling live-watch line for an open session - rates from the same computation the batch summary uses.</summary>
    public static string FormatLive(CharacterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        TimeSpan span = session.LastSeen - session.Start;
        MetricSnapshot xp = MetricSnapshot.Compute(session.Stats.Experience, span);
        MetricSnapshot inf = MetricSnapshot.Compute(session.Stats.Influence, span);
        MetricSnapshot tickets = MetricSnapshot.Compute(session.Stats.Tickets, span);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{session.LastSeen:HH:mm:ss}] {Ascii(session.Character)}: xp {xp.Value:0} ({xp.PerHour:0}/hr) | inf {inf.Value:0} ({inf.PerHour:0}/hr) | tickets {tickets.Value:0} ({tickets.PerHour:0}/hr)");
    }

    /// <summary>
    /// The multibox farm total: one line summing every open session (operator
    /// use case: influence gain across all boxes at once). Rates add because
    /// the sessions run concurrently.
    /// </summary>
    public static string FormatCombined(IReadOnlyCollection<CharacterSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        decimal xp = 0m, xpRate = 0m, inf = 0m, infRate = 0m, tickets = 0m, ticketRate = 0m;
        foreach (CharacterSession session in sessions)
        {
            TimeSpan span = session.LastSeen - session.Start;
            MetricSnapshot experience = MetricSnapshot.Compute(session.Stats.Experience, span);
            MetricSnapshot influence = MetricSnapshot.Compute(session.Stats.Influence, span);
            MetricSnapshot earned = MetricSnapshot.Compute(session.Stats.Tickets, span);
            xp += experience.Value;
            xpRate += experience.PerHour;
            inf += influence.Value;
            infRate += influence.PerHour;
            tickets += earned.Value;
            ticketRate += earned.PerHour;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"[all {sessions.Count} boxes] xp {xp:0} ({xpRate:0}/hr) | inf {inf:0} ({infRate:0}/hr) | tickets {tickets:0} ({ticketRate:0}/hr)");
    }

    /// <summary>Console output stays printable ASCII (docs/style-guides/encoding.md); names may not be.</summary>
    private static string Ascii(string text)
    {
        return text.All(static letter => letter >= ' ' && letter <= '~')
            ? text
            : string.Concat(text.Select(static letter => letter >= ' ' && letter <= '~' ? letter : '?'));
    }

    private static string Rate(decimal value, TimeSpan window) =>
        string.Create(CultureInfo.InvariantCulture, $"{MetricSnapshot.Compute(value, window).PerHour:0.#}");
}
