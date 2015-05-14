// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.ContentModel;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class RestoreCommand
    {
        // Temporary until we have RESX and loc
        private const string MSBuildMultiTargetWarning = "Packages containing MSBuild targets and props files cannot be properly installed in projects targetting multiple frameworks. See https://docs.nuget.org/something for more information.";

        private readonly ILogger _log;

        public RestoreCommand(ILogger logger)
        {
            _log = logger;
        }

        public async Task<RestoreResult> ExecuteAsync(RestoreRequest request)
        {
            if (request.Project.TargetFrameworks.Count == 0)
            {
                _log.LogError("The project does not specify any target frameworks!");
                return new RestoreResult(success: false, restoreGraphs: Enumerable.Empty<RestoreTargetGraph>());
            }

            var projectLockFilePath = string.IsNullOrEmpty(request.LockFilePath) ?
                Path.Combine(request.Project.BaseDirectory, LockFileFormat.LockFileName) :
                request.LockFilePath;

            _log.LogInformation($"Restoring packages for '{request.Project.FilePath}'");

            _log.LogWarning("TODO: Read and use lock file");

            // Load repositories
            var projectResolver = new PackageSpecResolver(request.Project);
            var nugetRepository = Repository.Factory.GetCoreV3(request.PackagesDirectory);

            var context = new RemoteWalkContext();

            context.ProjectLibraryProviders.Add(
                new LocalDependencyProvider(
                    new PackageSpecReferenceDependencyProvider(projectResolver)));

            if (request.ExternalProjects != null)
            {
                context.ProjectLibraryProviders.Add(
                    new LocalDependencyProvider(
                        new ExternalProjectReferenceDependencyProvider(request.ExternalProjects)));
            }

            context.LocalLibraryProviders.Add(
                new SourceRepositoryDependencyProvider(nugetRepository, _log));

            foreach (var provider in request.Sources.Select(s => CreateProviderFromSource(s, request.NoCache)))
            {
                context.RemoteLibraryProviders.Add(provider);
            }

            var remoteWalker = new RemoteDependencyWalker(context);

            var projectRange = new LibraryRange()
            {
                Name = request.Project.Name,
                VersionRange = new VersionRange(request.Project.Version),
                TypeConstraint = LibraryTypes.Project
            };

            // Resolve dependency graphs
            var frameworks = request.Project.TargetFrameworks.Select(f => f.FrameworkName).ToList();
            var graphs = new List<RestoreTargetGraph>();
            var frameworkTasks = new List<Task<RestoreTargetGraph>>();

            foreach (var framework in frameworks)
            {
                frameworkTasks.Add(WalkDependencies(projectRange, framework, remoteWalker, context));
            }

            graphs.AddRange(await Task.WhenAll(frameworkTasks));

            if (graphs.Any(g => g.InConflict))
            {
                _log.LogError("Failed to resolve conflicts");
                return new RestoreResult(success: false, restoreGraphs: graphs);
            }

            // Install the runtime-agnostic packages
            var allInstalledPackages = new HashSet<LibraryIdentity>();
            var localRepository = new NuGetv3LocalRepository(request.PackagesDirectory, checkPackageIdCase: false);
            await InstallPackages(graphs, request.PackagesDirectory, allInstalledPackages, request.MaxDegreeOfConcurrency);

            // Resolve runtime dependencies
            var runtimeGraphs = new List<RestoreTargetGraph>();
            if (request.Project.RuntimeGraph.Runtimes.Count > 0)
            {
                var runtimeTasks = new List<Task<RestoreTargetGraph[]>>();
                foreach (var graph in graphs)
                {
                    runtimeTasks.Add(WalkRuntimeDependencies(projectRange, graph, request.Project.RuntimeGraph, remoteWalker, context, localRepository));
                }

                foreach (var runtimeSpecificGraphs in await Task.WhenAll(runtimeTasks))
                {
                    runtimeGraphs.AddRange(runtimeSpecificGraphs);
                }

                graphs.AddRange(runtimeGraphs);

                if (runtimeGraphs.Any(g => g.InConflict))
                {
                    _log.LogError("Failed to resolve conflicts");
                    return new RestoreResult(success: false, restoreGraphs: graphs);
                }

                // Install runtime-specific packages
                await InstallPackages(runtimeGraphs, request.PackagesDirectory, allInstalledPackages, request.MaxDegreeOfConcurrency);
            }
            else
            {
                _log.LogVerbose("Skipping runtime dependency walk, no runtimes defined in project.json");
            }

            // Build the lock file
            var repository = new NuGetv3LocalRepository(request.PackagesDirectory, checkPackageIdCase: false);
            var lockFile = CreateLockFile(request.Project, graphs, repository);
            var lockFileFormat = new LockFileFormat();
            lockFileFormat.Write(projectLockFilePath, lockFile);

            // Generate Targets/Props files
            WriteTargetsAndProps(request.Project, graphs, repository);

            return new RestoreResult(true, graphs, lockFile);
        }

        private void WriteTargetsAndProps(PackageSpec project, List<RestoreTargetGraph> targetGraphs, NuGetv3LocalRepository repository)
        {
            // Get the runtime-independent graphs
            var tfmGraphs = targetGraphs.Where(g => string.IsNullOrEmpty(g.RuntimeIdentifier)).ToList();
            if(tfmGraphs.Count > 1)
            {
                var name = $"{project.Name}.nuget.targets";
                var path = Path.Combine(project.BaseDirectory, name);
                _log.LogInformation($"Generating MSBuild file {name}");

                GenerateMSBuildErrorFile(path); 
                return;
            }
            var graph = tfmGraphs[0];

            var pathResolver = new DefaultPackagePathResolver(repository.RepositoryRoot);

            var targets = new List<string>();
            var props = new List<string>();
            foreach (var library in graph.Flattened.Distinct().OrderBy(g => g.Data.Match.Library))
            {
                var package = repository.FindPackagesById(library.Key.Name).FirstOrDefault(p => p.Version == library.Key.Version);
                if (package != null)
                {
                    var criteria = graph.Conventions.Criteria.ForFramework(graph.Framework);
                    var contentItemCollection = new ContentItemCollection();
                    using (var nupkgStream = File.OpenRead(package.ZipPath))
                    {
                        var reader = new PackageReader(nupkgStream);
                        contentItemCollection.Load(reader.GetFiles());
                    }

                    // Find MSBuild thingies
                    var buildItems = contentItemCollection.FindBestItemGroup(criteria, graph.Conventions.Patterns.MSBuildFiles);
                    if (buildItems != null)
                    {
                        // We need to additionally filter to items that are named "{packageId}.targets" and "{packageId}.props"
                        // Filter by file name here and we'll filter by extension when we add things to the lists.
                        var items = buildItems.Items
                            .Where(item => Path.GetFileNameWithoutExtension(item.Path).Equals(package.Id, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        targets.AddRange(items
                            .Select(c => c.Path)
                            .Where(path => Path.GetExtension(path).Equals(".targets", StringComparison.OrdinalIgnoreCase))
                            .Select(path => Path.Combine(pathResolver.GetPackageDirectory(package.Id, package.Version), path.Replace('/', Path.DirectorySeparatorChar))));
                        props.AddRange(items
                            .Select(c => c.Path)
                            .Where(path => Path.GetExtension(path).Equals(".props", StringComparison.OrdinalIgnoreCase))
                            .Select(path => Path.Combine(pathResolver.GetPackageDirectory(package.Id, package.Version), path.Replace('/', Path.DirectorySeparatorChar))));
                    }
                }
            }

            // Generate the files as needed
            if(targets.Any())
            {
                var name = $"{project.Name}.nuget.targets";
                var path = Path.Combine(project.BaseDirectory, name);
                _log.LogInformation($"Generating MSBuild file {name}");

                GenerateImportsFile(repository, path, targets); 
            }
            if(props.Any())
            {
                var name = $"{project.Name}.nuget.props";
                var path = Path.Combine(project.BaseDirectory, name);
                _log.LogInformation($"Generating MSBuild file {name}");

                GenerateImportsFile(repository, path, props);
            }
        }

        private void GenerateMSBuildErrorFile(string path)
        {
            var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "no"),

                new XElement(ns + "Project",
                    new XAttribute("ToolsVersion", "14.0"),

                    new XElement(ns + "Target",
                        new XAttribute("Name", "EmitMSBuildWarning"),
                        new XAttribute("BeforeTargets", "Build"),

                        new XElement(ns + "Warning",
                            new XAttribute("Text", MSBuildMultiTargetWarning)))));

            using (var output = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                doc.Save(output);
            }
        }

        private void GenerateImportsFile(NuGetv3LocalRepository repository, string path, List<string> imports)
        {
            var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "no"),

                new XElement(ns + "Project",
                    new XAttribute("ToolsVersion", "14.0"),

                    new XElement(ns + "PropertyGroup",
                        new XAttribute("Condition", "'$(NuGetPackageRoot)' == ''"),

                        new XElement(ns + "NuGetPackageRoot", repository.RepositoryRoot)),
                    new XElement(ns + "ImportGroup", imports.Select(i =>
                        new XElement(ns + "Import",
                            new XAttribute("Project", Path.Combine("$(NuGetPackageRoot)", i)),
                            new XAttribute("Condition", $"Exists('{Path.Combine("$(NuGetPackageRoot)", i)}')"))))));

            using (var output = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                doc.Save(output);
            }
        }

        private LockFile CreateLockFile(PackageSpec project, List<RestoreTargetGraph> targetGraphs, NuGetv3LocalRepository repository)
        {
            var lockFile = new LockFile();

            using (var sha512 = SHA512.Create())
            {
                foreach (var item in targetGraphs.SelectMany(g => g.Flattened).Distinct().OrderBy(x => x.Data.Match.Library))
                {
                    var library = item.Data.Match.Library;
                    var packageInfo = repository.FindPackagesById(library.Name)
                        .FirstOrDefault(p => p.Version == library.Version);

                    if (packageInfo == null)
                    {
                        continue;
                    }

                    var lockFileLib = CreateLockFileLibrary(
                        packageInfo,
                        sha512,
                        correctedPackageName: library.Name);

                    lockFile.Libraries.Add(lockFileLib);
                }
            }

            // Use empty string as the key of dependencies shared by all frameworks
            lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                string.Empty,
                project.Dependencies.Select(x => x.LibraryRange.ToString())));

            foreach (var frameworkInfo in project.TargetFrameworks)
            {
                lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                    frameworkInfo.FrameworkName.ToString(),
                    frameworkInfo.Dependencies.Select(x => x.LibraryRange.ToString())));
            }

            // Add the targets
            foreach (var targetGraph in targetGraphs)
            {
                var target = new LockFileTarget();
                target.TargetFramework = targetGraph.Framework;
                target.RuntimeIdentifier = targetGraph.RuntimeIdentifier;

                foreach (var library in targetGraph.Flattened.Select(g => g.Key).OrderBy(x => x))
                {
                    var packageInfo = repository.FindPackagesById(library.Name)
                        .FirstOrDefault(p => p.Version == library.Version);

                    if (packageInfo == null)
                    {
                        continue;
                    }

                    var targetLibrary = CreateLockFileTargetLibrary(
                        packageInfo,
                        targetGraph,
                        new DefaultPackagePathResolver(repository.RepositoryRoot),
                        correctedPackageName: library.Name);

                    target.Libraries.Add(targetLibrary);
                }

                lockFile.Targets.Add(target);
            }

            return lockFile;
        }

        private LockFileLibrary CreateLockFileLibrary(LocalPackageInfo package, SHA512 sha512, string correctedPackageName)
        {
            var lockFileLib = new LockFileLibrary();

            // package.Id is read from nuspec and it might be in wrong casing.
            // correctedPackageName should be the package name used by dependency graph and
            // it has the correct casing that runtime needs during dependency resolution.
            lockFileLib.Name = correctedPackageName ?? package.Id;
            lockFileLib.Version = package.Version;

            using (var nupkgStream = File.OpenRead(package.ZipPath))
            {
                lockFileLib.Sha512 = Convert.ToBase64String(sha512.ComputeHash(nupkgStream));
                nupkgStream.Seek(0, SeekOrigin.Begin);

                var packageReader = new PackageReader(nupkgStream);

                // Get package files, excluding directory entries
                lockFileLib.Files = packageReader.GetFiles().Where(x => !x.EndsWith("/")).ToList();
            }

            return lockFileLib;
        }

        private LockFileTargetLibrary CreateLockFileTargetLibrary(LocalPackageInfo package, RestoreTargetGraph targetGraph, DefaultPackagePathResolver defaultPackagePathResolver, string correctedPackageName)
        {
            var lockFileLib = new LockFileTargetLibrary();

            var framework = targetGraph.Framework;
            var runtimeIdentifier = targetGraph.RuntimeIdentifier;

            // package.Id is read from nuspec and it might be in wrong casing.
            // correctedPackageName should be the package name used by dependency graph and
            // it has the correct casing that runtime needs during dependency resolution.
            lockFileLib.Name = correctedPackageName ?? package.Id;
            lockFileLib.Version = package.Version;

            IList<string> files;
            var contentItems = new ContentItemCollection();
            HashSet<string> referenceFilter = null;
            using (var nupkgStream = File.OpenRead(package.ZipPath))
            {
                var packageReader = new PackageReader(nupkgStream);
                files = packageReader.GetFiles().Select(p => p.Replace(Path.DirectorySeparatorChar, '/')).ToList();

                contentItems.Load(files);

                var dependencySet = packageReader.GetPackageDependencies().GetNearest(framework);
                if (dependencySet != null)
                {
                    var set = dependencySet.Packages;

                    if (set != null)
                    {
                        lockFileLib.Dependencies = set.ToList();
                    }
                }

                var referenceSet = packageReader.GetReferenceItems().GetNearest(framework);
                if (referenceSet != null)
                {
                    referenceFilter = new HashSet<string>(referenceSet.Items, StringComparer.OrdinalIgnoreCase);
                }

                // TODO: Remove this when we do #596
                // ASP.NET Core isn't compatible with generic PCL profiles
                if (!string.Equals(framework.Framework, FrameworkConstants.FrameworkIdentifiers.AspNetCore, StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.Equals(framework.Framework, FrameworkConstants.FrameworkIdentifiers.DnxCore, StringComparison.OrdinalIgnoreCase))
                {
                    var frameworkAssemblies = packageReader.GetFrameworkItems().GetNearest(framework);
                    if (frameworkAssemblies != null)
                    {
                        foreach (var assemblyReference in frameworkAssemblies.Items)
                        {
                            lockFileLib.FrameworkAssemblies.Add(assemblyReference);
                        }
                    }
                }
            }

            var nativeCriteria = targetGraph.Conventions.Criteria.ForRuntime(targetGraph.RuntimeIdentifier);
            var managedCriteria = targetGraph.Conventions.Criteria.ForFrameworkAndRuntime(framework, targetGraph.RuntimeIdentifier);

            var compileGroup = contentItems.FindBestItemGroup(managedCriteria, targetGraph.Conventions.Patterns.CompileAssemblies, targetGraph.Conventions.Patterns.RuntimeAssemblies);

            if (compileGroup != null)
            {
                lockFileLib.CompileTimeAssemblies = compileGroup.Items.Select(t => new LockFileItem(t.Path)).ToList();
            }

            var runtimeGroup = contentItems.FindBestItemGroup(managedCriteria, targetGraph.Conventions.Patterns.RuntimeAssemblies);
            if (runtimeGroup != null)
            {
                lockFileLib.RuntimeAssemblies = runtimeGroup.Items.Select(p => new LockFileItem(p.Path)).ToList();
            }

            var resourceGroup = contentItems.FindBestItemGroup(managedCriteria, targetGraph.Conventions.Patterns.ResourceAssemblies);
            if (resourceGroup != null)
            {
                lockFileLib.ResourceAssemblies = resourceGroup.Items.Select(ToResourceLockFileItem).ToList();
            }

            var nativeGroup = contentItems.FindBestItemGroup(nativeCriteria, targetGraph.Conventions.Patterns.NativeLibraries);
            if (nativeGroup != null)
            {
                lockFileLib.NativeLibraries = nativeGroup.Items.Select(p => new LockFileItem(p.Path)).ToList();
            }

            // COMPAT: Support lib/contract so older packages can be consumed
            var contractPath = "lib/contract/" + package.Id + ".dll";
            var hasContract = files.Any(path => path == contractPath);
            var hasLib = lockFileLib.RuntimeAssemblies.Any();

            if (hasContract
                && hasLib
                && !framework.IsDesktop())
            {
                lockFileLib.CompileTimeAssemblies.Clear();
                lockFileLib.CompileTimeAssemblies.Add(new LockFileItem(contractPath));
            }

            // Apply filters from the <references> node in the nuspec
            if (referenceFilter != null)
            {
                // Remove anything that starts with "lib/" and is NOT specified in the reference filter.
                // runtimes/* is unaffected (it doesn't start with lib/)
                lockFileLib.RuntimeAssemblies = lockFileLib.RuntimeAssemblies.Where(p => !p.Path.StartsWith("lib/") || referenceFilter.Contains(p.Path)).ToList();
                lockFileLib.CompileTimeAssemblies = lockFileLib.CompileTimeAssemblies.Where(p => !p.Path.StartsWith("lib/") || referenceFilter.Contains(p.Path)).ToList();
            }

            return lockFileLib;
        }

        private static LockFileItem ToResourceLockFileItem(ContentItem item)
        {
            return new LockFileItem(item.Path)
            {
                Properties =
                {
                    { "locale", item.Properties["locale"].ToString()}
                }
            };
        }

        private Task<RestoreTargetGraph> WalkDependencies(LibraryRange projectRange, NuGetFramework framework, RemoteDependencyWalker walker, RemoteWalkContext context)
        {
            return WalkDependencies(projectRange, framework, null, RuntimeGraph.Empty, walker, context);
        }

        private async Task<RestoreTargetGraph> WalkDependencies(LibraryRange projectRange, NuGetFramework framework, string runtimeIdentifier, RuntimeGraph runtimeGraph, RemoteDependencyWalker walker, RemoteWalkContext context)
        {
            _log.LogInformation($"Restoring packages for {framework}");
            var graph = await walker.WalkAsync(
                projectRange,
                framework,
                runtimeIdentifier,
                runtimeGraph);

            // Resolve conflicts
            _log.LogVerbose($"Resolving Conflicts for {framework}");
            var inConflict = !graph.TryResolveConflicts();

            // Flatten and create the RestoreTargetGraph to hold the packages
            return RestoreTargetGraph.Create(inConflict, framework, runtimeIdentifier, runtimeGraph, graph, context, _log);
        }

        private Task<RestoreTargetGraph[]> WalkRuntimeDependencies(LibraryRange projectRange, RestoreTargetGraph graph, RuntimeGraph projectRuntimeGraph, RemoteDependencyWalker walker, RemoteWalkContext context, NuGetv3LocalRepository localRepository)
        {
            // Load runtime specs
            _log.LogVerbose("Scanning packages for runtime.json files...");
            var runtimeGraph = projectRuntimeGraph;
            graph.Graph.ForEach(node =>
                {
                    var match = node?.Item?.Data?.Match;
                    if (match == null)
                    {
                        return;
                    }

                    // Locate the package in the local repository
                    var package = localRepository.FindPackagesById(match.Library.Name).FirstOrDefault(p => p.Version == match.Library.Version);
                    if (package != null)
                    {
                        var nextGraph = LoadRuntimeGraph(package);
                        if (nextGraph != null)
                        {
                            _log.LogVerbose($"Merging in runtimes defined in {match.Library}");
                            runtimeGraph = RuntimeGraph.Merge(runtimeGraph, nextGraph);
                        }
                    }
                });

            var resultGraphs = new List<Task<RestoreTargetGraph>>();
            foreach (var runtimeName in projectRuntimeGraph.Runtimes.Keys)
            {
                _log.LogInformation($"Restoring packages for {graph.Framework} on {runtimeName}");
                resultGraphs.Add(WalkDependencies(projectRange, graph.Framework, runtimeName, runtimeGraph, walker, context));
            }

            return Task.WhenAll(resultGraphs);
        }

        private RuntimeGraph LoadRuntimeGraph(LocalPackageInfo package)
        {
            var runtimeGraphFile = Path.Combine(package.ExpandedPath, RuntimeGraph.RuntimeGraphFileName);
            if (File.Exists(runtimeGraphFile))
            {
                using (var stream = File.OpenRead(runtimeGraphFile))
                {
                    return JsonRuntimeFormat.ReadRuntimeGraph(stream);
                }
            }
            return null;
        }

        private async Task InstallPackages(IEnumerable<RestoreTargetGraph> graphs, string packagesDirectory, HashSet<LibraryIdentity> allInstalledPackages, int maxDegreeOfConcurrency)
        {
            var packagesToInstall = graphs.SelectMany(g => g.Install.Where(match => allInstalledPackages.Add(match.Library)));
            if (maxDegreeOfConcurrency <= 1)
            {
                foreach (var match in packagesToInstall)
                {
                    await InstallPackage(match, packagesDirectory);
                }
            }
            else
            {
                var bag = new ConcurrentBag<RemoteMatch>(packagesToInstall);
                var tasks = Enumerable.Range(0, maxDegreeOfConcurrency)
                    .Select(async _ =>
                        {
                            RemoteMatch match;
                            while (bag.TryTake(out match))
                            {
                                await InstallPackage(match, packagesDirectory);
                            }
                        });
                await Task.WhenAll(tasks);
            }
        }

        private async Task InstallPackage(RemoteMatch installItem, string packagesDirectory)
        {
            using (var memoryStream = new MemoryStream())
            {
                await installItem.Provider.CopyToAsync(installItem.Library, memoryStream, default(CancellationToken));

                memoryStream.Seek(0, SeekOrigin.Begin);
                await NuGetPackageUtils.InstallFromStream(memoryStream, installItem.Library, packagesDirectory, _log);
            }
        }

        private IRemoteDependencyProvider CreateProviderFromSource(PackageSource source, bool noCache)
        {
            _log.LogVerbose($"Using source {source.Source}");

            var nugetRepository = Repository.Factory.GetCoreV3(source.Source);
            return new SourceRepositoryDependencyProvider(nugetRepository, _log, noCache);
        }
    }
}
