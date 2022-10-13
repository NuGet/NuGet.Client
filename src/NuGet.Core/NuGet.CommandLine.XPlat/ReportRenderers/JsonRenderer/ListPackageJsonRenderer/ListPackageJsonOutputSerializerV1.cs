// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{
    internal static class ListPackageJsonOutputSerializerV1
    {
        private const string VersionProperty = "version";
        private const string ParametersProperty = "parameters";
        private const string ProblemsProperty = "problems";
        private const string SourcesProperty = "sources";
        private const string ProjectProperty = "project";
        private const string ProjectsProperty = "projects";
        private const string FrameworksProperty = "frameworks";
        private const string FrameworkProperty = "framework";
        private const string PathProperty = "path";
        private const string TopLevelPackagesProperty = "topLevelPackages";
        private const string TransitivePackagesProperty = "transitivePackages";
        private const string IdProperty = "id";
        private const string RequestedVersionProperty = "requestedVersion";
        private const string ResolvedVersionProperty = "resolvedVersion";
        private const string AutoReferencedProperty = "autoReferenced";
        private const string SeverityProperty = "severity";
        private const string AdvisoryUrlProperty = "advisoryurl";
        private const string VulnerabilitiesProperty = "vulnerabilities";
        private const string LatestVersionProperty = "latestVersion";
        private const string DeprecationReasonsProperty = "deprecationReasons";
        private const string AlternativePackageProperty = "alternativePackage";
        private const string VersionRangeProperty = "versionRange";
        private const string MessageProperty = "message";

        private static readonly JsonSerializer JsonSerializer = JsonSerializer.Create(GetSerializerSettings());
        private static ListPackageArgs ListPackageArgs;

        internal static string Render(ListPackageOutputContentV1 jsonOutputContent)
        {
            ListPackageArgs = jsonOutputContent.ListPackageArgs;

            using (var writer = new StringWriter())
            {
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

        private static void WriteProblems(JsonWriter writer, IEnumerable<ReportProblem> reportProblems)
        {
            writer.WritePropertyName(ProblemsProperty);
            writer.WriteStartArray();

            foreach (ReportProblem reportProblem in reportProblems)
            {
                writer.WriteStartObject();

                if (!string.IsNullOrEmpty(reportProblem.Project))
                {
                    writer.WritePropertyName(ProjectProperty);
                    writer.WriteValue(NormalizeFilePath(reportProblem.Project));
                }

                writer.WritePropertyName(MessageProperty);
                writer.WriteValue((reportProblem.ProblemType == ProblemType.Warning ? "warn : " : string.Empty) + NormalizeFilePath(reportProblem.Message));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteSources(JsonWriter writer, IEnumerable<PackageSource> packageSources)
        {
            if (ListPackageArgs.ReportType == ReportType.Default)
            {
                // generic list is offline.
                return;
            }

            writer.WritePropertyName(SourcesProperty);
            writer.WriteStartArray();

            foreach (PackageSource packageSource in packageSources)
            {
                writer.WriteValue(packageSource.Source);
            }

            writer.WriteEndArray();
        }

        private static void WriteProjects(JsonWriter writer, List<ListPackageProjectModel> reportProjects)
        {
            writer.WritePropertyName(ProjectsProperty);
            writer.WriteStartArray();

            foreach (ListPackageProjectModel reportProject in reportProjects)
            {
                writer.WriteStartObject();

                writer.WritePropertyName(PathProperty);
                writer.WriteValue(NormalizeFilePath(reportProject.ProjectPath));

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
                writer.WriteValue(reportFrameworkPackage.Framework);
                WriteTopLevelPackages(writer, TopLevelPackagesProperty, reportFrameworkPackage.TopLevelPackages);
                WriteTransitivePackages(writer, TransitivePackagesProperty, reportFrameworkPackage.TransitivePackages);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteTopLevelPackages(JsonWriter writer, string property, List<ListReportTopPackage> topLevelPackages)
        {
            if (topLevelPackages?.Count > 0)
            {
                writer.WritePropertyName(property);

                writer.WriteStartArray();
                foreach (ListReportTopPackage topLevelPackage in topLevelPackages)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(IdProperty);
                    writer.WriteValue(topLevelPackage.PackageId);

                    writer.WritePropertyName(RequestedVersionProperty);
                    writer.WriteValue(topLevelPackage.OriginalRequestedVersion);

                    writer.WritePropertyName(ResolvedVersionProperty);
                    writer.WriteValue(topLevelPackage.ResolvedVersion);

                    if (topLevelPackage.AutoReference)
                    {
                        writer.WritePropertyName(AutoReferencedProperty);
                        writer.WriteValue("true");
                    }

                    switch (ListPackageArgs.ReportType)
                    {
                        case ReportType.Outdated:
                            writer.WritePropertyName(LatestVersionProperty);
                            writer.WriteValue(topLevelPackage.LatestVersion);
                            break;
                        case ReportType.Deprecated:
                            WriteDeprecations(writer, topLevelPackage);
                            break;
                        case ReportType.Vulnerable:
                            WriteVulnerabilities(writer, topLevelPackage.Vulnerabilities);
                            break;
                        default:
                            break;
                    }

                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
        }

        private static void WriteTransitivePackages(JsonWriter writer, string property, List<ListReportTransitivePackage> transitivePackages)
        {
            if (transitivePackages?.Count > 0)
            {
                writer.WritePropertyName(property);

                writer.WriteStartArray();
                foreach (ListReportTransitivePackage transitivePackage in transitivePackages)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(IdProperty);
                    // Here for ReportType.Vulnerable: Substring(s) removes "> " from front.
                    string packageId = ListPackageArgs.ReportType == ReportType.Vulnerable ?
                        transitivePackage.PackageId.Substring(2) : transitivePackage.PackageId;
                    writer.WriteValue(packageId);

                    writer.WritePropertyName(ResolvedVersionProperty);
                    writer.WriteValue(transitivePackage.ResolvedVersion);

                    switch (ListPackageArgs.ReportType)
                    {
                        case ReportType.Outdated:
                            writer.WritePropertyName(LatestVersionProperty);
                            writer.WriteValue(transitivePackage.LatestVersion);
                            break;
                        case ReportType.Deprecated:
                            WriteDeprecations(writer, transitivePackage);
                            break;
                        case ReportType.Vulnerable:
                            WriteVulnerabilities(writer, transitivePackage.Vulnerabilities);
                            break;
                        default:
                            break;
                    }


                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
        }

        private static void WriteDeprecations(JsonWriter writer, ListReportPackage listPackage)
        {
            if (listPackage.DeprecationReasons != null)
            {
                writer.WritePropertyName(DeprecationReasonsProperty);
                writer.WriteStartArray();

                foreach (string deprecationReason in listPackage.DeprecationReasons?.Reasons)
                {
                    writer.WriteValue(deprecationReason);
                }

                writer.WriteEndArray();
            }

            if (listPackage.AlternativePackage != null)
            {
                writer.WritePropertyName(AlternativePackageProperty);
                writer.WriteStartObject();
                writer.WritePropertyName(IdProperty);
                writer.WriteValue(listPackage.AlternativePackage.PackageId);
                writer.WritePropertyName(VersionRangeProperty);

                var versionRangeString = VersionRangeFormatter.Instance.Format(
                    "p",
                    listPackage.AlternativePackage.Range,
                    VersionRangeFormatter.Instance);

                writer.WriteValue(versionRangeString);
                writer.WriteEndObject();
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

        private static string NormalizeFilePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

#if NETCOREAPP
            return path.Replace("\\", "/", StringComparison.Ordinal);
#else
            return path.Replace("\\", "/");
#endif
        }

        private class JsonOutputConverter : JsonConverter
        {
            internal static JsonOutputConverter Default { get; } = new JsonOutputConverter();

            private static readonly Type TargetType = typeof(ListPackageOutputContentV1);
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
                if (!(value is ListPackageOutputContentV1 jsonOutputContent))
                {
                    throw new ArgumentException(message: "value is not of type JsonOutputContent", paramName: nameof(value));
                }

                writer.WriteStartObject();

                writer.WritePropertyName(VersionProperty);
                writer.WriteValue(jsonOutputContent.Version);

                writer.WritePropertyName(ParametersProperty);
                writer.WriteValue(NormalizeFilePath(ListPackageArgs.ArgumentText));

                if (jsonOutputContent.AutoReferenceFound)
                {
                    jsonOutputContent.Problems.Add(new ReportProblem(string.Empty, Strings.ListPkg_AutoReferenceDescription, ProblemType.Information));
                }

                if (jsonOutputContent.Problems?.Count > 0)
                {
                    WriteProblems(writer, jsonOutputContent.Problems);
                }

                WriteSources(writer, ListPackageArgs.PackageSources);
                WriteProjects(writer, jsonOutputContent.Projects);
                writer.WriteEndObject();
            }
        }
    }
}
