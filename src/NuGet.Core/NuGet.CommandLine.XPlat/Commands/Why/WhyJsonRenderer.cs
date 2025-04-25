// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.CommandLine.XPlat.Commands.Why
{
    /// <summary>
    /// Json output renderer for 'why' command
    /// </summary>
    internal class WhyJsonRenderer : IReportRenderer
    {
        private const int ReportOutputVersion = 1;

        private readonly ILoggerWithColor _logger;

        public WhyJsonRenderer(ILoggerWithColor logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Render(WhyReportModel reportModel)
        {
            List<DependencyGraphContract> dependencyGraphs = ProcessDependencyGraphContracts(reportModel.DependencyGraphPerFramework);

            WhyJsonReportContract report = new WhyJsonReportContract
            {
                Version = ReportOutputVersion,
                Parameters = reportModel.WhyCommandArgs.ArgumentText,
                Project = reportModel.ProjectName,
                Package = reportModel.TargetPackage,
                DependencyGraphs = dependencyGraphs,
            };

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true,
            });

            _logger.LogMinimal(json);
        }

        private static List<DependencyGraphContract> ProcessDependencyGraphContracts(Dictionary<string, List<DependencyNode>?> dependencyGraphPerFramework)
        {
            List<DependencyGraphContract> dependencyGraphs = [];
            foreach (var framework in dependencyGraphPerFramework.Keys)
            {
                var dependencies = dependencyGraphPerFramework[framework];
                if (dependencies != null)
                {
                    var dependencyGraph = new DependencyGraphContract
                    {
                        Framework = framework,
                        Dependencies = [.. dependencies]
                    };
                    dependencyGraphs.Add(dependencyGraph);
                }
            }

            return dependencyGraphs;
        }

        public class WhyJsonReportContract
        {
            [JsonPropertyName("version")]
            public required int Version { get; set; }

            [JsonPropertyName("parameters")]
            public required string Parameters { get; set; }

            [JsonPropertyName("project")]
            public required string Project { get; set; }

            [JsonPropertyName("package")]
            public required string Package { get; set; }

            [JsonPropertyName("dependencyGraphs")]
            public required List<DependencyGraphContract> DependencyGraphs { get; set; }
        }

        public class DependencyGraphContract
        {
            [JsonPropertyName("framework")]
            public required string Framework { get; set; }

            [JsonPropertyName("dependencies")]
            public required HashSet<DependencyNode> Dependencies { get; set; }
        }
    }
}
