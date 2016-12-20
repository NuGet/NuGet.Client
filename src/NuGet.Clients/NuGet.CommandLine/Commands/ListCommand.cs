using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CommandLine;
using NuGet.Commands;
using NuGet.Commands.ListCommand;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine
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

        public IListCommandRunner ListCommandRunner { get; set; }

        private async Task<IList<KeyValuePair<Configuration.PackageSource, string>>> GetListEndpointsAsync()
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

            var sourceRepositoryProvider = new CommandLineSourceRepositoryProvider(SourceProvider);

            var listCommandResourceTasks = packageSources
                .Select(source =>
                {
                    var sourceRepository = sourceRepositoryProvider.CreateRepository(source);
                    return sourceRepository.GetResourceAsync<ListCommandResource>();
                })
                .ToList();

            var listCommandResources = await Task.WhenAll(listCommandResourceTasks);

            var listEndpoints = packageSources.Zip(
                listCommandResources,
                (p, l) => new KeyValuePair<Configuration.PackageSource, string>(p, l?.GetListEndpoint()));

            var partitioned = listEndpoints.ToLookup(kv => kv.Value != null);

            foreach (var packageSource in partitioned[key: false].Select(kv => kv.Key))
            {
                var message = string.Format(
                    LocalizedResourceManager.GetString("ListCommand_ListNotSupported"),
                    packageSource.Source);

                Console.LogWarning(message);
            }

            return partitioned[key: true].ToList();
        }

        public async override Task ExecuteCommandAsync()
        {
            if (Verbose)
            {
                Console.WriteWarning(LocalizedResourceManager.GetString("Option_VerboseDeprecated"));
                Verbosity = Verbosity.Detailed;
            }

            if ((!Arguments.Any() || string.IsNullOrWhiteSpace(Arguments[0])))
            {
                HelpCommand.ViewHelpForCommand(CommandAttribute.CommandName);
                return;
            }

            if (ListCommandRunner == null)
            {
                ListCommandRunner = new ListCommandRunner();
            }
            var listEndpoints = await GetListEndpointsAsync();

            var list = new ListArgs(Arguments,
                listEndpoints,
                Settings,
                Console,
                Console.PrintJustified,
                Verbosity == Verbosity.Detailed,
                LocalizedResourceManager.GetString("ListCommandNoPackages"), 
                LocalizedResourceManager.GetString("ListCommand_LicenseUrl"),
                AllVersions, 
                IncludeDelisted,
                Prerelease,
                CancellationToken.None);

            await ListCommandRunner.ExecuteCommand(list);
        }
    }
}