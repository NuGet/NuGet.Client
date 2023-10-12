// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchCommand
    {
        public static IList<PackageSource> GetEndpointsAsync(List<string> sources)
        {
            ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory(),
                    configFileName: null,
                    machineWideSettings: new XPlatMachineWideSetting());
            PackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
            List<PackageSource> configurationSources = sourceProvider.LoadPackageSources()
                .Where(p => p.IsEnabled)
                .ToList();

            IList<PackageSource> packageSources;
            if (sources.Count > 0)
            {
                packageSources = sources
                    .Select(s => ResolveSource(configurationSources, s))
                    .ToList();
            }
            else
            {
                packageSources = configurationSources;
            }
            return packageSources;
        }

        private static PackageSource ResolveSource(IEnumerable<PackageSource> availableSources, string source)
        {
            var resolvedSource = availableSources.FirstOrDefault(
                    f => f.Source.Equals(source, StringComparison.OrdinalIgnoreCase) ||
                        f.Name.Equals(source, StringComparison.OrdinalIgnoreCase));

            if (resolvedSource == null)
            {
                ValidateSource(source);
                return new PackageSource(source);
            }
            else
            {
                return resolvedSource;
            }
        }

        private static void ValidateSource(string source)
        {
            if (!Uri.TryCreate(source, UriKind.Absolute, out Uri result))
            {
                throw new Exception("Invalid source " + source);
            }
        }
        private static void WarnForHTTPSources(IList<PackageSource> packageSources)
        {
            List<PackageSource> httpPackageSources = null;
            foreach (PackageSource packageSource in packageSources)
            {
                if (packageSource.IsHttp && !packageSource.IsHttps && !packageSource.AllowInsecureConnections)
                {
                    if (httpPackageSources == null)
                    {
                        httpPackageSources = new();
                    }
                    httpPackageSources.Add(packageSource);
                }
            }

            if (httpPackageSources != null && httpPackageSources.Count != 0)
            {
                if (httpPackageSources.Count == 1)
                {
                    Console.WriteLine(
                        string.Format(CultureInfo.CurrentCulture,
                        Strings.Warning_HttpServerUsage,
                        "search",
                        httpPackageSources[0]));
                }
                else
                {
                    Console.WriteLine(
                        string.Format(CultureInfo.CurrentCulture,
                        Strings.Warning_HttpServerUsage,
                        "search",
                        Environment.NewLine + string.Join(Environment.NewLine, httpPackageSources.Select(e => e.Name))));
                }
            }
        }
        private static void PrintResult(IEnumerable<IPackageSearchMetadata> results, bool exactMatch, string searchTerm)
        {
            if (exactMatch && results?.Any() == true &&
                results.First().Identity.Id.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                // we are doing exact match and if the result from the API are sorted, the first result should be the package we are searching
                IPackageSearchMetadata result = results.First();
                Console.WriteLine($"{result.Identity.Id} | {result.Identity.Version} | Downloads: {(result.DownloadCount.HasValue ? result.DownloadCount.ToString() : "N/A")}");
                return;
            }
            else
            {
                foreach (IPackageSearchMetadata result in results)
                {
                    Console.WriteLine($"{result.Identity.Id} | {result.Identity.Version} | Downloads: {(result.DownloadCount.HasValue ? result.DownloadCount.ToString() : "N/A")}");
                }
            }
        }
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("search", pkgSearch =>
            {
                int lineSeparatorLength = 20;
                int take_value = 20;
                int skip_value = 0;
                pkgSearch.Description = Strings.pkgSearch_Description;
                pkgSearch.HelpOption(XPlatUtility.HelpOption);

                CommandArgument searchTern = pkgSearch.Argument(
                    "<Search Term>",
                    Strings.pkgSearch_termDescription);
                CommandOption sources = pkgSearch.Option(
                    "--source",
                    Strings.pkgSearch_SourceDescription,
                    CommandOptionType.MultipleValue);
                CommandOption exactMatch = pkgSearch.Option(
                    "--exact-match",
                    Strings.pkgSearch_ExactMatchDescription,
                    CommandOptionType.NoValue);
                CommandOption prerelease = pkgSearch.Option(
                    "--prerelease",
                    Strings.pkgSearch_PrereleaseDescription,
                    CommandOptionType.NoValue);
                CommandOption interactive = pkgSearch.Option(
                    "--interactive",
                    Strings.pkgSearch_InteractiveDescription,
                    CommandOptionType.NoValue);
                CommandOption take = pkgSearch.Option(
                    "--take",
                    Strings.pkgSearch_TakeDescription,
                    CommandOptionType.SingleValue);
                CommandOption skip = pkgSearch.Option(
                    "--skip",
                    Strings.pkgSearch_SkipDescription,
                    CommandOptionType.SingleValue);

                pkgSearch.OnExecute(async () =>
                {
                    string sourceSeparator = new string('=', lineSeparatorLength);
                    string packageSeparator = new string('-', lineSeparatorLength);

                    ILogger logger = NullLogger.Instance;
                    CancellationToken cancellationToken = CancellationToken.None;

                    var taskList = new List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)>();
                    IList<PackageSource> listEndpoints = GetEndpointsAsync(sources.Values);

                    WarnForHTTPSources(listEndpoints);
                    foreach (PackageSource source in listEndpoints)
                    {
                        SourceRepository repository = Repository.Factory.GetCoreV3(source);
                        PackageSearchResource resource = await repository.GetResourceAsync<PackageSearchResource>(cancellationToken);

                        if (resource is null)
                        {
                            taskList.Add((null, source));
                            continue;
                        }

                        taskList.Add((Task.Run(() => resource.SearchAsync(
                            searchTern.Value,
                            new SearchFilter(includePrerelease: prerelease.HasValue()),
                            skip: skip_value,
                            take: take_value,
                            logger,
                            cancellationToken)), source));
                    }

                    foreach (var taskItem in taskList)
                    {
                        var (task, source) = taskItem;

                        if (task is null)
                        {
                            Console.WriteLine(sourceSeparator);
                            Console.WriteLine($"Source: {source.Name}");
                            Console.WriteLine(packageSeparator);
                            Console.WriteLine("Failed to obtain a search resource.");
                            Console.WriteLine(packageSeparator);
                            Console.WriteLine();
                            continue;
                        }

                        var results = await task;

                        Console.WriteLine(sourceSeparator);
                        Console.WriteLine($"Source: {source.Name}"); // System.Console is used so that output is not suppressed by Verbosity.Quiet

                        if (results.Any())
                        {
                            PrintResult(results, exactMatch.HasValue(), searchTern.Value);
                            Console.WriteLine(packageSeparator);
                        }
                        else
                        {
                            Console.WriteLine(packageSeparator);
                            Console.WriteLine("No results found.");
                            Console.WriteLine(packageSeparator);
                            Console.WriteLine();
                        }
                    }
                    return 0;
                });

            });
        }
    }
}
