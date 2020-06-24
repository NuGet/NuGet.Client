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

        //private static string BoldStart = "\x1B[1m";
        //private static string BoldEnd = "\x1B[22m";

        //private string Bold(string text)
        //{
        //    return BoldStart + text + BoldEnd;
        //}

        public override async Task ExecuteCommandAsync()
        {

            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;

            foreach (var source in SourceProvider.LoadPackageSources())
            {
                using (Process myProcess = new Process())
                {
                    myProcess.StartInfo.FileName = "cmd"; // TODO: Move outside the loop
                    myProcess.StartInfo.Arguments = "/c more";
                    myProcess.StartInfo.UseShellExecute = false;
                    myProcess.StartInfo.RedirectStandardInput = true;

                    myProcess.Start();

                    StreamWriter myStreamWriter = myProcess.StandardInput;

                    var target = source.Source;
                    var name = source.Name;

                    SourceRepository repository = Repository.Factory.GetCoreV3(target);
                    PackageSearchResource resource = await repository.GetResourceAsync<PackageSearchResource>();

                    if (resource is null)
                    {
                        continue;
                    }

                    SearchFilter searchFilter = new SearchFilter(includePrerelease: true);

                    IEnumerable<IPackageSearchMetadata> results = await resource.SearchAsync(
                        Arguments[0],
                        searchFilter,
                        skip: 0,
                        take: 20,
                        logger,
                        cancellationToken);


                    myStreamWriter.WriteLine(new string('=', 20));
                    myStreamWriter.WriteLine($"Source: {source.Name}");

                    if (!results.Any())
                    {
                        myStreamWriter.WriteLine(new string('-', 20));
                        myStreamWriter.WriteLine("No results found.");
                    }


                    foreach (IPackageSearchMetadata result in results)
                    {
                        myStreamWriter.WriteLine(new string('-', 20));
                        myStreamWriter.WriteLine($"> {result.Identity.Id} | v{result.Identity.Version} | DLs: {result.DownloadCount}"); // TODO: '>' and 'v' necessary or not?
                        myStreamWriter.WriteLine($"{result.Description}\n");

                    }

                    myStreamWriter.Close();

                    // Wait for the sort process to write the sorted text lines.
                    myProcess.WaitForExit();
                }

            }

            Thread.Sleep(5000);
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
