// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Newtonsoft.Json;

namespace NuGet.CommandLine.XPlat.ReportRenderers.JsonRenderers
{
    internal static class JsonOutputFormat
    {
        public const int Version = 1;

        private const string VersionProperty = "version";
        private const string ParametersProperty = "parameters";
        private const string ProblemsProperty = "problems";
        private const string SourcesProperty = "sources";
        private const string ProjectsProperty = "projects";

        private static readonly JsonSerializer JsonSerializer = JsonSerializer.Create(GetSerializerSettings());

        public static string Render(JsonOutputContent jsonOutputContent)
        {
            using (var writer = new StringWriter())
            {
                Write(writer, jsonOutputContent);
                return writer.ToString();
            }
        }

        public static void Write(StringWriter stringWriter, JsonOutputContent hashFile)
        {
            using (var jsonWriter = new JsonTextWriter(stringWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                JsonSerializer.Serialize(jsonWriter, hashFile);
            }
        }

        private static JsonSerializerSettings GetSerializerSettings()
        {
            var settings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented
            };
            settings.Converters.Add(JsonOutputConverter.Default);
            return settings;
        }

        private class JsonOutputConverter : JsonConverter
        {
            internal static JsonOutputConverter Default { get; } = new JsonOutputConverter();

            private static readonly Type TargetType = typeof(JsonOutputConverter);
            public override bool CanConvert(Type objectType)
            {
                return objectType == TargetType;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (!(value is JsonOutputContent jsonOutputContent))
                {
                    throw new ArgumentException(message: "value is not of type JsonOutputContent", paramName: nameof(value));
                }

                writer.WriteStartObject();

                writer.WritePropertyName(VersionProperty);
                writer.WriteValue(jsonOutputContent.Version);

                writer.WritePropertyName(ParametersProperty);
                writer.WriteValue(jsonOutputContent.Parameters);

                writer.WritePropertyName(ProblemsProperty);
                writer.WriteValue(jsonOutputContent.Problems);

                writer.WritePropertyName(SourcesProperty);
                writer.WriteValue(jsonOutputContent.Sources);

                writer.WritePropertyName(ProjectsProperty);
                writer.WriteValue(jsonOutputContent.Projects);

                writer.WriteEndObject();
            }
        }
    }
}
