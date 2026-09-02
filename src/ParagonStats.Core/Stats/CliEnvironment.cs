namespace ParagonStats.Core.Stats;

/// <summary>
/// The CLI's touchpoints with the outside world, injectable so every flow is
/// testable and coverage-measured: interactive input, the config location,
/// the game-client-process check, and watch-loop pacing.
/// </summary>
public sealed class CliEnvironment
{
    public TextReader? Input { get; init; }

    public string ConfigPath { get; init; } =
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "paragon-stats", "config.json");

    public Func<bool> ClientRunning { get; init; } = static () => true;

    // Pacing, not time measurement: TimeProvider (the codebase's clock seam)
    // has no sleep primitive, so the loop delay is its own injectable.
    public Action<int> Sleep { get; init; } = Thread.Sleep;

    public CancellationToken Token { get; init; }

    /// <summary>
    /// The production wiring, in Core so the coverage gate sees it. The
    /// game-client check is name-only and deliberately conservative: a
    /// collision keeps sessions open (idle timeout and in-log triggers still
    /// close them); a miss is edge-guarded in LiveMonitor.
    /// </summary>
    public static CliEnvironment Production(CancellationToken token) => new()
    {
        Input = Console.In,
        ClientRunning = static () => ClientProcessRunning(static () => System.Diagnostics.Process.GetProcessesByName("cityofheroes")),
        Token = token,
    };

    /// <summary>Disposes the handles it was given; testable with any real process list.</summary>
    internal static bool ClientProcessRunning(Func<System.Diagnostics.Process[]> query)
    {
        try
        {
            return AnyRunning(query());
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return true; // cannot tell: never force-close a live session over it
        }
    }

    internal static bool AnyRunning(System.Diagnostics.Process[] processes)
    {
        foreach (System.Diagnostics.Process process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }
}
