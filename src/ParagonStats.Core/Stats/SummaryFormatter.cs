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
            sb.AppendLine(CultureInfo.InvariantCulture, $"{session.Character} ({session.Account}) {session.Start:yyyy-MM-dd HH:mm} +{span:hh\\:mm\\:ss}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  damage {session.Stats.TotalDamage:0.##} | defeats {session.Stats.Defeats} | xp {session.Stats.Experience} | inf {session.Stats.Influence} | activations {session.Stats.Activations}");
            long uncategorized = session.Stats.CategoryCounts.GetValueOrDefault(EventCategory.Uncategorized);
            long total = session.Messages.TotalCaptured;
            sb.AppendLine(CultureInfo.InvariantCulture, $"  lines {total} ({uncategorized} uncategorized)");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"sessions {result.Sessions.Count} | unattributed lines {result.UnattributedCount}");
        return sb.ToString();
    }
}
