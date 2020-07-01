// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

    [Command(typeof(NuGetCommand), "search", "SearchCommandDescription", MaxArgs = 1,
            UsageSummaryResourceName = "SearchCommandUsageSummary", UsageExampleResourceName = "SearchCommandUsageExamples")]
    public class SearchCommand : Command
    {
        [Option(typeof(NuGetCommand), "SearchCommandAssemblyPathDescription")]
        public string AssemblyPath
        {
            get;
            set;
        }

        [Option(typeof(NuGetCommand), "SearchCommandForceDescription")]
        public bool Force
        {
            get;
            set;
        }


        public override async Task ExecuteCommandAsync()
        {
            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;

            foreach (var source in SourceProvider.LoadPackageSources())
            {
                var target = source.Source;
                var name = source.Name;

                SourceRepository repository = Repository.Factory.GetCoreV3(target);
                PackageSearchResource resource = await repository.GetResourceAsync<PackageSearchResource>();

                if (resource is null)
                {
                    continue;
                }

                SearchFilter searchFilter = new SearchFilter(includePrerelease: false);

                IEnumerable<IPackageSearchMetadata> results = await resource.SearchAsync(
                    Arguments[0],
                    searchFilter,
                    skip: 0,
                    take: 20,
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
                Console.WriteLine($"> {result.Identity.Id} | {result.Identity.Version.ToNormalizedString()} | Downloads: {result.DownloadCount}");

                string description = result.Description;

                if (description.Length > 100)
                {
                    description = description.Substring(0, 100) + "...";
                }

                Console.PrintJustified(2, description);
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
