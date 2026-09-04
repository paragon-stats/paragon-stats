using System.Globalization;
using System.Runtime.InteropServices;

namespace ParagonStats.Core.Sessions;

/// <summary>
/// What the lines nobody could be shown to have earned were worth. Carried as
/// one value so every surface says the same thing: a count alone cannot tell
/// nine lines of login chatter from a fifth of a farm, and both read as
/// "unattributed" (#251).
/// </summary>
/// <param name="Experience">XP on the unattributed books.</param>
/// <param name="Influence">Influence on the unattributed books.</param>
/// <param name="Tickets">
/// AE tickets. An architect farm pays tickets INSTEAD of influence, so a report
/// that values only XP and influence calls its best sessions worthless.
/// </param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct UnattributedValue(long Experience, long Influence, long Tickets)
{
    /// <summary>Whether there is anything worth saying. Nothing is not news.</summary>
    public bool Any => Experience > 0 || Influence > 0 || Tickets > 0;

    /// <summary>The one rendering, so the readout and the summary cannot drift apart.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"xp {Experience} | inf {Influence} | tickets {Tickets}");
}
