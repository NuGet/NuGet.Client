using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.CommandLine;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    [Command(
        typeof(NuGetCommand),
        "list",
        "ListCommandDescription",
        UsageSummaryResourceName = "ListCommandUsageSummary",
        UsageDescriptionResourceName = "ListCommandUsageDescription",
        UsageExampleResourceName = "ListCommandUsageExamples")]
    public class ListCommand : Command
    {
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

        [Option(typeof(NuGetCommand), "ListCommandIncludeDelisted")]
        public bool IncludeDelisted { get; set; }

        [SuppressMessage(
            "Microsoft.Design",
            "CA1024:UsePropertiesWhereAppropriate",
            Justification = "This call is expensive")]
        public IEnumerable<IPackage> GetPackages(IEnumerable<string> listEndpoints)
        {
            IPackageRepository packageRepository = GetRepository(listEndpoints);
            string searchTerm = Arguments != null ? Arguments.FirstOrDefault() : null;

            IQueryable<IPackage> packages = packageRepository.Search(
                searchTerm,
                targetFrameworks: Enumerable.Empty<string>(),
                allowPrereleaseVersions: Prerelease);

            if (AllVersions)
            {
                return packages.OrderBy(p => p.Id);
            }
            else
            {
                if (Prerelease && packageRepository.SupportsPrereleasePackages)
                {
                    packages = packages.Where(p => p.IsAbsoluteLatestVersion);
                }
                else
                {
                    packages = packages.Where(p => p.IsLatestVersion);
                }
            }

            var result = packages.OrderBy(p => p.Id)
                .AsEnumerable();

            // we still need to do client side filtering of delisted & prerelease packages.
            if (IncludeDelisted == false)
            {
                result = result.Where(PackageExtensions.IsListed);
            }
            return result.Where(p => Prerelease || p.IsReleaseVersion())
                       .AsCollapsed();
        }

        private IPackageRepository GetRepository(IEnumerable<string> listEndpoints)
        {
            var repositories = listEndpoints
                                .Select(RepositoryFactory.CreateRepository)
                                .ToList();

            var repository = new AggregateRepository(repositories);
            return repository;
        }

        private async Task<IList<string>> GetListEndpointsAsync()
        {
            var configurationSources = SourceProvider.LoadPackageSources()
                .Where(p => p.IsEnabled)
                .ToList();

            IList<Configuration.PackageSource> packageSources;
            if (Source.Count > 0)
            {
                packageSources
                    = Source
                        .Select(s => Common.PackageSourceProviderExtensions.ResolveSource(configurationSources, s))
                        .ToList();
            }
            else
            {
                packageSources = configurationSources;
            }

            var sourceRepositoryProvider = new CommandLineSourceRepositoryProvider(SourceProvider);

            var listCommandResourceTasks = new List<Task<ListCommandResource>>();

            foreach (var source in packageSources)
            {
                var sourceRepository = sourceRepositoryProvider.CreateRepository(source);
                listCommandResourceTasks.Add(sourceRepository.GetResourceAsync<ListCommandResource>());
            }
            var listCommandResources = await Task.WhenAll(listCommandResourceTasks);

            var listEndpoints = new List<string>();
            for (int i = 0; i < listCommandResources.Length; i++)
            {
                string listEndpoint = null;
                var listCommandResource = listCommandResources[i];
                if (listCommandResource != null)
                {
                    listEndpoint = listCommandResource.GetListEndpoint();
                }

                if (listEndpoint != null)
                {
                    listEndpoints.Add(listEndpoint);
                }
                else
                {
                    var message = string.Format(
                        LocalizedResourceManager.GetString("ListCommand_ListNotSupported"),
                        packageSources[i].Source);

                    Console.LogWarning(message);
                }
            }

            return listEndpoints;
        }

        public override async Task ExecuteCommandAsync()
        {
            if (Verbose)
            {
                Console.WriteWarning(LocalizedResourceManager.GetString("Option_VerboseDeprecated"));
                Verbosity = Verbosity.Detailed;
            }

            var listEndpoints = await GetListEndpointsAsync();
            var packages = GetPackages(listEndpoints);

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
                        Console.PrintJustified(0, p.Id);
                        Console.PrintJustified(1, p.Version.ToString());
                        Console.PrintJustified(1, p.Description);
                        if (!string.IsNullOrEmpty(p.LicenseUrl?.OriginalString))
                        {
                            Console.PrintJustified(1,
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    LocalizedResourceManager.GetString("ListCommand_LicenseUrl"),
                                    p.LicenseUrl.OriginalString));
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
                        Console.PrintJustified(0, p.GetFullName());
                        hasPackages = true;
                    }
                }
            }

            if (!hasPackages)
            {
                Console.WriteLine(LocalizedResourceManager.GetString("ListCommandNoPackages"));
            }
        }
    }
}