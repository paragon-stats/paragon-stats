namespace ParagonStats.Core.Tui;

/// <summary>
/// Everything a screen is allowed to know: the engine's state plus the two bits
/// of environment the chrome shows. Deliberately not folded into
/// <see cref="Tui.Snapshot"/>, which is engine data - the version and the log
/// root are facts about this run, not about the sessions.
/// </summary>
/// <param name="Version">Stamped at build time; shown so a bug report can name it.</param>
/// <param name="Root">The resolved directory being read, so the header names what is actually in use.</param>
/// <param name="Snapshot">The sessions as of this frame.</param>
public sealed record Readout(string Version, string Root, Snapshot Snapshot);
