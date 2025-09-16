using System.Text.Json.Serialization;
using AGI.Captor.Desktop.Models;

namespace AGI.Captor.Desktop.Services.Serialization;

// System.Text.Json source generation context for trimmer-safe (AOT) serialization
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppJsonContext : JsonSerializerContext
{
}


