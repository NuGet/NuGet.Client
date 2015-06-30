using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CommandLine;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Commands
{
    [Command(typeof(NuGetCommand), "list", "ListCommandDescription",
        UsageSummaryResourceName = "ListCommandUsageSummary", UsageDescriptionResourceName = "ListCommandUsageDescription",
        UsageExampleResourceName = "ListCommandUsageExamples")]
    public class ListCommand : Command
    {
        // PageSize when a filter is specified.
        private const int FilteredPageSize = 300;
        // PageSize when no search filter is specified.
        private const int UnfilteredPageSize = 30;

        private readonly List<string> _sources = new List<string>();

        [Option(typeof(NuGetCommand), "ListCommandSourceDescription")]
        public ICollection<string> Source
        {
            get { return _sources; }
        }

        [Option(typeof(NuGetCommand), "ListCommandVerboseListDescription")]
        public bool Verbose { get; set; }

        [Option(typeof(NuGetCommand), "ListCommandAllVersionsDescription")]
        public bool AllVersions { get; set; }

        [Option(typeof(NuGetCommand), "ListCommandPrerelease")]
        public bool Prerelease { get; set; }

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This call is expensive")]
        public async Task<IEnumerable<SimpleSearchMetadata>> GetPackages()
        {
            var packageSourceProvider = new Configuration.PackageSourceProvider(Settings);
            var configurationSources = packageSourceProvider.LoadPackageSources();
            IEnumerable<Configuration.PackageSource> packageSources;
            if (Source.Count > 0)
            {
                packageSources = Source.Select(s => Common.PackageSourceProviderExtensions.ResolveSource(configurationSources, s));
            }
            else
            {
                packageSources = configurationSources;
            }

            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider,
                Enumerable.Concat(
                    Protocol.Core.v2.FactoryExtensionsV2.GetCoreV2(Repository.Provider),
                    Protocol.Core.v3.FactoryExtensionsV2.GetCoreV3(Repository.Provider)));

            var resourceTasks = new List<Task<SimpleSearchResource>>();
            foreach (var source in packageSources)
            {
                var sourceRepository = sourceRepositoryProvider.CreateRepository(source);
                resourceTasks.Add(sourceRepository.GetResourceAsync<SimpleSearchResource>());
            }
            var resources = await Task.WhenAll(resourceTasks);

            var searchTerm = Arguments.FirstOrDefault();
            var resultTasks = resources.Where(r => r != null)
                .Select(r => r.Search(
                    searchTerm,
                    new SearchFilter(Enumerable.Empty<string>(), Prerelease, includeDelisted: false),
                    skip: 0,
                    take: string.IsNullOrEmpty(searchTerm) ? UnfilteredPageSize : FilteredPageSize,
                    cancellationToken: CancellationToken.None));

            var results = (await Task.WhenAll(resultTasks)).SelectMany(feedResult => feedResult);

            //if (AllVersions)
            //{
            //    results = results.SelectMany(package => package.AllVersions.Select(version =>
            //                new SimpleSearchMetadata(
            //                    new Packaging.Core.PackageIdentity(package.Identity.Id, version), 
            //                    package.Description, 
            //                    Enumerable.Empty<NuGetVersion>())));
            //}

            return results.OrderBy(s => s.Identity.Id, StringComparer.Ordinal);
        }

        public override async Task ExecuteCommandAsync()
        {
            if (Verbose)
            {
                Console.WriteWarning(LocalizedResourceManager.GetString("Option_VerboseDeprecated"));
                Verbosity = Verbosity.Detailed;
            }

            var packages = await GetPackages();

            bool hasPackages = false;

            if (packages != null)
            {
                if (Verbosity == Verbosity.Detailed)
                {
                    /***********************************************
                     * Package-Name
                     *  1.0.0.2010
                     *  This is the package Description
                     * 
                     * Package-Name-Two
                     *  2.0.0.2010
                     *  This is the second package Description
                     ***********************************************/
                    foreach (var p in packages)
                    {
                        Console.PrintJustified(0, p.Identity.Id);
                        if (!AllVersions)
                        {
                            Console.PrintJustified(1, p.Identity.Version.ToNormalizedString());
                            Console.PrintJustified(1, p.Description);
                        }
                        else
                        {
                            Console.PrintJustified(0, p.Description);
                            foreach (var version in p.AllVersions)
                            {
                                Console.PrintJustified(1, version.ToNormalizedString());
                            }
                        }


                        Console.WriteLine();
                        hasPackages = true;
                    }
                }
                else
                {
                    /***********************************************
                     * Package-Name 1.0.0.2010
                     * Package-Name-Two 2.0.0.2010
                     ***********************************************/
                    foreach (var p in packages)
                    {
                        Console.PrintJustified(0, p.Identity.Id + " " + p.Identity.Version);
                        if (AllVersions)
                        {
                            foreach (var version in p.AllVersions)
                            {
                                Console.PrintJustified(0, p.Identity.Id + " " + version);
                            }
                        }

                        hasPackages = true;
                    }
                }
            }

            if (!hasPackages)
            {
                Console.WriteLine(LocalizedResourceManager.GetString("ListCommandNoPackages"));
            }
        }

        private SourceRepositoryProvider GetSourceRepositoryProvider()
        {
            var packageSourceProvider = new Configuration.PackageSourceProvider(Settings);
            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider,
                Enumerable.Concat(
                    Protocol.Core.v2.FactoryExtensionsV2.GetCoreV2(Repository.Provider),
                    Protocol.Core.v3.FactoryExtensionsV2.GetCoreV3(Repository.Provider)));
            return sourceRepositoryProvider;
        }
    }
}