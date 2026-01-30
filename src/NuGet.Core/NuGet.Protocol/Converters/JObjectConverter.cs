// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Converters
{
    /// <summary>
    /// A System.Text.Json converter for Newtonsoft.Json.Linq.JObject.
    /// </summary>
    internal sealed class JObjectConverter : JsonConverter<JObject>
    {
        public override JObject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            // Use JsonDocument to read the JSON and then convert to string
            using (JsonDocument document = JsonDocument.ParseValue(ref reader))
            {
                string json = document.RootElement.GetRawText();
                return JObject.Parse(json);
            }
        }

        public override void Write(Utf8JsonWriter writer, JObject value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            // Convert JObject to string and parse it as JsonDocument to write
            string json = value.ToString(Newtonsoft.Json.Formatting.None);
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                document.RootElement.WriteTo(writer);
            }
        }
    }
}
