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
        StringBuilder sb = new();
        foreach (CharacterSession session in result.Sessions)
        {
            TimeSpan span = session.LastSeen - session.Start;
            sb.AppendLine(CultureInfo.InvariantCulture, $"{session.Character} ({session.Account}) {session.Start:yyyy-MM-dd HH:mm} +{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  damage {session.Stats.TotalDamage:0.##} | defeats {session.Stats.Defeats} | xp {session.Stats.Experience} | inf {session.Stats.Influence} | activations {session.Stats.Activations}");
            string categories = string.Join(
                " | ",
                session.Stats.CategoryCounts
                    .OrderBy(c => c.Key)
                    .Select(c => string.Create(CultureInfo.InvariantCulture, $"{c.Key} {c.Value}")));
            sb.AppendLine(CultureInfo.InvariantCulture, $"  lines {session.Messages.TotalCaptured}: {categories}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"sessions {result.Sessions.Count} | unattributed lines {result.UnattributedCount}");
        return sb.ToString();
    }
}
