namespace ParagonStats.Core.Tui;

/// <summary>
/// One column of the readout. This is the seam CP2's metrics plug into: a new
/// metric is a new descriptor in <see cref="Columns.Default"/>, not a change to
/// any layout code. It is also the single place a feature can be declared, so
/// the GUI in CP4 binds the same list rather than growing its own.
/// </summary>
/// <param name="Header">Column heading, shown as written.</param>
/// <param name="Width">Character width; longer values are clipped by the frame.</param>
/// <param name="Value">Renders one row's cell. Formatting lives here, never in a screen.</param>
/// <param name="RightAligned">Numbers read better right-aligned; names do not.</param>
public sealed record Column(string Header, int Width, Func<SessionRow, string> Value, bool RightAligned = false);
