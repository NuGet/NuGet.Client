using Microsoft.Framework.Logging;
using NuGet.Client;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.ProjectModel;
using NuGet.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ILogger = Microsoft.Framework.Logging.ILogger;
using System;
using NuGet.LibraryModel;
using NuGet.Versioning;
using System.IO;
using NuGet.Frameworks;
using NuGet.RuntimeModel;

namespace NuGet.Strawman.Commands
{
    public class RestoreCommand
    {
        private readonly ILoggerFactory _loggerFactory;
        private ILogger _log;

        public RestoreCommand(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _log = loggerFactory.CreateLogger<RestoreCommand>();
        }

        public async Task<RestoreResult> ExecuteAsync(RestoreRequest request)
        {
            if (request.Project.TargetFrameworks.Count == 0)
            {
                _log.LogError("The project does not specify any target frameworks!");
            }

            _log.LogInformation($"Restoring packages for '{request.Project.FilePath}'");

            _log.LogWarning("TODO: Read and use lock file");

            _log.LogWarning("TODO: Run prerestore script");

            // Load repositories
            var projectResolver = new PackageSpecResolver(request.Project.BaseDirectory);
            var nugetRepository = new NuGetv3LocalRepository(request.PackagesDirectory, checkPackageIdCase: true);

            var context = new RemoteWalkContext();

            context.ProjectLibraryProviders.Add(new LocalDependencyProvider(
                new PackageSpecReferenceDependencyProvider(projectResolver)));

            context.LocalLibraryProviders.Add(
                new LocalDependencyProvider(
                    new NuGetDependencyResolver(nugetRepository)));

            foreach (var provider in request.Sources.Select(CreateProviderFromSource))
            {
                context.RemoteLibraryProviders.Add(provider);
            }
            // Beware the walkers!
            var remoteWalker = new RemoteDependencyWalker(context);

            var projectRange = new LibraryRange()
            {
                Name = request.Project.Name,
                VersionRange = new VersionRange(request.Project.Version),
                TypeConstraint = LibraryTypes.Project
            };

            // Resolve dependency graphs
            var frameworks = request.Project.TargetFrameworks.Select(f => f.FrameworkName).ToList();
            List<RestoreGraph> graphs = await WalkDependencies(
                projectRange,
                frameworks,
                remoteWalker);

            if(!ResolveConflicts(graphs))
            {
                _log.LogError("Failed to resolve conflicts");
                return new RestoreResult(success: false, restoreGraphs: graphs);
            }

            // Resolve runtime dependencies
            if (request.Project.RuntimeGraph.Runtimes.Count > 0)
            {
                graphs.AddRange(await WalkRuntimeDependencies(projectRange, graphs, frameworks, request.Project.RuntimeGraph, remoteWalker));
            }
            else
            {
                _log.LogVerbose("Skipping runtime dependency walk, no runtimes defined in project.json");
            }

            var libraries = new HashSet<LibraryIdentity>();
            var installItems = new List<GraphItem<RemoteResolveResult>>();
            var missingItems = new HashSet<LibraryRange>();
            var graphItems = new List<GraphItem<RemoteResolveResult>>();

            bool success = FlattenDependencyGraph(context, graphs, libraries, installItems, missingItems, graphItems);

            await InstallPackages(installItems, request.PackagesDirectory, request.DryRun);

            return new RestoreResult(success, graphs);
        }

        private async Task<List<RestoreGraph>> WalkRuntimeDependencies(LibraryRange projectRange, IEnumerable<RestoreGraph> graphs, IEnumerable<NuGetFramework> frameworks, RuntimeGraph projectRuntimes, RemoteDependencyWalker walker)
        {
            var restoreGraphs = new List<RestoreGraph>();
            foreach (var graph in graphs) 
            {
                // Load runtime specs
                _log.LogVerbose("Scanning packages for runtime.json files...");
                var runtimeFilePackages = new List<LibraryIdentity>();
                var runtimeFileTasks = new List<Task<RuntimeGraph>>();
                graph.Graph.ForEach(node =>
                {
                    var match = node?.Item?.Data?.Match;
                    if (match == null) { return; }
                    runtimeFilePackages.Add(match.Library);
                    runtimeFileTasks.Add(match.Provider.GetRuntimeGraph(node.Item.Data.Match, graph.Framework));
                });

                var libraryRuntimeFiles = await Task.WhenAll(runtimeFileTasks);

                // Build the complete runtime graph
                var runtimeGraph = projectRuntimes;
                foreach(var runtimePair in libraryRuntimeFiles.Zip(runtimeFilePackages, Tuple.Create).Where(file => file.Item1 != null))
                {
                    _log.LogVerbose($"Merging in runtimes defined in {runtimePair.Item2}");
                    runtimeGraph = RuntimeGraph.Merge(runtimeGraph, runtimePair.Item1);
                }

                foreach (var runtimeName in projectRuntimes.Runtimes.Keys)
                {
                    // Walk dependencies for the runtime
                    _log.LogInformation($"Restoring packages for {graph.Framework} on {runtimeName}");
                    restoreGraphs.Add(new RestoreGraph(
                        runtimeName,
                        graph.Framework,
                        await walker.Walk(
                            projectRange,
                            graph.Framework,
                            runtimeName,
                            runtimeGraph)));
                }
            }
            return restoreGraphs;
        }

