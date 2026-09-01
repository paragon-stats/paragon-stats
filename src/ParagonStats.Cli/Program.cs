using ParagonStats.Core.Stats;

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, e) =>
{
    // First Ctrl+C: graceful (watch prints its final summary). Second: kill.
    e.Cancel = !cancellation.IsCancellationRequested;
    cancellation.Cancel();
};

return CliRunner.Run(args, Console.Out, Console.Error, CliEnvironment.Production(cancellation.Token));
