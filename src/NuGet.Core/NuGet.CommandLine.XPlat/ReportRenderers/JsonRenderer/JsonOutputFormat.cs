// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

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

        private static void WriteProjectArray(JsonWriter writer, List<ReportProject> reportProjects)
        {
            writer.WritePropertyName(ProjectsProperty);

            writer.WriteStartArray();
            foreach (ReportProject reportProject in reportProjects)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(PathProperty);
                writer.WriteValue(reportProject.Path);

                if (reportProject.FrameworkPackages.Count > 0)
                {
                    writer.WritePropertyName(FrameworksProperty);

                    WriteFrameworkPackage(writer, reportProject.FrameworkPackages);

                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static void WriteFrameworkPackage(JsonWriter writer, List<ReportFrameworkPackage> reportFrameworkPackages)
        {
            writer.WriteStartArray();
            foreach (ReportFrameworkPackage reportFrameworkPackage in reportFrameworkPackages)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(FrameworkProperty);
                writer.WriteValue(reportFrameworkPackage.FrameWork);
                WriteTopLevelPackages(writer, TopLevelPackagesProperty, reportFrameworkPackage.TopLevelPackages);
                WriteTransitivePackages(writer, TransitivePackagesProperty, reportFrameworkPackage.TransitivePackages);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static void WriteTopLevelPackages(JsonWriter writer, string property, List<TopLevelPackage> topLevelPackages)
        {
            if (topLevelPackages?.Count > 0)
            {
                writer.WritePropertyName(property);

                writer.WriteStartArray();
                foreach (TopLevelPackage topLevelPackage in topLevelPackages)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(IdProperty);
                    writer.WriteValue(topLevelPackage.PackageId);

                    writer.WritePropertyName(RequestedVersionProperty);
                    writer.WriteValue(topLevelPackage.RequestedVersion);

                    writer.WritePropertyName(ResolvedVersionProperty);
                    writer.WriteValue(topLevelPackage.ResolvedVersion);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
        }

        private static void WriteTransitivePackages(JsonWriter writer, string property, List<TransitivePackage> transitivePackages)
        {
            if (transitivePackages?.Count > 0)
            {
                writer.WritePropertyName(property);

                writer.WriteStartArray();
                foreach (TransitivePackage transitivePackage in transitivePackages)
                {
                    writer.WritePropertyName(IdProperty);
                    writer.WriteValue(transitivePackage.PackageId);

                    writer.WritePropertyName(ResolvedVersionProperty);
                    writer.WriteValue(transitivePackage.ResolvedVersion);
                }
                writer.WriteEndArray();
            }
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
                    // Not implemented yet
                    //JsonUtility.WriteObject(writer, jsonOutputContent.Problems, WriteProblem);
                }

                if (jsonOutputContent.Sources.Count > 0)
                {
                    writer.WritePropertyName(SourcesProperty);
                    // Not implemented yet
                    //JsonUtility.WriteObject(writer, jsonOutputContent.Sources, WriteSource);
                }

                if (jsonOutputContent.Projects.Count > 0)
                {
                    WriteProjectArray(writer, jsonOutputContent.Projects);
                }

                writer.WriteEndObject();
            }
        }
    }
}
