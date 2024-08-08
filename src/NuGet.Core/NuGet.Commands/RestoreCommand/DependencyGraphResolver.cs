// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    internal static class ExtensionMethodsAndStuff
    {
        public static HashSet<T> Empty<T>(this HashSet<T> _) => new HashSet<T>();
    }

    /// <summary>
    /// Represents a class that can resolve a dependency graph.
    /// </summary>
    internal sealed class DependencyGraphResolver
    {
        private readonly RestoreCollectorLogger _logger;
        private readonly Guid _operationId;
        private readonly RestoreRequest _restoreRequest;
        private readonly TelemetryActivity _telemetryActivity;

        public DependencyGraphResolver(RestoreCollectorLogger logger, RestoreRequest restoreRequest, TelemetryActivity telemetryActivity, Guid operationId)
        {
            _logger = logger;
            _restoreRequest = restoreRequest;
            _telemetryActivity = telemetryActivity;
            _operationId = operationId;
        }

        private enum LibraryDependencyIndex : int
        {
            Invalid = -1,
        }

        private enum LibraryRangeIndex : int
        {
            Invalid = -1,
        }

        public async Task<ValueTuple<bool, IEnumerable<RestoreTargetGraph>?>> ResolveAsync(NuGetv3LocalRepository userPackageFolder, IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders, RemoteWalkContext context, List<ExternalProjectReference> projectReferences, TelemetryActivity telemetryActivity, CancellationToken cancellationToken)
        {
            bool success = true;

            var projectRestoreRequest = new ProjectRestoreRequest(_restoreRequest, _restoreRequest.Project, _restoreRequest.ExistingLockFile, _logger)
            {
                ParentId = _operationId
            };

            var projectRestoreCommand = new ProjectRestoreCommand(projectRestoreRequest);

            var localRepositories = new List<NuGetv3LocalRepository>(capacity: fallbackPackageFolders.Count + 1)
            {
                userPackageFolder,
            };

            localRepositories.AddRange(fallbackPackageFolders);

            context.ProjectLibraryProviders.Add(new PackageSpecReferenceDependencyProvider(projectReferences, _logger));

            LibraryRangeInterningTable libraryRangeInterningTable = new LibraryRangeInterningTable();
            LibraryDependencyInterningTable libraryDependencyInterningTable = new LibraryDependencyInterningTable();

            List<RestoreTargetGraph> allGraphs = new();
            RuntimeGraph allRuntimes = RuntimeGraph.Empty;
            Dictionary<NuGetFramework, RestoreTargetGraph> restoreTargetGraphsByFramework = new();

            bool havePackagesBeenInstalled = false;

            telemetryActivity.StartIntervalMeasure();

            foreach (FrameworkRuntimePair frameworkRuntimePair in RestoreCommand.CreateFrameworkRuntimePairs(_restoreRequest.Project, RequestRuntimeUtility.GetRestoreRuntimes(_restoreRequest)))
            {
                // Install packages once we get to the first RID-specific framework/runtime pair but only once
                if (!string.IsNullOrWhiteSpace(frameworkRuntimePair.RuntimeIdentifier) && !havePackagesBeenInstalled)
                {
                    success &= await RestoreCommand.InstallPackagesAsync(_restoreRequest.Project, allGraphs, context, projectRestoreCommand, userPackageFolder, _telemetryActivity, cancellationToken);

                    if (!success)
                    {
                        return (false, null);
                    }

                    havePackagesBeenInstalled = true;
                }

                (RestoreTargetGraph restoreTargetGraph, RuntimeGraph? runtimeGraph) = CreateRestoreTargetGraph(frameworkRuntimePair, allRuntimes, restoreTargetGraphsByFramework, localRepositories);

                Dictionary<LibraryDependencyIndex, ResolvedItem> resolvedItems = new(capacity: 2048);

                Dictionary<LibraryRangeIndex, FindLibraryCachedAsyncResult> findLibraryCachedAsyncResultCache = new Dictionary<LibraryRangeIndex, FindLibraryCachedAsyncResult>(2048);

                LibraryDependency initialProject = new LibraryDependency(new LibraryRange()
                {
                    Name = _restoreRequest.Project.Name,
                    VersionRange = new VersionRange(_restoreRequest.Project.Version),
                    TypeConstraint = LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject
                });

                DependencyGraphItem projectDependencyGraphItem = new()
                {
                    DirectPackageReferenceFromRootProject = false,
                    LibraryDependency = initialProject,
                    LibraryDependencyIndex = libraryDependencyInterningTable.Intern(initialProject),
                    LibraryRangeIndex = libraryRangeInterningTable.Intern(initialProject.LibraryRange),
                    Path = Array.Empty<LibraryRangeIndex>(),
                    Suppressions = new HashSet<LibraryDependencyIndex>(),
                    VersionOverrides = new Dictionary<LibraryDependencyIndex, VersionRange>(),
                };

                success &= ResolveItems(initialProject, projectDependencyGraphItem, resolvedItems, findLibraryCachedAsyncResultCache, frameworkRuntimePair, restoreTargetGraph, runtimeGraph, context, projectRestoreCommand, userPackageFolder, libraryRangeInterningTable, libraryDependencyInterningTable, cancellationToken);

                if (!success)
                {
                    return (false, null);
                }

                success &= FlattenResolvedItems(initialProject, projectDependencyGraphItem, resolvedItems, findLibraryCachedAsyncResultCache, context, restoreTargetGraph);

                foreach (var profile in _restoreRequest.Project.RuntimeGraph.Supports)
                {
                    var runtimes = allRuntimes;

                    CompatibilityProfile? compatProfile;
                    if (profile.Value.RestoreContexts.Any())
                    {
                        // Just use the contexts from the project definition
                        compatProfile = profile.Value;
                    }
                    else if (!runtimes.Supports.TryGetValue(profile.Value.Name, out compatProfile))
                    {
                        // No definition of this profile found, so just continue to the next one
                        var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_UnknownCompatibilityProfile, profile.Key);

                        _logger.LogAsync(RestoreLogMessage.CreateWarning(NuGetLogCode.NU1502, message)).GetAwaiter().GetResult();

                        continue;
                    }

                    foreach (FrameworkRuntimePair? restoreContextPair in compatProfile.RestoreContexts)
                    {
                        _restoreRequest.CompatibilityProfiles.Add(restoreContextPair);
                    }
                }

                allGraphs.Add(restoreTargetGraph);

                if (string.IsNullOrEmpty(frameworkRuntimePair.RuntimeIdentifier))
                {
                    restoreTargetGraphsByFramework.Add(frameworkRuntimePair.Framework, restoreTargetGraph);
                }
            }

            telemetryActivity.EndIntervalMeasure(ProjectRestoreCommand.WalkFrameworkDependencyDuration);

            success &= await RestoreCommand.InstallPackagesAsync(_restoreRequest.Project, allGraphs, context, projectRestoreCommand, userPackageFolder, _telemetryActivity, cancellationToken);

            // Update the logger with the restore target graphs
            // This allows lazy initialization for the Transitive Warning Properties
            _logger.ApplyRestoreOutput(allGraphs);

            if (!success)
            {
                // Log message for any unresolved dependencies
                await UnresolvedMessages.LogAsync(allGraphs, context, cancellationToken);
            }

            return (success, allGraphs);
        }

        private static bool EvictOnTypeConstraint(LibraryDependencyTarget current, LibraryDependencyTarget previous)
        {
            if (current == previous)
            {
                return false;
            }

            if (previous == LibraryDependencyTarget.PackageProjectExternal)
            {
                LibraryDependencyTarget ppeFlags = current & LibraryDependencyTarget.PackageProjectExternal;
                LibraryDependencyTarget nonPpeFlags = current & ~LibraryDependencyTarget.PackageProjectExternal;
                return (ppeFlags != LibraryDependencyTarget.None && nonPpeFlags == LibraryDependencyTarget.None);
            }

            // TODO: Should there be other cases here?
            return false;
        }

        private (RestoreTargetGraph, RuntimeGraph?) CreateRestoreTargetGraph(FrameworkRuntimePair frameworkRuntimePair, RuntimeGraph allRuntimes, Dictionary<NuGetFramework, RestoreTargetGraph> restoreTargetGraphsByFramework, List<NuGetv3LocalRepository> localRepositories)
        {
            var restoreTargetGraph = new RestoreTargetGraph
            {
                AnalyzeResult = new AnalyzeResult<RemoteResolveResult>(),
                Conflicts = new List<ResolverConflict>(),
                InConflict = false,
                Install = new HashSet<RemoteMatch>(),
                ResolvedDependencies = new HashSet<ResolvedDependencyKey>(),
                Unresolved = new HashSet<LibraryRange>(),
                Framework = frameworkRuntimePair.Framework,
                RuntimeIdentifier = string.IsNullOrWhiteSpace(frameworkRuntimePair.RuntimeIdentifier) ? null : frameworkRuntimePair.RuntimeIdentifier,
            };

            restoreTargetGraph.Name = FrameworkRuntimePair.GetName(restoreTargetGraph.Framework, restoreTargetGraph.RuntimeIdentifier);
            restoreTargetGraph.TargetGraphName = FrameworkRuntimePair.GetTargetGraphName(restoreTargetGraph.Framework, restoreTargetGraph.RuntimeIdentifier);

            RestoreTargetGraph? tfmNonRidGraph;
            RuntimeGraph? runtimeGraph = default;

            if (!string.IsNullOrEmpty(frameworkRuntimePair.RuntimeIdentifier))
            {
                // We start with the non-RID TFM graph.
                // This is guaranteed to be computed before any graph with a RID, so we can assume this will return a value.
                tfmNonRidGraph = restoreTargetGraphsByFramework[frameworkRuntimePair.Framework];

                // PCL Projects with Supports have a runtime graph but no matching framework.
                string? runtimeGraphPath = _restoreRequest.Project.TargetFrameworks.FirstOrDefault(e => NuGetFramework.Comparer.Equals(e.FrameworkName, tfmNonRidGraph.Framework))?.RuntimeIdentifierGraphPath;

                RuntimeGraph? projectProviderRuntimeGraph = default;
                if (!string.IsNullOrWhiteSpace(runtimeGraphPath))
                {
                    projectProviderRuntimeGraph = ProjectRestoreCommand.GetRuntimeGraph(runtimeGraphPath, _logger);
                }

                runtimeGraph = ProjectRestoreCommand.GetRuntimeGraph(tfmNonRidGraph, localRepositories, projectRuntimeGraph: projectProviderRuntimeGraph, _logger);
                allRuntimes = RuntimeGraph.Merge(allRuntimes, runtimeGraph);
            }

            restoreTargetGraph.Conventions = new Client.ManagedCodeConventions(runtimeGraph);

            return (restoreTargetGraph, runtimeGraph);
        }

        private bool FlattenResolvedItems(LibraryDependency initialProject, DependencyGraphItem projectDependencyGraphItem, Dictionary<LibraryDependencyIndex, ResolvedItem> resolvedItems, Dictionary<LibraryRangeIndex, FindLibraryCachedAsyncResult> allResolvedItems, RemoteWalkContext context, RestoreTargetGraph restoreTargetGraph)
        {
            bool success = true;

            // Now that we've completed import, figure out the short real flattened list
            HashSet<GraphItem<RemoteResolveResult>> uniqueNodes = new();
            HashSet<LibraryDependencyIndex> visitedItems = new HashSet<LibraryDependencyIndex>();
            Queue<(LibraryDependencyIndex, GraphNode<RemoteResolveResult>)> itemsToFlatten = new Queue<(LibraryDependencyIndex, GraphNode<RemoteResolveResult>)>();
            List<GraphNode<RemoteResolveResult>> graphNodes = new();

            ResolvedItem projectResolvedItem = resolvedItems[projectDependencyGraphItem.LibraryDependencyIndex];

            var rootGraphNode = new GraphNode<RemoteResolveResult>(projectResolvedItem.LibraryDependency.LibraryRange);
            rootGraphNode.Item = allResolvedItems[projectResolvedItem.LibraryRangeIndex].Item;
            graphNodes.Add(rootGraphNode);

            var nodesById = new Dictionary<LibraryRangeIndex, GraphNode<RemoteResolveResult>>();

            var downgrades = new Dictionary<LibraryRangeIndex, (GraphNode<RemoteResolveResult> OuterNode, LibraryDependency LibraryDependency)>();

            var versionConflicts = new Dictionary<LibraryRangeIndex, GraphNode<RemoteResolveResult>>();

            var pins = new Dictionary<LibraryRangeIndex, List<GraphNode<RemoteResolveResult>>>();

            if (_restoreRequest.Project.RestoreMetadata.CentralPackageTransitivePinningEnabled)
            {
                foreach (var chosenResolvedItem in resolvedItems)
                {
                    if (chosenResolvedItem.Value.LibraryDependency.ReferenceType == LibraryDependencyReferenceType.None && chosenResolvedItem.Value.LibraryDependency.VersionCentrallyManaged && !pins.ContainsKey(chosenResolvedItem.Value.LibraryRangeIndex))
                    {
                        pins.Add(chosenResolvedItem.Value.LibraryRangeIndex, new List<GraphNode<RemoteResolveResult>>());
                    }
                }
            }

            itemsToFlatten.Enqueue((projectDependencyGraphItem.LibraryDependencyIndex, rootGraphNode));
            while (itemsToFlatten.Count > 0)
            {
                (LibraryDependencyIndex currentDependencyIndex, GraphNode<RemoteResolveResult> currentGraphNode) = itemsToFlatten.Dequeue();

                if (!resolvedItems.TryGetValue(currentDependencyIndex, out ResolvedItem foundItem))
                {
                    continue;
                }

                //(LibraryDependency chosenRef, LibraryRangeIndex chosenRefRangeIndex, LibraryRangeIndex[] pathToChosenRef, bool directPackageReferenceFromRootProject, var chosenSuppressions) = foundItem;

                if (allResolvedItems.TryGetValue(foundItem.LibraryRangeIndex, out FindLibraryCachedAsyncResult node))
                {
                    uniqueNodes.Add(node.Item);

                    // If the package came from a remote library provider, it needs to be installed locally.
                    if (context.RemoteLibraryProviders.Contains(node.Item.Data.Match.Provider))
                    {
                        restoreTargetGraph.Install.Add(node.Item.Data.Match);
                    }

                    for (int i = 0; i < node.Item.Data.Dependencies.Count; i++)
                    {
                        LibraryDependency libraryDependency = node.Item.Data.Dependencies[i];

                        if (StringComparer.OrdinalIgnoreCase.Equals(libraryDependency.Name, node.Item.Key.Name) || StringComparer.OrdinalIgnoreCase.Equals(libraryDependency.Name, rootGraphNode.Key.Name))
                        {
                            // Cycle
                            var nodeWithCycle = new GraphNode<RemoteResolveResult>(libraryDependency.LibraryRange)
                            {
                                OuterNode = currentGraphNode,
                                Disposition = Disposition.Cycle
                            };

                            restoreTargetGraph.AnalyzeResult.Cycles.Add(nodeWithCycle);

                            continue;
                        }

                        LibraryDependencyIndex depIndex = node.GetDependencyIndexForDependency(i);

                        if (!resolvedItems.TryGetValue(depIndex, out ResolvedItem resolvedItem))
                        {
                            continue;
                        }

                        LibraryRangeIndex chosenItemRangeIndex = resolvedItem.LibraryRangeIndex;
                        LibraryDependency actualDep = resolvedItem.LibraryDependency;

                        if ((currentGraphNode.Key.TypeConstraint == LibraryDependencyTarget.Package || currentGraphNode.Key.TypeConstraint == LibraryDependencyTarget.PackageProjectExternal) && pins.TryGetValue(chosenItemRangeIndex, out List<GraphNode<RemoteResolveResult>>? parents))
                        {
                            parents.Add(currentGraphNode);
                        }

                        if (!visitedItems.Add(depIndex))
                        {
                            LibraryRangeIndex currentRangeIndex = node.GetRangeIndexForDependency(i);

                            if (foundItem.Path.Contains(currentRangeIndex))
                            {
                                // Cycle
                                var nodeWithCycle = new GraphNode<RemoteResolveResult>(libraryDependency.LibraryRange);
                                nodeWithCycle.OuterNode = currentGraphNode;
                                nodeWithCycle.Disposition = Disposition.Cycle;
                                restoreTargetGraph.AnalyzeResult.Cycles.Add(nodeWithCycle);

                                continue;
                            }

                            if (!RemoteDependencyWalker.IsGreaterThanOrEqualTo(actualDep.LibraryRange.VersionRange, libraryDependency.LibraryRange.VersionRange))
                            {
                                if (node.DependencyIndex != projectDependencyGraphItem.LibraryDependencyIndex && libraryDependency.SuppressParent == LibraryIncludeFlags.All)
                                {
                                    continue;
                                }

                                if (foundItem.SuppressionsAndOverrides.Count > 0 && foundItem.SuppressionsAndOverrides[0].Suppressions.Contains(depIndex))
                                {
                                    continue;
                                }

                                // Downgrade
                                if (!downgrades.ContainsKey(chosenItemRangeIndex))
                                {
                                    downgrades.Add(chosenItemRangeIndex, (currentGraphNode, libraryDependency));
                                }

                                continue;
                            }

                            if (versionConflicts.ContainsKey(chosenItemRangeIndex) && !nodesById.ContainsKey(currentRangeIndex))
                            {
                                // Version conflict
                                var selectedConflictingNode = new GraphNode<RemoteResolveResult>(actualDep.LibraryRange)
                                {
                                    Item = allResolvedItems[chosenItemRangeIndex].Item,
                                    Disposition = Disposition.Acceptable,
                                    OuterNode = currentGraphNode,
                                };
                                currentGraphNode.InnerNodes.Add(selectedConflictingNode);

                                nodesById.Add(currentRangeIndex, selectedConflictingNode);

                                continue;
                            }

                            continue;
                        }

                        var newGraphNode = new GraphNode<RemoteResolveResult>(actualDep.LibraryRange);
                        newGraphNode.Item = allResolvedItems[chosenItemRangeIndex].Item;
                        currentGraphNode.InnerNodes.Add(newGraphNode);
                        newGraphNode.OuterNode = currentGraphNode;

                        if (_restoreRequest.Project.RestoreMetadata.CentralPackageTransitivePinningEnabled && actualDep.ReferenceType == LibraryDependencyReferenceType.None && actualDep.VersionCentrallyManaged == true)
                        {
                            newGraphNode.Item.IsCentralTransitive = true;
                        }

                        if (newGraphNode.Item.Key.Type != LibraryType.Project && !versionConflicts.ContainsKey(chosenItemRangeIndex) && libraryDependency.SuppressParent != LibraryIncludeFlags.All && !libraryDependency.LibraryRange.VersionRange!.Satisfies(newGraphNode.Item.Key.Version))
                        {
                            currentGraphNode.InnerNodes.Remove(newGraphNode);

                            // Conflict
                            var conflictingNode = new GraphNode<RemoteResolveResult>(libraryDependency.LibraryRange)
                            {
                                Disposition = Disposition.Acceptable
                            };

                            conflictingNode.Item = new GraphItem<RemoteResolveResult>(new LibraryIdentity(libraryDependency.Name, libraryDependency.LibraryRange.VersionRange?.MinVersion!, LibraryType.Package));
                            currentGraphNode.InnerNodes.Add(conflictingNode);
                            conflictingNode.OuterNode = currentGraphNode;

                            versionConflicts.Add(chosenItemRangeIndex, conflictingNode);

                            continue;
                        }

                        newGraphNode.Disposition = Disposition.Accepted;

                        nodesById.Add(chosenItemRangeIndex, newGraphNode);
                        itemsToFlatten.Enqueue((depIndex, newGraphNode));

                        if (newGraphNode.Item.Key.Type == LibraryType.Unresolved)
                        {
                            restoreTargetGraph.Unresolved.Add(actualDep.LibraryRange);

                            success = false;

                            continue;
                        }

                        restoreTargetGraph.ResolvedDependencies.Add(new ResolvedDependencyKey(
                            parent: newGraphNode.OuterNode.Item.Key,
                            range: newGraphNode.Key.VersionRange,
                            child: newGraphNode.Item.Key));
                    }
                }
            }

            if (pins.Count > 0)
            {
                foreach (var parents in pins)
                {
                    if (nodesById.TryGetValue(parents.Key, out var node))
                    {
                        node.ParentNodes.AddRange(parents.Value);
                    }
                }
            }

            if (versionConflicts.Count > 0)
            {
                foreach (var versionConflict in versionConflicts)
                {
                    if (nodesById.TryGetValue(versionConflict.Key, out var selected))
                    {
                        restoreTargetGraph.AnalyzeResult.VersionConflicts.Add(new VersionConflictResult<RemoteResolveResult>
                        {
                            Conflicting = versionConflict.Value,
                            Selected = selected
                        });
                    }
                }
            }

            if (downgrades.Count > 0)
            {
                foreach (var downgrade in downgrades)
                {
                    if (!nodesById.TryGetValue(downgrade.Key, out var downgradedTo))
                    {
                        continue;
                    }

                    var downgradedFrom = new GraphNode<RemoteResolveResult>(downgrade.Value.LibraryDependency.LibraryRange)
                    {
                        Item = new GraphItem<RemoteResolveResult>(new LibraryIdentity(downgrade.Value.LibraryDependency.Name, downgrade.Value.LibraryDependency.LibraryRange.VersionRange?.MinVersion!, LibraryType.Package)),
                        OuterNode = downgrade.Value.OuterNode
                    };

                    restoreTargetGraph.AnalyzeResult.Downgrades.Add(new DowngradeResult<RemoteResolveResult>
                    {
                        DowngradedFrom = downgradedFrom,
                        DowngradedTo = downgradedTo
                    });
                }
            }

            restoreTargetGraph.Flattened = uniqueNodes;

            restoreTargetGraph.Graphs = graphNodes;

            return success;
        }

        private bool ResolveItems(LibraryDependency initialProject, DependencyGraphItem projectDependencyGraphItem, Dictionary<LibraryDependencyIndex, ResolvedItem> resolvedItems, Dictionary<LibraryRangeIndex, FindLibraryCachedAsyncResult> findLibraryCache, FrameworkRuntimePair frameworkRuntimePair, RestoreTargetGraph restoreTargetGraph, RuntimeGraph? runtimeGraph, RemoteWalkContext context, ProjectRestoreCommand projectRestoreCommand, NuGetv3LocalRepository userPackageFolder, LibraryRangeInterningTable libraryRangeInterningTable, LibraryDependencyInterningTable libraryDependencyInterningTable, CancellationToken cancellationToken)
        {
            int maxOutstandingRefs = 0;
            int totalLookups = 0;
            int totalDeepLookups = 0;
            int totalEvictions = 0;
            int totalHardEvictions = 0;

            Queue<DependencyGraphItem> dependencyGraphItems = new Queue<DependencyGraphItem>(4096);

            Dictionary<LibraryRangeIndex, (LibraryRangeIndex[], LibraryDependencyIndex, LibraryDependencyTarget)> evictions = new(1024);

            Dictionary<LibraryDependencyIndex, LibraryDependency>? pinnedPackages = default;

        ProcessDeepEviction:

            dependencyGraphItems.Clear();
            resolvedItems.Clear();

            dependencyGraphItems.Enqueue(projectDependencyGraphItem);

            while (dependencyGraphItems.Count > 0)
            {
                maxOutstandingRefs = Math.Max(dependencyGraphItems.Count, maxOutstandingRefs);

                DependencyGraphItem dependencyGraphItem = dependencyGraphItems.Dequeue();
                LibraryDependency libraryDependency = dependencyGraphItem.LibraryDependency;

                LibraryDependencyTarget typeConstraint = dependencyGraphItem.LibraryDependency.LibraryRange.TypeConstraint;
                bool isProject = ((typeConstraint == LibraryDependencyTarget.Project) ||
                                  (typeConstraint == (LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject)));

                if (evictions.TryGetValue(dependencyGraphItem.LibraryRangeIndex, out (LibraryRangeIndex[] Path, LibraryDependencyIndex LibraryDependencyIndex, LibraryDependencyTarget LibraryDependencyTarget) eviction))
                {
                    // If we evicted this same version previously, but the type constraint of currentRef is more stringent (package), then do not skip the current item - this is the one we want.
                    if (!((eviction.LibraryDependencyTarget == LibraryDependencyTarget.PackageProjectExternal || eviction.LibraryDependencyTarget == LibraryDependencyTarget.ExternalProject) &&
                        dependencyGraphItem.LibraryDependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package))
                    {
                        continue;
                    }
                }

                if (dependencyGraphItem.VersionOverrides != null && dependencyGraphItem.VersionOverrides.TryGetValue(dependencyGraphItem.LibraryDependencyIndex, out VersionRange? versionOverride))
                {
                    if (!versionOverride.Equals(dependencyGraphItem.LibraryDependency.LibraryRange.VersionRange))
                    {
                        continue;
                    }
                }

                if (pinnedPackages != null && pinnedPackages.TryGetValue(dependencyGraphItem.LibraryDependencyIndex, out LibraryDependency? pinnedLibraryDependency))
                {
                    libraryDependency = pinnedLibraryDependency;
                }

                // else if we've seen this ref (but maybe not version) before check to see if we need to upgrade
                if (resolvedItems.TryGetValue(dependencyGraphItem.LibraryDependencyIndex, out ResolvedItem resolvedItem))
                {
                    // List<(HashSet<LibraryDependencyIndex> dependencyGraphItem.Suppressions, IReadOnlyDictionary<LibraryDependencyIndex, VersionRange> dependencyGraphItem.VersionOverrides)> chosenSuppressions) = chosenResolvedItem;

                    if (resolvedItem.DirectPackageReferenceFromRootProject)
                    {
                        continue;
                    }

                    // We should evict on type constraint if the type constraint of the current ref is more stringent than the chosen ref.
                    // This happens when a previous type constraint is broader (e.g. PackageProjectExternal) than the current type constraint (e.g. Package).
                    bool evictOnTypeConstraint = false;
                    if ((resolvedItem.LibraryRangeIndex == dependencyGraphItem.LibraryRangeIndex) &&
                        EvictOnTypeConstraint(dependencyGraphItem.LibraryDependency.LibraryRange.TypeConstraint, resolvedItem.LibraryDependency.LibraryRange.TypeConstraint))
                    {
                        if (findLibraryCache.TryGetValue(resolvedItem.LibraryRangeIndex, out FindLibraryCachedAsyncResult evicteeFindLibraryCachedAsyncResult) && evicteeFindLibraryCachedAsyncResult.Item.Key.Type == LibraryType.Project)
                        {
                            // We need to evict the chosen item because this one has a more stringent type constraint.
                            evictOnTypeConstraint = true;
                        }
                    }

                    VersionRange nvr = dependencyGraphItem.LibraryDependency.LibraryRange.VersionRange ?? VersionRange.All;
                    VersionRange ovr = resolvedItem.LibraryDependency.LibraryRange.VersionRange ?? VersionRange.All;

                    if (evictOnTypeConstraint || !RemoteDependencyWalker.IsGreaterThanOrEqualTo(ovr, nvr))
                    {
                        if (resolvedItem.LibraryDependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package && dependencyGraphItem.LibraryDependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.PackageProjectExternal)
                        {
                            bool commonAncestry = true;

                            for (int i = 0; i < dependencyGraphItem.Path.Length && i < resolvedItem.Path.Length; i++)
                            {
                                if (dependencyGraphItem.Path[i] != resolvedItem.Path[i])
                                {
                                    commonAncestry = false;
                                    break;
                                }
                            }

                            if (commonAncestry)
                            {
                                continue;
                            }
                        }

                        // If we think the newer thing we are looking at is better, remove the old one and let it fall thru.
                        resolvedItems.Remove(dependencyGraphItem.LibraryDependencyIndex);
                        // Record an eviction for the node we are replacing.  The eviction path is for the current node.
                        LibraryRangeIndex evictedLibraryRangeIndex = resolvedItem.LibraryRangeIndex;

                        // If we're evicting on type constraint, then there is already an item in allResolvedItems that matches the old type constraint.
                        // We must remove it, otherwise we won't call FindLibraryCachedAsync again to load the correct item and save it into allResolvedItems.
                        if (evictOnTypeConstraint)
                        {
                            findLibraryCache.Remove(evictedLibraryRangeIndex);
                        }

                        int deepEvictions = 0;
                        // unwind anything chosen by the node we're evicting..
                        HashSet<LibraryRangeIndex>? evicteesToRemove = default;
                        foreach (KeyValuePair<LibraryRangeIndex, (LibraryRangeIndex[], LibraryDependencyIndex, LibraryDependencyTarget)> evictee in evictions)
                        {
                            (LibraryRangeIndex[] evicteePath, LibraryDependencyIndex evicteeDepIndex, LibraryDependencyTarget evicteeTypeConstraint) = evictee.Value;

                            if (evicteePath.Contains(evictedLibraryRangeIndex))
                            {
                                // if evictee.Key (depIndex) == currentDepIndex && evictee.TypeConstraint == ExternalProject --> Don't remove it.  It must remain evicted.
                                // If the evictee to remove is the same dependency, but the project version of said dependency, then do not remove it - it must remain evicted in favor of the package.
                                if (!(evicteeDepIndex == dependencyGraphItem.LibraryDependencyIndex &&
                                    (evicteeTypeConstraint == LibraryDependencyTarget.ExternalProject || evicteeTypeConstraint == LibraryDependencyTarget.PackageProjectExternal)))
                                {
                                    if (evicteesToRemove == null)
                                        evicteesToRemove = new HashSet<LibraryRangeIndex>();
                                    evicteesToRemove.Add(evictee.Key);
                                }
                            }
                        }
                        if (evicteesToRemove != null)
                        {
                            foreach (var evicteeToRemove in evicteesToRemove)
                            {
                                evictions.Remove(evicteeToRemove);
                                deepEvictions++;
                            }
                        }
                        foreach (KeyValuePair<LibraryDependencyIndex, ResolvedItem> chosenItem in resolvedItems)
                        {
                            if (chosenItem.Value.Path.Contains(evictedLibraryRangeIndex))
                            {
                                deepEvictions++;
                                break;
                            }
                        }
                        evictions[evictedLibraryRangeIndex] = (DependencyGraphItem.CreatePathToRef(dependencyGraphItem.Path, dependencyGraphItem.LibraryRangeIndex), dependencyGraphItem.LibraryDependencyIndex, resolvedItem.LibraryDependency.LibraryRange.TypeConstraint);
                        totalEvictions++;
                        if (deepEvictions > 0)
                        {
                            totalHardEvictions++;
                            goto ProcessDeepEviction;
                        }
                        // Since this is a "new" choice, its gets a new import context list
                        //resolvedItems.Add(dependencyGraphItem.LibraryDependencyIndex, (libraryDependency, dependencyGraphItem.LibraryRangeIndex, dependencyGraphItem.Path, packageReferenceFromRootProject,
                        //    new List<(HashSet<LibraryDependencyIndex>, IReadOnlyDictionary<LibraryDependencyIndex, VersionRange>)> { (dependencyGraphItem.Suppressions, dependencyGraphItem.VersionOverrides) }));

                        resolvedItems.Add(dependencyGraphItem.LibraryDependencyIndex, new ResolvedItem(dependencyGraphItem, libraryDependency));

                        // if we are going to live with this queue and chosen state, we need to also kick
                        // any queue members who were descendants of the thing we just evicted.
                        Queue<DependencyGraphItem> newDependencyGraphItems = new(capacity: 4096);

                        while (dependencyGraphItems.Count > 0)
                        {
                            DependencyGraphItem item = dependencyGraphItems.Dequeue();

                            if (!item.Path.Contains(evictedLibraryRangeIndex))
                            {
                                newDependencyGraphItems.Enqueue(item);
                            }
                        }

                        dependencyGraphItems = newDependencyGraphItems;
                    }
                    // if its lower we'll never do anything other than skip it.
                    else if (!VersionRange.PreciseEquals(ovr, nvr))
                    {
                        continue;
                    }
                    else
                    // we are looking at same.  consider if its an upgrade.
                    {
                        // If the one we already have chosen is pure, then we can skip this one.  Processing it wont bring any new info
                        if (resolvedItem.SuppressionsAndOverrides != null && (resolvedItem.SuppressionsAndOverrides.Count == 1) && (resolvedItem.SuppressionsAndOverrides[0].Suppressions.Count == 0) &&
                            (resolvedItem.SuppressionsAndOverrides[0].VersionOverrides.Count == 0))
                        {
                            continue;
                        }
                        // if the one we are now looking at is pure, then we should replace the one we have chosen because if we're here it isn't pure.
                        else if ((dependencyGraphItem.Suppressions.Count == 0) && (dependencyGraphItem.VersionOverrides!.Count == 0))
                        {
                            resolvedItems.Remove(dependencyGraphItem.LibraryDependencyIndex);
                            // slightly evil, but works.. we should just shift to the current thing as ref?
                            resolvedItems.Add(dependencyGraphItem.LibraryDependencyIndex, new ResolvedItem(dependencyGraphItem, libraryDependency));
                        }
                        else
                        // check to see if we are equal to one of the dispositions or if we are less restrictive than one
                        {
                            bool isEqualOrSuperSetDisposition = false;

                            if (resolvedItem.SuppressionsAndOverrides != null)
                            {
                                foreach (SuppressionsAndVersionOverrides suppressionsAndOverrides in resolvedItem.SuppressionsAndOverrides)
                                {
                                    bool localIsEqualOrSuperSetDisposition = dependencyGraphItem.Suppressions.IsSupersetOf(suppressionsAndOverrides.Suppressions);

                                    bool localIsEqualorSuperSetOverride = dependencyGraphItem.VersionOverrides!.Count >= suppressionsAndOverrides.VersionOverrides.Count;

                                    if (localIsEqualorSuperSetOverride)
                                    {
                                        foreach (var chosenOverride in suppressionsAndOverrides.VersionOverrides)
                                        {
                                            if (!dependencyGraphItem.VersionOverrides.TryGetValue(chosenOverride.Key, out VersionRange? currentOverride))
                                            {
                                                localIsEqualorSuperSetOverride = false;
                                                break;
                                            }

                                            if (!VersionRange.PreciseEquals(currentOverride, chosenOverride.Value))
                                            {
                                                localIsEqualorSuperSetOverride = false;
                                                break;
                                            }
                                        }
                                    }

                                    isEqualOrSuperSetDisposition = localIsEqualOrSuperSetDisposition && localIsEqualorSuperSetOverride;
                                }
                            }

                            if (isEqualOrSuperSetDisposition)
                            {
                                continue;
                            }
                            else
                            {
                                // case of differently restrictive dispositions or less restrictive... we should technically be able to remove
                                // a disposition if its less restrictive than another.  But we'll just add it to the list.
                                resolvedItems.Remove(dependencyGraphItem.LibraryDependencyIndex);

                                List<SuppressionsAndVersionOverrides> newSuppressionsAndOverrides =
                                    new List<SuppressionsAndVersionOverrides>
                                    {
                                        new SuppressionsAndVersionOverrides
                                        {
                                            Suppressions = dependencyGraphItem.Suppressions,
                                            VersionOverrides = dependencyGraphItem.VersionOverrides!
                                        }
                                    };

                                if (resolvedItem.SuppressionsAndOverrides != null)
                                {
                                    newSuppressionsAndOverrides.AddRange(resolvedItem.SuppressionsAndOverrides);
                                }

                                // slightly evil, but works.. we should just shift to the current thing as ref?
                                //resolvedItems.Add(dependencyGraphItem.LibraryDependencyIndex, (libraryDependency, dependencyGraphItem.LibraryRangeIndex, dependencyGraphItem.Path, packageReferenceFromRootProject, newSuppressionsAndOverrides));
                                resolvedItems.Add(dependencyGraphItem.LibraryDependencyIndex, new ResolvedItem(dependencyGraphItem, libraryDependency));
                            }
                        }
                    }
                }
                else
                {
                    // This is now the thing we think is the highest version of this ref
                    resolvedItems.Add(dependencyGraphItem.LibraryDependencyIndex, new ResolvedItem(dependencyGraphItem, libraryDependency));
                }

                if (!findLibraryCache.TryGetValue(dependencyGraphItem.LibraryRangeIndex, out FindLibraryCachedAsyncResult findLibraryCachedAsyncResult))
                {
                    GraphItem<RemoteResolveResult> graphItem;
                    try
                    {
                        graphItem = ResolverUtility.FindLibraryCachedAsync(
                            dependencyGraphItem.LibraryDependency.LibraryRange,
                            restoreTargetGraph.Framework,
                            restoreTargetGraph.RuntimeIdentifier,
                            context,
                            CancellationToken.None).GetAwaiter().GetResult();
                    }
                    catch (FatalProtocolException)
                    {
                        return false;
                    }

                    totalDeepLookups++;

                    findLibraryCachedAsyncResult = new FindLibraryCachedAsyncResult(
                        libraryDependency,
                        graphItem,
                        dependencyGraphItem.LibraryDependencyIndex,
                        dependencyGraphItem.LibraryRangeIndex,
                        libraryDependencyInterningTable,
                        libraryRangeInterningTable);

                    findLibraryCache.Add(dependencyGraphItem.LibraryRangeIndex, findLibraryCachedAsyncResult);
                }

                totalLookups++;

                HashSet<LibraryDependencyIndex>? suppressions = default;
                Dictionary<LibraryDependencyIndex, VersionRange>? finalVersionOverrides = default;
                Dictionary<LibraryDependencyIndex, VersionRange>? newOverrides = default;

                // Scan for suppressions and overrides
                for (int i = 0; i < findLibraryCachedAsyncResult.Item.Data.Dependencies.Count; i++)
                {
                    LibraryDependency dep = findLibraryCachedAsyncResult.Item.Data.Dependencies[i];
                    LibraryDependencyIndex depIndex = findLibraryCachedAsyncResult.GetDependencyIndexForDependency(i);
                    if ((dep.SuppressParent == LibraryIncludeFlags.All) && (isProject == false))
                    {
                        if (suppressions == null)
                        {
                            suppressions = new HashSet<LibraryDependencyIndex>();
                        }

                        suppressions.Add(depIndex);
                    }
                    if (dep.VersionOverride != null)
                    {
                        if (newOverrides == null)
                        {
                            newOverrides = new Dictionary<LibraryDependencyIndex, VersionRange>(capacity: 1024);
                        }

                        newOverrides[depIndex] = dep.VersionOverride;
                    }

                    if (_restoreRequest.Project.RestoreMetadata.CentralPackageTransitivePinningEnabled && dep.ReferenceType == LibraryDependencyReferenceType.None && dep.VersionCentrallyManaged == true)
                    {
                        if (pinnedPackages == null)
                        {
                            pinnedPackages = new Dictionary<LibraryDependencyIndex, LibraryDependency>(findLibraryCachedAsyncResult.Item.Data.Dependencies.Count);
                        }

                        pinnedPackages[depIndex] = dep;
                    }
                }

                // If the override set has been mutated, then add the rest of the overrides.
                // Otherwise, just use the incoming set of overrides.
                if (newOverrides != null)
                {
                    Dictionary<LibraryDependencyIndex, VersionRange> allOverrides = new(dependencyGraphItem.VersionOverrides!.Count + newOverrides.Count);
                    allOverrides.AddRange(dependencyGraphItem.VersionOverrides);
                    foreach (var overridePair in newOverrides)
                    {
                        allOverrides[overridePair.Key] = overridePair.Value;
                    }
                    finalVersionOverrides = allOverrides;
                }
                else
                {
                    finalVersionOverrides = dependencyGraphItem.VersionOverrides!;
                }

                // If the suppressions have been mutated, then add the rest of the suppressions.
                // Otherwise just use teh incoming set of suppressions.
                if (suppressions != null)
                {
                    suppressions.AddRange(dependencyGraphItem.Suppressions);
                }
                else
                {
                    suppressions = dependencyGraphItem.Suppressions;
                }

                for (int i = 0; i < findLibraryCachedAsyncResult.Item.Data.Dependencies.Count; i++)
                {
                    LibraryDependency dep = findLibraryCachedAsyncResult.Item.Data.Dependencies[i];

                    if (_restoreRequest.Project.RestoreMetadata.CentralPackageTransitivePinningEnabled && dep.ReferenceType == LibraryDependencyReferenceType.None && dep.VersionCentrallyManaged == true)
                    {
                        // Skip pinned transitive dependencies since they are always referenced by the project even if they weren't used
                        continue;
                    }

                    LibraryDependencyIndex depIndex = findLibraryCachedAsyncResult.GetDependencyIndexForDependency(i);

                    // Suppress this node
                    if (suppressions != null && suppressions.Contains(depIndex))
                    {
                        continue;
                    }

                    dependencyGraphItems.Enqueue(new DependencyGraphItem()
                    {
                        LibraryDependency = dep,
                        LibraryDependencyIndex = depIndex,
                        LibraryRangeIndex = findLibraryCachedAsyncResult.GetRangeIndexForDependency(i),
                        Path = DependencyGraphItem.CreatePathToRef(dependencyGraphItem.Path, dependencyGraphItem.LibraryRangeIndex),
                        Suppressions = suppressions ?? DependencyGraphItem.EmptySuppressions,
                        VersionOverrides = finalVersionOverrides ?? DependencyGraphItem.EmptyVersionOverrides,
                        DirectPackageReferenceFromRootProject = (dependencyGraphItem.LibraryRangeIndex == projectDependencyGraphItem.LibraryRangeIndex) && (dep.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package),
                    });
                }

                // Add runtime dependencies of the current node if a runtime identifier has been specified.
                if (!string.IsNullOrEmpty(frameworkRuntimePair.RuntimeIdentifier))
                {
                    // Check for runtime dependencies.
                    HashSet<LibraryDependency> runtimeDependencies = new HashSet<LibraryDependency>();
                    LibraryRange libraryRange = dependencyGraphItem.LibraryDependency.LibraryRange;

                    RemoteDependencyWalker.EvaluateRuntimeDependencies(ref libraryRange, frameworkRuntimePair.RuntimeIdentifier, runtimeGraph, ref runtimeDependencies);

                    if (runtimeDependencies.Count > 0)
                    {
                        // Runtime dependencies start after non-runtime dependencies.
                        // Keep track of the first index for any runtime dependencies so that it can be used to enqueue later.
                        int runtimeDependencyIndex = findLibraryCachedAsyncResult.Item.Data.Dependencies.Count;

                        // If there are runtime dependencies that need to be added, remove the currentRef from allResolvedItems,
                        // and add the newly created version that contains the previously detected dependencies and newly detected runtime dependencies.
                        findLibraryCache.Remove(dependencyGraphItem.LibraryRangeIndex);
                        bool rootHasInnerNodes = (findLibraryCachedAsyncResult.Item.Data.Dependencies.Count + (runtimeDependencies == null ? 0 : runtimeDependencies.Count)) > 0;
                        GraphNode<RemoteResolveResult> rootNode = GraphNode<RemoteResolveResult>.Create(libraryRange, rootHasInnerNodes, hasParentNodes: false, findLibraryCachedAsyncResult.Item);
                        RemoteDependencyWalker.MergeRuntimeDependencies(runtimeDependencies, rootNode);
                        findLibraryCachedAsyncResult = new FindLibraryCachedAsyncResult(
                            libraryDependency,
                            rootNode.Item,
                            dependencyGraphItem.LibraryDependencyIndex,
                            dependencyGraphItem.LibraryRangeIndex,
                            libraryDependencyInterningTable,
                            libraryRangeInterningTable);
                        findLibraryCache.Add(dependencyGraphItem.LibraryRangeIndex, findLibraryCachedAsyncResult);

                        // Enqueue each of the runtime dependencies, but only if they weren't already present in refItemResult before merging the runtime dependencies above.
                        if (runtimeDependencies != null && (rootNode.Item.Data.Dependencies.Count - runtimeDependencyIndex) == runtimeDependencies.Count)
                        {
                            foreach (var dep in runtimeDependencies)
                            {
                                dependencyGraphItems.Enqueue(new DependencyGraphItem()
                                {
                                    LibraryDependency = dep,
                                    LibraryDependencyIndex = findLibraryCachedAsyncResult.GetDependencyIndexForDependency(runtimeDependencyIndex),
                                    LibraryRangeIndex = findLibraryCachedAsyncResult.GetRangeIndexForDependency(runtimeDependencyIndex),
                                    Path = DependencyGraphItem.CreatePathToRef(dependencyGraphItem.Path, dependencyGraphItem.LibraryRangeIndex),
                                    Suppressions = suppressions ?? DependencyGraphItem.EmptySuppressions,
                                    VersionOverrides = finalVersionOverrides ?? DependencyGraphItem.EmptyVersionOverrides,
                                    DirectPackageReferenceFromRootProject = false,
                                });

                                runtimeDependencyIndex++;
                            }
                        }
                    }
                }
            }

            return true;
        }

        [DebuggerDisplay("{LibraryDependency},nq")]
        private struct DependencyGraphItem
        {
            public static readonly HashSet<LibraryDependencyIndex> EmptySuppressions = new();

            public static readonly Dictionary<LibraryDependencyIndex, VersionRange> EmptyVersionOverrides = new(capacity: 0);

            public DependencyGraphItem(LibraryDependency libraryDependency)
            {
                LibraryDependency = libraryDependency;
                Path = Array.Empty<LibraryRangeIndex>();
            }

            public DependencyGraphItem(LibraryDependency libraryDependency, HashSet<LibraryDependencyIndex> suppressions, Dictionary<LibraryDependencyIndex, VersionRange> versionOverrides)
            {
                LibraryDependency = libraryDependency;
                Suppressions = suppressions;
                VersionOverrides = versionOverrides;
            }

            public bool DirectPackageReferenceFromRootProject { get; set; }

            public LibraryDependency LibraryDependency { get; set; }

            public LibraryDependencyIndex LibraryDependencyIndex { get; set; } = LibraryDependencyIndex.Invalid;

            public LibraryRangeIndex LibraryRangeIndex { get; set; } = LibraryRangeIndex.Invalid;

            public LibraryRangeIndex[] Path { get; set; } = Array.Empty<LibraryRangeIndex>();

            public required HashSet<LibraryDependencyIndex> Suppressions { get; set; }

            public required Dictionary<LibraryDependencyIndex, VersionRange> VersionOverrides { get; set; }

            internal static LibraryRangeIndex[] CreatePathToRef(LibraryRangeIndex[] existingPath, LibraryRangeIndex currentRef)
            {
                LibraryRangeIndex[] newPath = new LibraryRangeIndex[existingPath.Length + 1];

                Array.Copy(existingPath, newPath, existingPath.Length);

                newPath[newPath.Length - 1] = currentRef;

                return newPath;
            }
        }

        private struct FindLibraryCachedAsyncResult
        {
            private LibraryDependencyIndex[] _dependencyIndices;
            private LibraryRangeIndex[] _rangeIndices;

            public FindLibraryCachedAsyncResult(LibraryDependency libraryDependency, GraphItem<RemoteResolveResult> resolvedItem, LibraryDependencyIndex itemDependencyIndex, LibraryRangeIndex itemRangeIndex, LibraryDependencyInterningTable libraryDependencyInterningTable, LibraryRangeInterningTable libraryRangeInterningTable)
            {
                Item = resolvedItem;
                DependencyIndex = itemDependencyIndex;
                RangeIndex = itemRangeIndex;
                int dependencyCount = resolvedItem.Data.Dependencies.Count;
                _dependencyIndices = new LibraryDependencyIndex[dependencyCount];
                _rangeIndices = new LibraryRangeIndex[dependencyCount];

                for (int i = 0; i < dependencyCount; i++)
                {
                    LibraryDependency dependency = resolvedItem.Data.Dependencies[i];
                    _dependencyIndices[i] = libraryDependencyInterningTable.Intern(dependency);
                    _rangeIndices[i] = libraryRangeInterningTable.Intern(dependency.LibraryRange);
                }
            }

            public LibraryDependencyIndex DependencyIndex { get; }

            public GraphItem<RemoteResolveResult> Item { get; }

            public LibraryRangeIndex RangeIndex { get; }

            public LibraryDependencyIndex GetDependencyIndexForDependency(int dependencyIndex)
            {
                return _dependencyIndices[dependencyIndex];
            }

            public LibraryRangeIndex GetRangeIndexForDependency(int dependencyIndex)
            {
                return _rangeIndices[dependencyIndex];
            }
        }

        [DebuggerDisplay("{LibraryDependency}, RangeIndex={LibraryRangeIndex}, Direct={DirectPackageReferenceFromRootProject},nq")]
        private struct ResolvedItem
        {
            public static readonly List<SuppressionsAndVersionOverrides> EmptySuppressionsAndOverrides = new(capacity: 0);

            public ResolvedItem(DependencyGraphItem dependencyGraphItem, LibraryDependency libraryDependency)
                : this(dependencyGraphItem,
                      libraryDependency,
                      new List<SuppressionsAndVersionOverrides>
                      {
                          new SuppressionsAndVersionOverrides
                          {
                              Suppressions = dependencyGraphItem.Suppressions ?? DependencyGraphItem.EmptySuppressions,
                              VersionOverrides = dependencyGraphItem.VersionOverrides ?? DependencyGraphItem.EmptyVersionOverrides
                          }
                      })
            {
            }

            public ResolvedItem(DependencyGraphItem dependencyGraphItem, LibraryDependency libraryDependency, List<SuppressionsAndVersionOverrides> suppressionsAndVersionOverrides)
            {
                DirectPackageReferenceFromRootProject = dependencyGraphItem.DirectPackageReferenceFromRootProject;
                LibraryDependency = libraryDependency;
                LibraryRangeIndex = dependencyGraphItem.LibraryRangeIndex;
                Path = dependencyGraphItem.Path;
                SuppressionsAndOverrides = suppressionsAndVersionOverrides;
            }

            public bool DirectPackageReferenceFromRootProject { get; set; }

            public LibraryDependency LibraryDependency { get; set; }

            public LibraryRangeIndex LibraryRangeIndex { get; set; }

            public LibraryRangeIndex[] Path { get; set; } = Array.Empty<LibraryRangeIndex>();

            public List<SuppressionsAndVersionOverrides> SuppressionsAndOverrides { get; set; }
        }

        private struct SuppressionsAndVersionOverrides
        {
            public HashSet<LibraryDependencyIndex> Suppressions { get; set; }

            public Dictionary<LibraryDependencyIndex, VersionRange> VersionOverrides { get; set; }
        }

        private sealed class LibraryDependencyInterningTable
        {
            private readonly Dictionary<string, LibraryDependencyIndex> _table = new Dictionary<string, LibraryDependencyIndex>(StringComparer.OrdinalIgnoreCase);
            private int _nextIndex = 0;

            public LibraryDependencyIndex Intern(LibraryDependency libraryDependency)
            {
                string key = libraryDependency.Name;

                if (!_table.TryGetValue(key, out LibraryDependencyIndex index))
                {
                    index = (LibraryDependencyIndex)_nextIndex++;
                    _table.Add(key, index);
                }

                return index;
            }
        }

        private sealed class LibraryRangeInterningTable
        {
            private readonly Dictionary<string, LibraryRangeIndex> _table = new Dictionary<string, LibraryRangeIndex>(StringComparer.OrdinalIgnoreCase);
            private int _nextIndex = 0;

            public LibraryRangeIndex Intern(LibraryRange libraryRange)
            {
                string key = libraryRange.ToString();
                if (!_table.TryGetValue(key, out LibraryRangeIndex index))
                {
                    index = (LibraryRangeIndex)_nextIndex++;
                    _table.Add(key, index);
                }

                return index;
            }
        }
    }
}
