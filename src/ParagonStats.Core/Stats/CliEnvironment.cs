namespace ParagonStats.Core.Stats;

/// <summary>
/// The CLI's touchpoints with the outside world, injectable so every flow is
/// testable and coverage-measured: interactive input, the config location,
/// the game-client-process check, and watch-loop pacing.
/// </summary>
public sealed class CliEnvironment
{
    /// <summary>
    /// Overrides where the saved game location lives. Without it the config
    /// path is fixed to %APPDATA%, so exercising the no-argument and
    /// first-launch flows means writing over a real user's config - which is
    /// why those flows went untested and shipped a binary with no `--help`
    /// (#245). Matches the existing PARAGON_CORPUS_DIR idiom.
    /// </summary>
    public const string ConfigPathVariable = "PARAGON_STATS_CONFIG";

    public TextReader? Input { get; init; }

    public string ConfigPath { get; init; } = DefaultConfigPath(Environment.GetEnvironmentVariable);

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

    /// <summary>
    /// Takes the reader rather than calling <see cref="Environment"/> directly, so
    /// both branches are testable without mutating process state that other
    /// tests share.
    /// </summary>
    internal static string DefaultConfigPath(Func<string, string?> readVariable)
    {
        ArgumentNullException.ThrowIfNull(readVariable);
        string? configured = readVariable(ConfigPathVariable);
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "paragon-stats", "config.json")
            : configured;
    }

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
