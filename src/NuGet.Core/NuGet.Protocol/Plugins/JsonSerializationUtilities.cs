// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// JSON serialization/deserialization utilities.
    /// </summary>
    public static class JsonSerializationUtilities
    {
        /// <summary>
        /// Gets the JSON serializer.
        /// </summary>
        public static Newtonsoft.Json.JsonSerializer Serializer
        {
            get;
        }

        static JsonSerializationUtilities()
        {
            Serializer = Newtonsoft.Json.JsonSerializer.Create(new JsonSerializerSettings()
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
        public static T Deserialize<T>(string json)
            where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(json));
            }

            using (var stringReader = new StringReader(json))
            using (var jsonReader = new JsonTextReader(stringReader))
            {
                return Serializer.Deserialize<T>(jsonReader);
            }
        }

        /// <summary>
        /// Deserializes an object from the provided JSON using System.Text.Json with source generation.
        /// </summary>
        /// <typeparam name="T">The deserialization type.</typeparam>
        /// <param name="json">JSON to deserialize.</param>
        /// <param name="jsonTypeInfo">The JSON type info for AOT-friendly deserialization.</param>
        /// <returns>An instance of <typeparamref name="T" />.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="json" />
        /// is either <see langword="null" /> or an empty string.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="jsonTypeInfo" /> is <see langword="null" />.</exception>
        public static T Deserialize<T>(string json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo)
            where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(json));
            }

            if (jsonTypeInfo == null)
            {
                throw new ArgumentNullException(nameof(jsonTypeInfo));
            }

            return System.Text.Json.JsonSerializer.Deserialize(json, jsonTypeInfo);
        }

        /// <summary>
        /// Serializes an object.
        /// </summary>
        /// <param name="value">An object to serialize.</param>
        /// <returns>A <see cref="JObject" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value" /> is <see langword="null" />.</exception>
        public static JObject FromObject(object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return JObject.FromObject(value, Serializer);
        }

        /// <summary>
        /// Serializes an object using System.Text.Json with source generation.
        /// </summary>
        /// <typeparam name="T">The type of the value to serialize.</typeparam>
        /// <param name="value">An object to serialize.</param>
        /// <param name="jsonTypeInfo">The JSON type info for AOT-friendly serialization.</param>
        /// <returns>A <see cref="JObject" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="jsonTypeInfo" /> is <see langword="null" />.</exception>
        public static JObject FromObject<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> jsonTypeInfo)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (jsonTypeInfo == null)
            {
                throw new ArgumentNullException(nameof(jsonTypeInfo));
            }

            // Serialize to JSON string using System.Text.Json
            string json = System.Text.Json.JsonSerializer.Serialize(value, jsonTypeInfo);

            // Parse the JSON string into a JObject for compatibility
            return JObject.Parse(json);
        }

        /// <summary>
        /// Serializes an object to the provided writer.
        /// </summary>
        /// <param name="writer">A JSON writer.</param>
        /// <param name="value">The value to serialize.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value" /> is <see langword="null" />.</exception>
        public static void Serialize(JsonWriter writer, object value)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            Serializer.Serialize(writer, value);
        }

        /// <summary>
        /// Serializes a Message to the provided writer using System.Text.Json.
        /// </summary>
        /// <param name="writer">A JSON writer.</param>
        /// <param name="message">The Message to serialize.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="writer" /> or <paramref name="message" /> is <see langword="null" />.</exception>
        public static void Serialize(System.Text.Json.Utf8JsonWriter writer, Message message)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            writer.WriteStartObject();

            writer.WriteString("RequestId", message.RequestId);
            writer.WriteString("Type", message.Type.ToString());
            writer.WriteString("Method", message.Method.ToString());

            if (message.Payload != null)
            {
                writer.WritePropertyName("Payload");
                // Convert JObject to string and write it as raw JSON
                string payloadJson = message.Payload.ToString(Formatting.None);
                using (var doc = System.Text.Json.JsonDocument.Parse(payloadJson))
                {
                    doc.RootElement.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Deserializes an object.
        /// </summary>
        /// <typeparam name="T">The deserialization type.</typeparam>
        /// <param name="jObject">A JSON object.</param>
        /// <returns>An instance of <typeparamref name="T" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="jObject" /> is <see langword="null" />.</exception>
        public static T ToObject<T>(JObject jObject)
        {
            if (jObject == null)
            {
                throw new ArgumentNullException(nameof(jObject));
            }

            return jObject.ToObject<T>(Serializer);
        }
    }
}
