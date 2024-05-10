// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    [Command(
         typeof(NuGetCommand),
         "list",
         "ListCommandDescription",
         UsageSummaryResourceName = "ListCommandUsageSummary",
         UsageDescriptionResourceName = "ListCommandUsageDescription",
         UsageExampleResourceName = "ListCommandUsageExamples")]
    [DeprecatedCommand(typeof(SearchCommand))]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class ListCommand : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        private readonly List<string> _sources = new List<string>();

        [Option(typeof(NuGetCommand), "ListCommandSourceDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public ICollection<string> Source
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get { return _sources; }
        }

        [Option(typeof(NuGetCommand), "ListCommandVerboseListDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Verbose { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "ListCommandAllVersionsDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool AllVersions { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "ListCommandPrerelease")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool Prerelease { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "ListCommandIncludeDelisted")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool IncludeDelisted { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        private IList<Configuration.PackageSource> GetEndpointsAsync()
        {
            var configurationSources = SourceProvider.LoadPackageSources()
                .Where(p => p.IsEnabled)
                .ToList();

            IList<Configuration.PackageSource> packageSources;
            if (Source.Count > 0)
            {
                packageSources = Source
                    .Select(s => Common.PackageSourceProviderExtensions.ResolveSource(configurationSources, s))
                    .ToList();
            }
            else
            {
                packageSources = configurationSources;
            }
            return packageSources;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public async override Task ExecuteCommandAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (Verbose)
            {
                Console.WriteWarning(LocalizedResourceManager.GetString("Option_VerboseDeprecated"));
                Verbosity = Verbosity.Detailed;
            }

            var listCommandRunner = new ListCommandRunner();
            var listEndpoints = GetEndpointsAsync();

            var list = new ListArgs(Arguments,
                listEndpoints,
                Settings,
                Console,
                Console.PrintJustified,
                Verbosity == Verbosity.Detailed,
                LocalizedResourceManager.GetString("ListCommandNoPackages"),
                LocalizedResourceManager.GetString("ListCommand_LicenseUrl"),
                LocalizedResourceManager.GetString("ListCommand_ListNotSupported"),
                AllVersions,
                IncludeDelisted,
                Prerelease,
                CancellationToken.None);

            await listCommandRunner.ExecuteCommand(list);
        }
    }
}
