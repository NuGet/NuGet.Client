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
        [Option(typeof(NuGetCommand), "SearchCommandAssemblyPathDescription")]
        public string AssemblyPath
        { get; set; }

        [Option(typeof(NuGetCommand), "SearchCommandForceDescription")]
        public bool Force
        { get; set; }

        private readonly List<string> _sources = new List<string>();

        [Option(typeof(NuGetCommand), "ListCommandSourceDescription")]
        public ICollection<string> Source
        {
            get { return _sources; }
        }

        // -Source "https://api.nuget.org/v3/index.json"


        [Option(typeof(NuGetCommand), "ListCommandPreRelease")]
        public bool PreRelease { get; set; } = false;


        [Option(typeof(NuGetCommand), "ListCommandTake")]
        public int Take { get; set; } = 20;


        [Option(typeof(NuGetCommand), "ListCommandPackageTypes")]
        public IEnumerable<string> PackageTypes { get; set; } = Enumerable.Empty<string>();


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
            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;

            SearchFilter searchFilter = new SearchFilter(includePrerelease: PreRelease);
            searchFilter.PackageTypes = PackageTypes;

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
                    Arguments[0],
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

                string downloads = string.Format(culture, "{0:N}", result.DownloadCount);
                string printDownloads = $" | Downloads: {downloads.Substring(0, downloads.Length - 3)}";

                Console.WriteLine(Verbosity != Verbosity.Quiet ? printBasicInfo + printDownloads : printBasicInfo);

                if (Verbosity != Verbosity.Quiet)
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
            Console.WriteLine("");
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