        private bool FlattenDependencyGraph(RemoteWalkContext context, List<RestoreGraph> graphs, HashSet<LibraryIdentity> libraries, List<GraphItem<RemoteResolveResult>> installItems, HashSet<LibraryRange> missingItems, List<GraphItem<RemoteResolveResult>> graphItems)
        {
            bool success = true;
            foreach (var g in graphs)
            {
                g.Graph.ForEach(node =>
                {
                    if (node == null || node.Key == null || node.Disposition == Disposition.Rejected)
                    {
                        return;
                    }

                    if (node.Item == null || node.Item.Data.Match == null)
                    {
                        if (node.Key.TypeConstraint != LibraryTypes.Reference &&
                            node.Key.VersionRange != null &&
                            missingItems.Add(node.Key))
                        {
                            var errorMessage = string.Format("Unable to locate {0} {1}",
                                node.Key.Name,
                                node.Key.VersionRange);
                            _log.LogError(errorMessage);
                            success = false;
                        }

                        return;
                    }

                    if (!string.Equals(node.Item.Data.Match.Library.Name, node.Key.Name, StringComparison.Ordinal))
                    {
                        // Fix casing of the library name to be installed
                        node.Item.Data.Match.Library.Name = node.Key.Name;
                    }

                    var isRemote = context.RemoteLibraryProviders.Contains(node.Item.Data.Match.Provider);
                    var isAdded = installItems.Any(item => item.Data.Match.Library == node.Item.Data.Match.Library);

                    if (!isAdded && isRemote)
                    {
                        installItems.Add(node.Item);
                    }

                    var isGraphItem = graphItems.Any(item => item.Data.Match.Library == node.Item.Data.Match.Library);
                    if (!isGraphItem)
                    {
                        graphItems.Add(node.Item);
                    }

                    libraries.Add(node.Item.Key);
                });
            }
            return success;
        }

        private bool ResolveConflicts(List<RestoreGraph> graphs)
        {
            foreach (var graph in graphs)
            {
                string runtimeStr = string.IsNullOrEmpty(graph.RuntimeIdentifier) ? string.Empty : $"on {graph.RuntimeIdentifier}";
                _log.LogVerbose($"Resolving Conflicts for {graph.Framework}{runtimeStr}");
                if (!graph.Graph.TryResolveConflicts())
                {
                    return false;
                }
            }
            return true;
        }

        private async Task<List<RestoreGraph>> WalkDependencies(LibraryRange projectRange, IEnumerable<NuGetFramework> frameworks, RemoteDependencyWalker walker)
        {
            var graphs = new List<RestoreGraph>();
            foreach (var framework in frameworks)
            {
                _log.LogInformation($"Restoring packages for {framework}");
                var graph = await walker.Walk(
                    projectRange,
                    framework,
                    runtimeName: null,
                    runtimeGraph: null);
                graphs.Add(new RestoreGraph(string.Empty, framework, graph));
            }

            return graphs;
        }

        private async Task InstallPackages(List<GraphItem<RemoteResolveResult>> installItems, string packagesDirectory, bool dryRun)
        {
            foreach (var installItem in installItems)
            {
                if (dryRun)
                {
                    _log.LogInformation($"Would install {installItem.Data.Match.Library.Name} {installItem.Data.Match.Library.Version}");
                }
                else
                {
                    await InstallPackage(installItem, packagesDirectory);
                }
            }
        }

        private async Task InstallPackage(GraphItem<RemoteResolveResult> installItem, string packagesDirectory)
        {
            using (var memoryStream = new MemoryStream())
            {
                var match = installItem.Data.Match;
                await match.Provider.CopyToAsync(installItem.Data.Match, memoryStream);

                memoryStream.Seek(0, SeekOrigin.Begin);
                await NuGetPackageUtils.InstallFromStream(memoryStream, match.Library, packagesDirectory, _log);
            }
        }

        private IRemoteDependencyProvider CreateProviderFromSource(PackageSource source)
        {
            var logger = new NuGetLoggerAdapter(
                _loggerFactory.CreateLogger(
                    typeof(IPackageFeed).FullName + ":" + source.Source));
            var feed = PackageFeedFactory.CreateFeed(
                source.Source,
                source.UserName,
                source.Password,
                noCache: false,
                ignoreFailedSources: false,
                logger: logger);
            _log.LogVerbose($"Using source {source.Source}");
            return new RemoteDependencyProvider(feed);
        }
    }
}
