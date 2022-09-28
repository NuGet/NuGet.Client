// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NuGet.CommandLine.XPlat.ReportRenderers.Models;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Configuration;
using NuGet.Protocol;

namespace NuGet.CommandLine.XPlat.ReportRenderers.ListPackageJsonRenderer
{
    internal static class ListPackageJsonOutputSerializer
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
        private const string SeverityProperty = "severity";
        private const string AdvisoryUrlProperty = "advisoryurl";
        private const string VulnerabilitiesProperty = "vulnerabilities";
        private const string LatestVersionProperty = "latestVersion";

        private static readonly JsonSerializer JsonSerializer = JsonSerializer.Create(GetSerializerSettings());
        internal static ListPackageArgs ListPackageArgs;

        public static string Render(ListPackageJsonOutputContent jsonOutputContent)
        {
            using (var writer = new StringWriter())
            {
                ListPackageArgs = jsonOutputContent.ListPackageArgs;

                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    jsonWriter.Formatting = Formatting.Indented;
                    JsonSerializer.Serialize(jsonWriter, jsonOutputContent);
                }

                return writer.ToString();
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

        private static void WriteSourcesArray(JsonWriter writer, IEnumerable<PackageSource> packageSources)
        {
            writer.WritePropertyName(SourcesProperty);

            writer.WriteStartArray();
            foreach (PackageSource packageSource in packageSources)
            {
                writer.WriteValue(packageSource.Source);
            }
            writer.WriteEndArray();
        }

        private static void WriteProjectArray(JsonWriter writer, List<ListPackageProjectDetails> reportProjects)
        {
            writer.WritePropertyName(ProjectsProperty);

            writer.WriteStartArray();
            foreach (ListPackageProjectDetails reportProject in reportProjects)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(PathProperty);
                // Normalize the project path.
#if NETCOREAPP
                writer.WriteValue(reportProject.ProjectPath.Replace("\\", "/", StringComparison.Ordinal));
#else
                writer.WriteValue(reportProject.ProjectPath.Replace("\\", "/"));
#endif
                if (reportProject.TargetFrameworkPackages?.Count > 0)
                {
                    writer.WritePropertyName(FrameworksProperty);

                    WriteFrameworkPackage(writer, reportProject.TargetFrameworkPackages);

                }

                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static void WriteFrameworkPackage(JsonWriter writer, List<ListPackageReportFrameworkPackage> reportFrameworkPackages)
        {
            writer.WriteStartArray();
            foreach (ListPackageReportFrameworkPackage reportFrameworkPackage in reportFrameworkPackages)
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

                    if (ListPackageArgs.ReportType == ReportType.Outdated)
                    {
                        writer.WritePropertyName(LatestVersionProperty);
                        writer.WriteValue(topLevelPackage.LatestVersion);
                    }

                    if (ListPackageArgs.ReportType == ReportType.Vulnerable)
                    {
                        WriteVulnerabilities(writer, topLevelPackage.Vulnerabilities);
                    }

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
                    writer.WriteStartObject();
                    writer.WritePropertyName(IdProperty);
                    // Here for ReportType.Vulnerable: Substring(s) removes "> " from front.
                    string packageId = ListPackageArgs.ReportType == ReportType.Vulnerable ?
                        transitivePackage.PackageId.Substring(2) : transitivePackage.PackageId;
                    writer.WriteValue(packageId);

                    writer.WritePropertyName(ResolvedVersionProperty);
                    writer.WriteValue(transitivePackage.ResolvedVersion);

                    if (ListPackageArgs.ReportType == ReportType.Outdated)
                    {
                        writer.WritePropertyName(LatestVersionProperty);
                        writer.WriteValue(transitivePackage.LatestVersion);
                    }

                    if (ListPackageArgs.ReportType == ReportType.Vulnerable)
                    {
                        WriteVulnerabilities(writer, transitivePackage.Vulnerabilities);
                    }

                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
        }

        private static void WriteVulnerabilities(JsonWriter writer, List<PackageVulnerabilityMetadata> vulnerabilities)
        {
            if (vulnerabilities == null)
            {
                return;
            }

            writer.WritePropertyName(VulnerabilitiesProperty);
            writer.WriteStartArray();

            foreach (PackageVulnerabilityMetadata vulnerability in vulnerabilities)
            {
                writer.WriteStartObject();
                string severity = (vulnerability?.Severity ?? -1) switch
                {
                    0 => "Low",
                    1 => "Moderate",
                    2 => "High",
                    3 => "Critical",
                    _ => string.Empty,
                };

                writer.WritePropertyName(SeverityProperty);
                writer.WriteValue(severity);

                writer.WritePropertyName(AdvisoryUrlProperty);
                writer.WriteValue(vulnerability.AdvisoryUrl);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private class JsonOutputConverter : JsonConverter
        {
            internal static JsonOutputConverter Default { get; } = new JsonOutputConverter();

            private static readonly Type TargetType = typeof(ListPackageJsonOutputContent);
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
                if (!(value is ListPackageJsonOutputContent jsonOutputContent))
                {
                    throw new ArgumentException(message: "value is not of type JsonOutputContent", paramName: nameof(value));
                }

                writer.WriteStartObject();

                writer.WritePropertyName(VersionProperty);
                writer.WriteValue(jsonOutputContent.Version);

                writer.WritePropertyName(ParametersProperty);

                // Normalize the path in parameters
#if NETCOREAPP
                writer.WriteValue(jsonOutputContent.Parameters.Replace("\\", "/", StringComparison.Ordinal));
#else
                writer.WriteValue(jsonOutputContent.Parameters.Replace("\\", "/"));
#endif

                if (jsonOutputContent.Problems?.Count > 0)
                {
                    writer.WritePropertyName(ProblemsProperty);
                    // Not implemented yet
                    //JsonUtility.WriteObject(writer, jsonOutputContent.Problems, WriteProblem);
                }

                if (ListPackageArgs.PackageSources.Any())
                {
                    WriteSourcesArray(writer, ListPackageArgs.PackageSources);
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
