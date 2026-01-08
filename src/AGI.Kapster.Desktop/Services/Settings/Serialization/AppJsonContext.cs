using System.Text.Json.Serialization;
using AGI.Kapster.Desktop.Models;

namespace AGI.Kapster.Desktop.Services.Serialization;

// System.Text.Json source generation context for trimmer-safe (AOT) serialization
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(HotkeySettings))]
[JsonSerializable(typeof(HotkeyGesture))]
[JsonSerializable(typeof(HotkeyKeySpec))]
[JsonSerializable(typeof(CharKeySpec))]
[JsonSerializable(typeof(NamedKeySpec))]
[JsonSerializable(typeof(NamedKey))]
[JsonSerializable(typeof(HotkeyModifiers))]
internal partial class AppJsonContext : JsonSerializerContext
{
}


