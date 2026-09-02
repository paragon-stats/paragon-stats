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

    /// <summary>
    /// Forces the text UI on with plain rendering, so it can be driven through
    /// a pipe. Without it the readout is unprovable in CI: redirected output
    /// falls back to the batch summary, which is exactly the path that was
    /// already covered while the interactive one shipped broken. Keys are then
    /// read from stdin, and end-of-input quits - so piping nothing renders one
    /// frame and exits.
    /// </summary>
    public const string ForceTuiVariable = "PARAGON_STATS_TUI";

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
    /// Whether frames are painted as ANSI. Separate from <see cref="Interactive"/>
    /// because "show the readout" and "emit escape sequences" are different
    /// questions: a piped run wants the first without the second.
    /// </summary>
    public bool Ansi { get; init; }

    /// <summary>
    /// The production wiring, in Core so the coverage gate sees it. The
    /// game-client check is name-only and deliberately conservative: a
    /// collision keeps sessions open (idle timeout and in-log triggers still
    /// close them); a miss is edge-guarded in LiveMonitor.
    /// </summary>
    public static CliEnvironment Production(CancellationToken token) =>
        Production(Environment.GetEnvironmentVariable(ForceTuiVariable), token);

    /// <summary>Takes the override so both wirings are testable without touching process state.</summary>
    internal static CliEnvironment Production(string? forceTui, CancellationToken token)
    {
        // Both must hold for a painted readout: a real console, and a terminal
        // that interprets the escapes. Under legacy conhost virtual-terminal
        // processing is off by default, and painting ANSI into it would print
        // literal garbage - worse than the plain output it replaced.
        bool terminal = !Console.IsOutputRedirected && !Console.IsInputRedirected && Tui.VirtualTerminal.TryEnable();
        bool forced = !string.IsNullOrWhiteSpace(forceTui);

        return new CliEnvironment
        {
            Input = Console.In,
            ClientRunning = static () => ClientProcessRunning(static () => System.Diagnostics.Process.GetProcessesByName("cityofheroes")),
            Token = token,
            Interactive = terminal || forced,
            Ansi = terminal,

            // Forced runs pin the strip. "Window size" is meaningless down a
            // pipe, and asking the console anyway would make golden frames
            // depend on whatever width the machine running CI happens to have.
            WindowSize = forced ? static () => (120, 13) : static () => ConsoleSize(),

            // Forced runs read keys from the pipe, and treat end-of-input as a
            // quit so a run with nothing piped renders one frame and exits.
            ReadKey = forced ? () => ReadPiped(Console.In) : ReadConsole,
        };
    }

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

    /// <summary>
    /// Takes the reader rather than reaching for <see cref="Console"/>, so the
    /// end-of-input rule is testable without mutating process-wide state that
    /// parallel tests share.
    /// </summary>
    internal static char? ReadPiped(TextReader input)
    {
        ArgumentNullException.ThrowIfNull(input);
        int next = input.Read();

        // End of input quits, so a forced run with nothing piped renders one
        // frame and exits rather than spinning forever against a dead stream.
        return next < 0 ? 'q' : (char)next;
    }

    /// <summary>
    /// Guarded: Console.KeyAvailable throws outright when stdin is redirected,
    /// and this is wired on every run, including ones that never paint.
    /// </summary>
    private static char? ReadConsole() =>
        !Console.IsInputRedirected && Console.KeyAvailable ? Console.ReadKey(intercept: true).KeyChar : null;
}
