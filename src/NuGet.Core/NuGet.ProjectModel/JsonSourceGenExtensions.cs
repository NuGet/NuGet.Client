
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NuGet.ProjectModel;

internal static class JsonSourceGenExtensions
{
    public static JsonTypeInfo<T>? GetTypeInfo<T>(this JsonSerializerOptions options)
    {
        JsonTypeInfo? typeInfo = options.TypeInfoResolver?.GetTypeInfo(typeof(T), options);

        return typeInfo != null ? typeInfo as JsonTypeInfo<T> : null;
    }

    public static JsonSerializerOptions WithSourceGenContext(this JsonSerializerOptions options, JsonSerializerContext context)
    {
        var newOptions = new JsonSerializerOptions(options)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(context, options.TypeInfoResolver)
        };
        return newOptions;
    }
}
