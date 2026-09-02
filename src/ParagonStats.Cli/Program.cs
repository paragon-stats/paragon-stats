using ParagonStats.Core.Stats;

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, args) =>
{
    // First Ctrl+C: graceful (watch prints its final summary). Second: kill.
    args.Cancel = !cancellation.IsCancellationRequested;
    cancellation.Cancel();
};

return CliRunner.Run(args, Console.Out, Console.Error, CliEnvironment.Production(cancellation.Token));
