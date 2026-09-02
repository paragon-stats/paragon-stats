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
    /// (#245). Matches the existing PARAGON_SOURCE_DIR idiom.
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
    /// Whether the text UI may paint. Default false, so every existing test and
    /// every redirected run keeps today's plain output byte for byte; only the
    /// production wiring turns it on, and only when it has a real terminal.
    /// </summary>
    public bool Interactive { get; init; }

    /// <summary>
    /// A keypress if one is waiting, otherwise null. Non-blocking by contract:
    /// the readout has to keep repainting while nobody is typing, or the clock
    /// stops advancing on screen.
    /// </summary>
    public Func<char?> ReadKey { get; init; } = static () => null;

    /// <summary>
    /// The terminal's character grid, read every frame so the readout follows a
    /// resized window instead of being hand-tuned to one size. Falls back to the
    /// 120x12 strip when there is no console to ask.
    /// </summary>
    public Func<(int Width, int Height)> WindowSize { get; init; } = static () => (120, 12);

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

        // Both must hold: a real console to paint into, and a terminal that
        // interprets the escapes. Under legacy conhost virtual-terminal
        // processing is off by default, and painting ANSI into it would print
        // literal garbage - worse than the plain output it replaced.
        Interactive = !Console.IsOutputRedirected && !Console.IsInputRedirected && Tui.VirtualTerminal.TryEnable(),

        // Guarded: Console.KeyAvailable throws outright when stdin is
        // redirected, and this property is constructed even on runs that never
        // enter the text UI.
        ReadKey = static () => !Console.IsInputRedirected && Console.KeyAvailable
            ? Console.ReadKey(intercept: true).KeyChar
            : null,
        WindowSize = static () => ConsoleSize(),
    };

    /// <summary>
    /// Asking the console its size throws when there is no console attached, and
    /// a zero would render a frame with no cells. Both fall back to the strip.
    /// </summary>
    internal static (int Width, int Height) ConsoleSize() =>
        ConsoleSize(static () => (Console.WindowWidth, Console.WindowHeight));

    /// <summary>
    /// Takes the reader so both failure modes are testable on a host that has a
    /// perfectly good console, the same way the config path does.
    /// </summary>
    internal static (int Width, int Height) ConsoleSize(Func<(int Width, int Height)> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        try
        {
            (int width, int height) = read();
            return width > 0 && height > 0 ? (width, height) : (120, 12);
        }
        catch (IOException)
        {
            return (120, 12);
        }
    }

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
