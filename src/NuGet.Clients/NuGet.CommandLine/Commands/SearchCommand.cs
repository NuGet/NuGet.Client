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

        // -Source "https://api.nuget.org/v3/index.json"


        [Option(typeof(NuGetCommand), "SearchCommandPreRelease")]
        public bool PreRelease { get; set; } = false;


        [Option(typeof(NuGetCommand), "SearchCommandTake")]
        public int Take { get; set; } = 20;


        private IList<PackageSource> GetEndpointsAsync()
        {
            var configurationSources = SourceProvider.LoadPackageSources()
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
            ILogger logger = Console;
            CancellationToken cancellationToken = CancellationToken.None;

            SearchFilter searchFilter = new SearchFilter(includePrerelease: PreRelease);

            var listEndpoints = GetEndpointsAsync();

            foreach (var source in listEndpoints)
            {
                var target = source.Source;
                var name = source.Name;

                SourceRepository repository = Repository.Factory.GetCoreV3(target);
                PackageSearchResource resource = await repository.GetResourceAsync<PackageSearchResource>();

                if (resource is null)
                {
                    continue;
                }

                IEnumerable<IPackageSearchMetadata> results = await resource.SearchAsync(
                    string.Join("+", Arguments).Trim().Replace(' ', '+'),
                    searchFilter,
                    skip: 0,
                    take: Take,
                    logger,
                    cancellationToken);

                Console.WriteLine(new string('=', 20));
                Console.WriteLine($"Source: {source.Name}");

                if (!results.Any())
                {
                    Console.WriteLine(new string('-', 20));
                    Console.WriteLine("No results found.");
                    Console.WriteLine(new string('-', 20) + "\n");
                }
                else
                {
                    if (Verbosity == Verbosity.Quiet)
                    {
                        System.Console.WriteLine($"Source: {source.Name}");
                        System.Console.WriteLine(new string('-', 20));
                    }
                    PrintResults(results);
                }
            }
        }


        private void PrintResults(IEnumerable<IPackageSearchMetadata> results)
        {
            foreach (IPackageSearchMetadata result in results)
            {
                Console.WriteLine(new string('-', 20));

                CultureInfo culture = CultureInfo.CurrentCulture;

                string printBasicInfo = $"> {result.Identity.Id} | {result.Identity.Version.ToNormalizedString()}";

                string downloads, printDownloads;

                if (result.DownloadCount != null)
                {
                    downloads = string.Format(culture, "{0:N}", result.DownloadCount);
                    printDownloads = $" | Downloads: {downloads.Substring(0, downloads.Length - 3)}";
                }
                else
                {
                    printDownloads = " | Downloads: N/A";
                }

                System.Console.WriteLine(Verbosity != Verbosity.Quiet ? printBasicInfo + printDownloads : printBasicInfo); // System.Console is used so that output is not suppressed by Verbosity.Quiet

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

            Console.WriteLine(new string('-', 20));
            System.Console.WriteLine("");
        }

        public override bool IncludedInHelp(string optionName)
        {
            if (string.Equals(optionName, "ConfigFile", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return base.IncludedInHelp(optionName);
        }
        
        private static string RemoveSchemaNamespace(string content)
        {
            // This seems to be the only way to clear out xml namespaces.
            return Regex.Replace(content, @"(xmlns:?[^=]*=[""][^""]*[""])", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        }

        private static IList<Resource> Deserialize(string value)
        {
            return JsonConvert.DeserializeObject<IndexJson>(value).Resources;
        }

        private sealed class IndexJson
        {
            [JsonProperty("resources")]
            public IList<Resource> Resources { get; set; }
        }

        private sealed class Resource
        {
            [JsonProperty("@type")]
            public string Type { get; set; }

            [JsonProperty("@id")]
            public string Id { get; set; }
        }


    }

    
}
