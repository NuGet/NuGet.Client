// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NuGet.CommandLine.XPlat.Utility;

namespace NuGet.CommandLine.XPlat.ReportRenderers.JsonRenderer
{
    internal static class JsonOutputFormat
    {
        public const int Version = 1;

        private const string VersionProperty = "version";
        private const string ParametersProperty = "parameters";
        private const string ProblemsProperty = "problems";
        private const string SourcesProperty = "sources";
        private const string ProjectsProperty = "projects";
        private const string FrameworksProperty = "frameworks";
        private const string FrameworkProperty = "framework";
        private const string PathProperty = "path";
        private const string TopLevelPackagesProperty = "topLevelPackages";
        private const string TransitivePackagesProperty = "transitivePackages";
        private const string IdProperty = "id";
        private const string RequestedVersionProperty = "requestedVersion";
        private const string ResolvedVersionProperty = "resolvedVersion";

        private static readonly JsonSerializer JsonSerializer = JsonSerializer.Create(GetSerializerSettings());

        public static string Render(JsonOutputContent jsonOutputContent)
        {
            using (var writer = new StringWriter())
            {
                Write(writer, jsonOutputContent);
                return writer.ToString();
            }
        }

        public static void Write(StringWriter stringWriter, JsonOutputContent jsonOutputContent)
        {
            using (var jsonWriter = new JsonTextWriter(stringWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                JsonSerializer.Serialize(jsonWriter, jsonOutputContent);
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

        private static void WriteProblem(JsonWriter writer, ReportProblem renderProblem)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(renderProblem.Message);
            writer.WriteValue(renderProblem.Message);

            writer.WriteEndObject();
        }

        private static void WriteSource(JsonWriter writer, string source)
        {
            writer.WriteStartObject();

            writer.WriteValue(source);

            writer.WriteEndObject();
        }

        private static void WriteProject(JsonWriter writer, ReportProject reportProject)
        {
            //writer.WriteStartObject();

            writer.WritePropertyName(PathProperty);
            writer.WriteValue(reportProject.Path);

            writer.WritePropertyName(FrameworksProperty);
            JsonUtility.WriteObject(writer, reportProject.FrameworkPackages, WriteFrameworkPackage);

            //writer.WriteEndObject();
        }

        private static void WriteFrameworkPackage(JsonWriter writer, ReportFrameworkPackage reportFrameworkPackage)
        {
            //writer.WriteStartObject();

            writer.WritePropertyName(FrameworkProperty);
            writer.WriteValue(reportFrameworkPackage.FrameWork);

            if (reportFrameworkPackage.TopLevelPackages?.Count > 0)
            {
                writer.WritePropertyName(TopLevelPackagesProperty);
                JsonUtility.WriteObject(writer, reportFrameworkPackage.TopLevelPackages, WriteTopLevelPackage);
            }

            if (reportFrameworkPackage.TransitivePackages?.Count > 0)
            {
                writer.WritePropertyName(TransitivePackagesProperty);
                JsonUtility.WriteObject(writer, reportFrameworkPackage.TransitivePackages, WriteTransitivePackage);
            }

            //writer.WriteEndObject();
        }

        private static void WriteTopLevelPackage(JsonWriter writer, TopLevelPackage topLevelPackage)
        {
            //writer.WriteStartObject();

            writer.WritePropertyName(IdProperty);
            writer.WriteValue(topLevelPackage.PackageId);

            writer.WritePropertyName(RequestedVersionProperty);
            writer.WriteValue(topLevelPackage.RequestedVersion);

            writer.WritePropertyName(ResolvedVersionProperty);
            writer.WriteValue(topLevelPackage.ResolvedVersion);

            //writer.WriteEndObject();
        }

        private static void WriteTransitivePackage(JsonWriter writer, TransitivePackage transitivePackage)
        {
            //writer.WriteStartObject();

            writer.WritePropertyName(IdProperty);
            writer.WriteValue(transitivePackage.PackageId);

            writer.WritePropertyName(ResolvedVersionProperty);
            writer.WriteValue(transitivePackage.ResolvedVersion);

            //writer.WriteEndObject();
        }

        private class JsonOutputConverter : JsonConverter
        {
            internal static JsonOutputConverter Default { get; } = new JsonOutputConverter();

            private static readonly Type TargetType = typeof(JsonOutputContent);
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

                if (jsonOutputContent.Problems.Count > 0)
                {
                    writer.WritePropertyName(ProblemsProperty);
                    JsonUtility.WriteObject(writer, jsonOutputContent.Problems, WriteProblem);
                }

                if (jsonOutputContent.Sources.Count > 0)
                {
                    writer.WritePropertyName(SourcesProperty);
                    JsonUtility.WriteObject(writer, jsonOutputContent.Sources, WriteSource);
                }

                if (jsonOutputContent.Projects.Count > 0)
                {
                    writer.WritePropertyName(ProjectsProperty);
                    JsonUtility.WriteObject(writer, jsonOutputContent.Projects, WriteProject);
                }

                writer.WriteEndObject();
            }
        }
    }
}
