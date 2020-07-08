// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Licenses;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Text;

namespace NuGet.CommandLine
{

    [Command(typeof(NuGetCommand), "search", "SearchCommandDescription",
            UsageSummaryResourceName = "SearchCommandUsageSummary", UsageExampleResourceName = "SearchCommandUsageExamples")]
    public class SearchCommand : Command
    {
        private readonly List<string> _sources = new List<string>();

        [Option(typeof(NuGetCommand), "SearchCommandSourceDescription")]
        public ICollection<string> Source
        {
            get { return _sources; }
        }

        [Option(typeof(NuGetCommand), "SearchCommandPreRelease")]
        public bool PreRelease { get; set; } = false;

        [Option(typeof(NuGetCommand), "SearchCommandTake")]
        public int Take { get; set; } = 20;

        private IList<PackageSource> GetEndpointsAsync()
        {
            List<PackageSource> configurationSources = SourceProvider.LoadPackageSources()
                .Where(p => p.IsEnabled)
                .ToList();

            IList<PackageSource> packageSources;
            if (Source.Count > 0)
            {
                packageSources = Source
                    .Select(s => PackageSourceProviderExtensions.ResolveSource(configurationSources, s))
                    .ToList();
            }
            else
            {
                packageSources = configurationSources;
            }
            return packageSources;
        }

        public override async Task ExecuteCommandAsync()
        {
            int lineSeparatorLength = 20;
            string sourceSeparator = new string('=', lineSeparatorLength);
            string packageSeparator = new string('-', lineSeparatorLength);

            ILogger logger = Console;
            CancellationToken cancellationToken = CancellationToken.None;

            SearchFilter searchFilter = new SearchFilter(includePrerelease: PreRelease);

            IList<PackageSource> listEndpoints = GetEndpointsAsync();

            foreach (PackageSource source in listEndpoints)
            {
                SourceRepository repository = Repository.Factory.GetCoreV3(source);
                PackageSearchResource resource = await repository.GetResourceAsync<PackageSearchResource>();

                if (resource is null)
                {
                    continue;
                }

                IEnumerable<IPackageSearchMetadata> results = await resource.SearchAsync(
                    string.Join(" ", Arguments).Trim(), // The arguments are joined with spaces to form a single query string
                    searchFilter,
                    skip: 0,
                    take: Take,
                    logger,
                    cancellationToken);

                Console.WriteLine(sourceSeparator);
                Console.WriteLine($"Source: {source.Name}");

                if (!results.Any())
                {
                    Console.WriteLine(packageSeparator);
                    Console.WriteLine("No results found.");
                    Console.WriteLine(packageSeparator + "\n");
                }
                else
                {
                    if (Verbosity == Verbosity.Quiet)
                    {
                        System.Console.WriteLine($"Source: {source.Name}");
                        System.Console.WriteLine(packageSeparator);
                    }
                    PrintResults(results);
                }
            }
        }

        private void PrintResults(IEnumerable<IPackageSearchMetadata> results)
        {
            int lineSeparatorLength = 20;
            string packageSeparator = new string('-', lineSeparatorLength);

            foreach (IPackageSearchMetadata result in results)
            {
                Console.WriteLine(packageSeparator);

                CultureInfo culture = CultureInfo.CurrentCulture;

                StringBuilder content = new StringBuilder();
                content.Append($"> {result.Identity.Id} | {result.Identity.Version.ToNormalizedString()}"); // Basic info (Name | Version)

                if (Verbosity != Verbosity.Quiet)
                {
                    if (result.DownloadCount != null)
                    {
                        string downloads = string.Format(culture, "{0:N}", result.DownloadCount);
                        content.Append($" | Downloads: {downloads.Substring(0, downloads.Length - 3)}");
                    }
                    else
                    {
                        content.Append(" | Downloads: N/A");
                    }
                }

                System.Console.WriteLine(content.ToString()); // System.Console is used so that output is not suppressed by Verbosity.Quiet

                if (Verbosity != Verbosity.Quiet && result.Description != null)
                {
                    string description = result.Description;

                    if (Verbosity == Verbosity.Normal)
                    {
                        if (description.Length > 100)
                        {
                            description = description.Substring(0, 100) + "...";
                        }
                    }
                    Console.PrintJustified(2, description);
                }
            }

            Console.WriteLine(packageSeparator);
            System.Console.WriteLine();
        }

        public override bool IncludedInHelp(string optionName)
        {
            if (string.Equals(optionName, "ConfigFile", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return base.IncludedInHelp(optionName);
        }
    }
}
