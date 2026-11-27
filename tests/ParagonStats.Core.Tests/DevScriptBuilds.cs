namespace ParagonStats.Core.Tests;

/// <summary>
/// Every class that runs a script with <see cref="DevScripts.RunScript"/>. Each
/// script build also builds the analyzer project into its own output folder, so
/// two script builds at once can collide; one collection never runs two at once.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DevScriptBuilds
{
    internal const string Name = "dev scripts";
}
