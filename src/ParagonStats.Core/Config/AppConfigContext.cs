using System.Text.Json.Serialization;

namespace ParagonStats.Core.Config;

/// <summary>Source-generated JSON - the PRD AOT-safe serialization decision.</summary>
[JsonSerializable(typeof(AppConfig))]
internal sealed partial class AppConfigContext : JsonSerializerContext;
