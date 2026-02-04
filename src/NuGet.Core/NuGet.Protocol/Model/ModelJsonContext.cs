
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Plugins;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Model.V3SearchResults))]
internal partial class ModelJsonContext : JsonSerializerContext
{
}
