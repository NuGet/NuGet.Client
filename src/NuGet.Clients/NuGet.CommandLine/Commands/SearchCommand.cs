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
        "search",
        "SearchCommandDescription",
        UsageSummaryResourceName = "SearchCommandUsageSummary",
        UsageDescriptionResourceName = "SearchCommandUsageDescription",
        UsageExampleResourceName = "SearchCommandUsageExamples")]
    public class SearchCommand : Command
    {
        private readonly List<string> _sources = new List<string>();

        [Option(typeof(NuGetCommand), "ListCommandSourceDescription")]
        public ICollection<string> Source
        {
            get { return _sources; }
        }

        [Option(typeof(NuGetCommand), "ListCommandPrerelease")]
        public bool Prerelease { get; set; }


        private IList<Configuration.PackageSource> GetEndpoints()
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

        public async override Task ExecuteCommandAsync()
        {
            var searchCommandRunner = new SearchCommandRunner();
            var listEndpoints = GetEndpoints();

            var searchArgs = new SearchArgs(Prerelease, listEndpoints); 

            await searchCommandRunner.ExecuteCommand(searchArgs);
        }
    }
}