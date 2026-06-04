// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NuGet.Shared;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// JSON serialization/deserialization utilities.
    /// </summary>
    public static class JsonSerializationUtilities
    {
        internal const string NsjSerializationMessage = "This method uses Newtonsoft.Json reflection-based serialization which is incompatible with trimming.";
        internal const string NsjDynamicCodeMessage = "This method uses Newtonsoft.Json which requires dynamic code generation.";

        /// <summary>
        /// Gets the JSON serializer.
        /// </summary>
        public static JsonSerializer Serializer => NsjSerializerHolder.Instance;

        // Nested class defers static initialization until first access.
        // When the feature switch is enabled and trimming is active,
        // all callers are gated so this class is never loaded and gets trimmed.
#if NET5_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This type is only loaded when the NSJ code path is active (feature switch disabled). The linker removes it when trimming with the switch enabled.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "This type is only loaded when the NSJ code path is active (feature switch disabled). The linker removes it when trimming with the switch enabled.")]
#endif
        private static class NsjSerializerHolder
        {
            internal static readonly JsonSerializer Instance = JsonSerializer.Create(new JsonSerializerSettings()
            {
                Converters = new JsonConverter[]
                {
                    new SemanticVersionConverter(),
                    new StringEnumConverter(),
                    new VersionRangeConverter()
                },
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        /// <summary>
        /// Deserializes an object from the provided JSON.
        /// </summary>
        /// <typeparam name="T">The deserialization type.</typeparam>
        /// <param name="json">JSON to deserialize.</param>
        /// <returns>An instance of <typeparamref name="T" />.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="json" />
        /// is either <see langword="null" /> or an empty string.</exception>
#if NET5_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NsjSerializationMessage)]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NsjDynamicCodeMessage)]
#endif
        public static T Deserialize<T>(string json)
            where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(json));
            }

            if (NuGetFeatureFlags.UseSystemTextJsonDeserializationFeatureSwitch)
            {
                throw new NotSupportedException("NSJ deserialization is not supported when STJ feature switch is enabled.");
            }

            using (var stringReader = new StringReader(json))
            using (var jsonReader = new JsonTextReader(stringReader))
            {
                return Serializer.Deserialize<T>(jsonReader);
            }
        }

        /// <summary>
        /// Serializes an object.
        /// </summary>
        /// <param name="value">An object to serialize.</param>
        /// <returns>A <see cref="JObject" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value" /> is <see langword="null" />.</exception>
#if NET5_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NsjSerializationMessage)]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NsjDynamicCodeMessage)]
#endif
        public static JObject FromObject(object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (NuGetFeatureFlags.UseSystemTextJsonDeserializationFeatureSwitch)
            {
                throw new NotSupportedException("NSJ serialization is not supported when STJ feature switch is enabled.");
            }

            return JObject.FromObject(value, Serializer);
        }

        /// <summary>
        /// Serializes an object to the provided writer.
        /// </summary>
        /// <param name="writer">A JSON writer.</param>
        /// <param name="value">The value to serialize.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value" /> is <see langword="null" />.</exception>
#if NET5_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NsjSerializationMessage)]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NsjDynamicCodeMessage)]
#endif
        public static void Serialize(JsonWriter writer, object value)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (NuGetFeatureFlags.UseSystemTextJsonDeserializationFeatureSwitch)
            {
                throw new NotSupportedException("NSJ serialization is not supported when STJ feature switch is enabled.");
            }

            Serializer.Serialize(writer, value);
        }

        /// <summary>
        /// Deserializes an object.
        /// </summary>
        /// <typeparam name="T">The deserialization type.</typeparam>
        /// <param name="jObject">A JSON object.</param>
        /// <returns>An instance of <typeparamref name="T" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="jObject" /> is <see langword="null" />.</exception>
#if NET5_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NsjSerializationMessage)]
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NsjDynamicCodeMessage)]
#endif
        public static T ToObject<T>(JObject jObject)
        {
            if (jObject == null)
            {
                throw new ArgumentNullException(nameof(jObject));
            }

            if (NuGetFeatureFlags.UseSystemTextJsonDeserializationFeatureSwitch)
            {
                throw new NotSupportedException("NSJ deserialization is not supported when STJ feature switch is enabled.");
            }

            return jObject.ToObject<T>(Serializer);
        }
    }
}
