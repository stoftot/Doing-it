using System.Text.Json.Serialization;

namespace DoingIt.Core;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PlannerData))]
internal partial class PlannerJsonContext : JsonSerializerContext;
