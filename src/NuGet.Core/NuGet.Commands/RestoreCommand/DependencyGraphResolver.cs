// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using NuGet.Common;
using NuGet.Repositories;
using NuGet.DependencyResolver;
using NuGet.ProjectModel;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NuGet.LibraryModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    /// <summary>
    /// Represents a class that can resolve a dependency graph.
    /// </summary>
    internal sealed class DependencyGraphResolver
    {
        private const int OverridesDictionarySize = 1024;
        private const int RefImportQueueSize = 4096;

        private readonly RestoreCollectorLogger _logger;
        private readonly Guid _operationId;
        private readonly RestoreRequest _request;
        private readonly TelemetryActivity _telemetryActivity;

        public DependencyGraphResolver(RestoreCollectorLogger logger, RestoreRequest restoreRequest, TelemetryActivity telemetryActivity, Guid operationId)
        {
            _logger = logger;
            _request = restoreRequest;
            _telemetryActivity = telemetryActivity;
            _operationId = operationId;
        }

        public enum LibraryDependencyIndex : int
        {
            Invalid = -1,
        }

        public enum LibraryRangeIndex : int
        {
            Invalid = -1,
        }

        public async Task<ValueTuple<bool, IEnumerable<RestoreTargetGraph>>> ResolveAsync(NuGetv3LocalRepository userPackageFolder, IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders, RemoteWalkContext context, CancellationToken token)
        {
            bool _success = true;

            if (_request.Project.TargetFrameworks.Count == 0)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_ProjectDoesNotSpecifyTargetFrameworks, _request.Project.Name, _request.Project.FilePath);
                await _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1001, message));

                _success = false;
                return (_success, Enumerable.Empty<RestoreTargetGraph>());
            }

            var projectRestoreRequest = new ProjectRestoreRequest(
             _request,
             _request.Project,
             _request.ExistingLockFile,
             _logger)
            {
                ParentId = _operationId
            };

            var projectRestoreCommand = new ProjectRestoreCommand(projectRestoreRequest);

            var localRepositories = new List<NuGetv3LocalRepository>();
            localRepositories.Add(userPackageFolder);
            localRepositories.AddRange(fallbackPackageFolders);

            _logger.LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoringPackages, _request.Project.FilePath));

            // Get external project references
            // If the top level project already exists, update the package spec provided
            // with the RestoreRequest spec.
            var updatedExternalProjects = RestoreCommand.GetProjectReferences(_request);

            // Load repositories
            // the external project provider is specific to the current restore project
            context.ProjectLibraryProviders.Add(
                    new PackageSpecReferenceDependencyProvider(updatedExternalProjects, _logger));

            var projectRange = new LibraryRange()
            {
                Name = _request.Project.Name,
                VersionRange = new VersionRange(_request.Project.Version),
                TypeConstraint = LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject
            };

            // Resolve dependency graphs
            var allGraphs = new List<RestoreTargetGraph>();
            var graphByTFM = new Dictionary<NuGetFramework, RestoreTargetGraph>();
            var runtimeIds = RequestRuntimeUtility.GetRestoreRuntimes(_request);
            var projectFrameworkRuntimePairs = RestoreCommand.CreateFrameworkRuntimePairs(_request.Project, runtimeIds);
            RuntimeGraph allRuntimes = RuntimeGraph.Empty;
            int patience = 0;
            int maxOutstandingRefs = 0;
            int totalLookups = 0;
            int totalDeepLookups = 0;
            int totalEvictions = 0;
            int totalHardEvictions = 0;

            Stopwatch sw2_prototype = Stopwatch.StartNew();
            Stopwatch sw3_preamble = new Stopwatch();
            Stopwatch sw4_voScan = new Stopwatch();
            Stopwatch sw5_fullImport = new Stopwatch();
            Stopwatch sw6_flatten = new Stopwatch();

            LibraryRangeInterningTable libraryRangeInterningTable = new LibraryRangeInterningTable();
            LibraryDependencyInterningTable libraryDependencyInterningTable = new LibraryDependencyInterningTable();

            bool hasInstallBeenCalledAlready = false;
            foreach (var pair in projectFrameworkRuntimePairs)
            {
                if (!string.IsNullOrWhiteSpace(pair.RuntimeIdentifier) && !hasInstallBeenCalledAlready)
                {
                    await InstallPackagesAsync(allGraphs);

                    hasInstallBeenCalledAlready = true;
                }

                sw3_preamble.Start();

                //Build up our new RestoreTargetGraph.
                //This is done by making everything on RTG setable.  We should move to using a normal constructor.
                var newRTG = new RestoreTargetGraph();
                //Set the statically setable stuff
                newRTG.AnalyzeResult = new AnalyzeResult<RemoteResolveResult>();
                newRTG.Conflicts = new List<ResolverConflict>();
                newRTG.InConflict = false; //its never set for substrate fwiw...
                newRTG.Install = new HashSet<RemoteMatch>();
                newRTG.ResolvedDependencies = new HashSet<ResolvedDependencyKey>();
                newRTG.Unresolved = new HashSet<LibraryRange>();

                //Set the natively settable things.
                newRTG.Framework = pair.Framework;
                newRTG.RuntimeIdentifier = (pair.RuntimeIdentifier == "" ? null : pair.RuntimeIdentifier);
                newRTG.Name = FrameworkRuntimePair.GetName(newRTG.Framework, newRTG.RuntimeIdentifier);
                newRTG.TargetGraphName = FrameworkRuntimePair.GetTargetGraphName(newRTG.Framework, newRTG.RuntimeIdentifier);

                RestoreTargetGraph tfmNonRidGraph = null;
                RuntimeGraph runtimeGraph = null;
                if (!string.IsNullOrEmpty(pair.RuntimeIdentifier))
                {
                    // We start with the non-RID TFM graph.
                    // This is guaranteed to be computed before any graph with a RID, so we can assume this will return a value.
                    tfmNonRidGraph = graphByTFM[pair.Framework];
                    Debug.Assert(tfmNonRidGraph != null);

                    // PCL Projects with Supports have a runtime graph but no matching framework.
                    var runtimeGraphPath = _request.Project.TargetFrameworks.
                            FirstOrDefault(e => NuGetFramework.Comparer.Equals(e.FrameworkName, tfmNonRidGraph.Framework))?.RuntimeIdentifierGraphPath;

                    RuntimeGraph projectProviderRuntimeGraph = null;
                    if (runtimeGraphPath != null)
                    {
                        projectProviderRuntimeGraph = ProjectRestoreCommand.GetRuntimeGraph(runtimeGraphPath, _logger);
                    }

                    runtimeGraph = ProjectRestoreCommand.GetRuntimeGraph(tfmNonRidGraph, localRepositories, projectRuntimeGraph: projectProviderRuntimeGraph, _logger);
                    allRuntimes = RuntimeGraph.Merge(allRuntimes, runtimeGraph);
                }

                newRTG.Conventions = new Client.ManagedCodeConventions(runtimeGraph);

                //Now build up our new flattened graph
                var initialProject = new LibraryDependency(new LibraryRange()
                {
                    Name = _request.Project.Name,
                    VersionRange = new VersionRange(_request.Project.Version),
                    TypeConstraint = LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject
                });

                //If we find newer nodes of things while we walk, we'll evict them.
                //A subset of evictions cause a re-import.  For instance if a newer chosen node has fewer refs,
                //then we might have a dangling over-elevated ref on the old one, it'll be hard evicted.
                //The second item here is the path to root.  When we add a hard-evictee, we'll also remove anything
                //added to the eviction list that contains the evictee on their path to root.
                Queue<ImportRefItem> refImport =
                  new Queue<ImportRefItem>(RefImportQueueSize);

                Dictionary<LibraryRangeIndex, FindLibraryCachedAsyncResult> allResolvedItems =
                    new Dictionary<LibraryRangeIndex, FindLibraryCachedAsyncResult>(2048);

                Dictionary<LibraryDependencyIndex, ResolvedDependencyGraphItem> chosenResolvedItems = new(2048);

                Dictionary<LibraryRangeIndex, (LibraryRangeIndex[], LibraryDependencyIndex, LibraryDependencyTarget)> evictions = new Dictionary<LibraryRangeIndex, (LibraryRangeIndex[], LibraryDependencyIndex, LibraryDependencyTarget)>(1024);

                sw3_preamble.Stop();

                sw5_fullImport.Start();

                ImportRefItem rootProjectRefItem = new ImportRefItem()
                {
                    Ref = initialProject,
                    DependencyIndex = libraryDependencyInterningTable.Intern(initialProject),
                    RangeIndex = libraryRangeInterningTable.Intern(initialProject.LibraryRange),
                    Suppressions = new HashSet<LibraryDependencyIndex>(),
                    CurrentOverrides = new Dictionary<LibraryDependencyIndex, VersionRange>(),
                    DirectPackageReferenceFromRootProject = false,
                };

            ProcessDeepEviction:
                patience++;

                refImport.Clear();
                chosenResolvedItems.Clear();

                refImport.Enqueue(rootProjectRefItem);

                while (refImport.Count > 0)
                {
                    maxOutstandingRefs = Math.Max(refImport.Count, maxOutstandingRefs);

                    ImportRefItem importRefItem = refImport.Dequeue();
                    var currentRef = importRefItem.Ref;
                    var currentRefDependencyIndex = importRefItem.DependencyIndex;
                    var currentRefRangeIndex = importRefItem.RangeIndex;
                    var pathToCurrentRef = importRefItem.PathToRef;
                    var currentSuppressions = importRefItem.Suppressions;
                    var currentOverrides = importRefItem.CurrentOverrides;
                    var directPackageReferenceFromRootProject = importRefItem.DirectPackageReferenceFromRootProject;
                    LibraryRangeIndex libraryRangeOfCurrentRef = importRefItem.RangeIndex;

                    LibraryDependencyTarget typeConstraint = currentRef.LibraryRange.TypeConstraint;
                    bool isProject = ((typeConstraint == LibraryDependencyTarget.Project) ||
                                      (typeConstraint == (LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject)));

                    if (evictions.TryGetValue(libraryRangeOfCurrentRef, out var eviction))
                    {
                        (LibraryRangeIndex[] evictedPath, LibraryDependencyIndex evictedDepIndex, LibraryDependencyTarget evictedTypeConstraint) = eviction;

                        // If we evicted this same version previously, but the type constraint of currentRef is more stringent (package), then do not skip the current item - this is the one we want.
                        if (!((evictedTypeConstraint == LibraryDependencyTarget.PackageProjectExternal || evictedTypeConstraint == LibraryDependencyTarget.ExternalProject) &&
                            currentRef.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package))
                        {
                            continue;
                        }
                    }

                    if (currentOverrides.TryGetValue(currentRefDependencyIndex, out var ov))
                    {
                        if (!ov.Equals(currentRef.LibraryRange.VersionRange))
                        {
                            continue;
                        }
                    }

                    //else if we've seen this ref (but maybe not version) before check to see if we need to upgrade
                    if (chosenResolvedItems.TryGetValue(currentRefDependencyIndex, out var chosenResolvedItem))
                    {
                        LibraryDependency chosenRef = chosenResolvedItem.libRef;
                        LibraryRangeIndex chosenRefRangeIndex = chosenResolvedItem.rangeIndex;
                        LibraryRangeIndex[] pathChosenRef = chosenResolvedItem.pathToRef;
                        bool packageReferenceFromRootProject = chosenResolvedItem.directPackageReferenceFromRootProject;
                        List<SuppressionsAndOverrides> chosenSuppressions = chosenResolvedItem.chosenSuppressions;

                        if (packageReferenceFromRootProject)
                        {
                            continue;
                        }

                        // We should evict on type constraint if the type constraint of the current ref is more stringent than the chosen ref.
                        // This happens when a previous type constraint is broader (e.g. PackageProjectExternal) than the current type constraint (e.g. Package).
                        bool evictOnTypeConstraint = false;
                        if ((chosenRefRangeIndex == currentRefRangeIndex) &&
                            LibraryDependencyTargetUtils.EvictOnTypeConstraint(currentRef.LibraryRange.TypeConstraint, chosenRef.LibraryRange.TypeConstraint))
                        {
                            if (allResolvedItems.TryGetValue(chosenRefRangeIndex, out FindLibraryCachedAsyncResult resolvedItem) && resolvedItem.Item.Key.Type == LibraryType.Project)
                            {
                                // We need to evict the chosen item because this one has a more stringent type constraint.
                                evictOnTypeConstraint = true;
                            }
                        }

                        // TODO: Handle null version ranges
                        VersionRange nvr = currentRef.LibraryRange.VersionRange;
                        VersionRange ovr = chosenRef.LibraryRange.VersionRange;

                        if (evictOnTypeConstraint || !RemoteDependencyWalker.IsGreaterThanOrEqualTo(ovr, nvr))
                        {
                            if (chosenRef.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package && currentRef.LibraryRange.TypeConstraint == LibraryDependencyTarget.PackageProjectExternal)
                            {
                                bool commonAncestry = true;

                                for (int i = 0; i < importRefItem.PathToRef.Length && i < chosenResolvedItem.pathToRef.Length; i++)
                                {
                                    if (importRefItem.PathToRef[i] != chosenResolvedItem.pathToRef[i])
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

                            //If we think the newer thing we are looking at is better, remove the old one and let it fall thru.

                            chosenResolvedItems.Remove(currentRefDependencyIndex);
                            //Record an eviction for the node we are replacing.  The eviction path is for the current node.
                            LibraryRangeIndex evictedLR = chosenRefRangeIndex;

                            // If we're evicting on typeconstraint, then there is already an item in allResolvedItems that matches the old typeconstraint.
                            // We must remove it, otherwise we won't call FindLibraryCachedAsync again to load the correct item and save it into allResolvedItems.
                            if (evictOnTypeConstraint)
                            {
                                allResolvedItems.Remove(evictedLR);
                            }

                            int deepEvictions = 0;
                            //unwind anything chosen by the node we're evicting..
                            HashSet<LibraryRangeIndex> evicteesToRemove = null;
                            foreach (var evictee in evictions)
                            {
                                (LibraryRangeIndex[] evicteePath, LibraryDependencyIndex evicteeDepIndex, LibraryDependencyTarget evicteeTypeConstraint) = evictee.Value;

                                if (evicteePath.Contains(evictedLR))
                                {
                                    // if evictee.Key (depIndex) == currentDepIndex && evictee.TypeConstraint == ExternalProject --> Don't remove it.  It must remain evicted.
                                    // If the evictee to remove is the same dependency, but the project version of said dependency, then do not remove it - it must remain evicted in favor of the package.
                                    if (!(evicteeDepIndex == currentRefDependencyIndex &&
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
                            foreach (var chosenItem in chosenResolvedItems)
                            {
                                if (chosenItem.Value.pathToRef.Contains(evictedLR))
                                {
                                    deepEvictions++;
                                    break;
                                }
                            }
                            evictions[evictedLR] = (LibraryRangeInterningTable.CreatePathToRef(pathToCurrentRef, libraryRangeOfCurrentRef), currentRefDependencyIndex, chosenRef.LibraryRange.TypeConstraint);
                            totalEvictions++;
                            if (deepEvictions > 0)
                            {
                                totalHardEvictions++;
                                goto ProcessDeepEviction;
                            }
                            //Since this is a "new" choice, its gets a new import context list
                            chosenResolvedItems.Add(
                                currentRefDependencyIndex,
                                new ResolvedDependencyGraphItem
                                {
                                    libRef = currentRef,
                                    rangeIndex = currentRefRangeIndex,
                                    pathToRef = pathToCurrentRef,
                                    directPackageReferenceFromRootProject = directPackageReferenceFromRootProject,
                                    chosenSuppressions = new List<SuppressionsAndOverrides>
                                    {
                                        new SuppressionsAndOverrides
                                        {
                                            currentSuppressions = currentSuppressions,
                                            currentOverrides = currentOverrides
                                        }
                                    }
                                });

                            //if we are going to live with this queue and chosen state, we need to also kick
                            // any queue members who were descendants of the thing we just evicted.
                            var newRefImport =
                                new Queue<ImportRefItem>(RefImportQueueSize);
                            while (refImport.Count > 0)
                            {
                                ImportRefItem item = refImport.Dequeue();
                                if (!item.PathToRef.Contains(evictedLR))
                                    newRefImport.Enqueue(item);
                            }
                            refImport = newRefImport;
                        }
                        //if its lower we'll never do anything other than skip it.
                        else if (!VersionRange.PreciseEquals(ovr, nvr))
                        {
                            continue;
                        }
                        else
                        //we are looking at same.  consider if its an upgrade.
                        {
                            //If the one we already have chosen is pure, then we can skip this one.  Processing it wont bring any new info
                            if ((chosenSuppressions.Count == 1) && (chosenSuppressions[0].currentSuppressions.Count == 0) &&
                                (chosenSuppressions[0].currentOverrides.Count == 0))
                            {
                                continue;
                            }
                            //if the one we are now looking at is pure, then we should replace the one we have chosen because if we're here it isnt pure.
                            else if ((currentSuppressions.Count == 0) && (currentOverrides.Count == 0))
                            {
                                chosenResolvedItems.Remove(currentRefDependencyIndex);
                                //slightly evil, but works.. we should just shift to the current thing as ref?
                                chosenResolvedItems.Add(
                                    currentRefDependencyIndex,
                                    new ResolvedDependencyGraphItem
                                    {
                                        libRef = currentRef,
                                        rangeIndex = currentRefRangeIndex,
                                        pathToRef = pathToCurrentRef,
                                        directPackageReferenceFromRootProject = packageReferenceFromRootProject,
                                        chosenSuppressions = new List<SuppressionsAndOverrides>
                                        {
                                            new SuppressionsAndOverrides
                                            {
                                                currentSuppressions = currentSuppressions,
                                                currentOverrides = currentOverrides
                                            }
                                        }
                                    });
                            }
                            else
                            //check to see if we are equal to one of the dispositions or if we are less restrictive than one
                            {
                                bool isEqualOrSuperSetDisposition = false;
                                foreach (var chosenImportDisposition in chosenSuppressions)
                                {
                                    bool localIsEqualOrSuperSetDisposition = currentSuppressions.IsSupersetOf(chosenImportDisposition.currentSuppressions);

                                    bool localIsEqualOrSuperSetOverride = currentOverrides.Count >= chosenImportDisposition.currentOverrides.Count;
                                    if (localIsEqualOrSuperSetOverride)
                                    {
                                        foreach (var chosenOverride in chosenImportDisposition.currentOverrides)
                                        {
                                            if (!currentOverrides.TryGetValue(chosenOverride.Key, out VersionRange currentOverride))
                                            {
                                                localIsEqualOrSuperSetOverride = false;
                                                break;
                                            }
                                            if (!VersionRange.PreciseEquals(currentOverride, chosenOverride.Value))
                                            {
                                                localIsEqualOrSuperSetOverride = false;
                                                break;
                                            }
                                        }
                                    }

                                    if (localIsEqualOrSuperSetDisposition && localIsEqualOrSuperSetOverride)
                                    {
                                        isEqualOrSuperSetDisposition = true;
                                    }
                                }

                                if (isEqualOrSuperSetDisposition)
                                {
                                    continue;
                                }
                                else
                                {
                                    //case of differently restrictive dispositions or less restrictive... we should technically be able to remove
                                    //a disposition if its less restrictive than another.  But we'll just add it to the list.
                                    chosenResolvedItems.Remove(currentRefDependencyIndex);
                                    var newImportDisposition =
                                        new List<SuppressionsAndOverrides> {
                                            new SuppressionsAndOverrides
                                            {
                                                currentSuppressions = currentSuppressions,
                                                currentOverrides = currentOverrides
                                            }
                                        };
                                    newImportDisposition.AddRange(chosenSuppressions);
                                    //slightly evil, but works.. we should just shift to the current thing as ref?
                                    chosenResolvedItems.Add(
                                        currentRefDependencyIndex,
                                        new ResolvedDependencyGraphItem {
                                            libRef = currentRef,
                                            rangeIndex = currentRefRangeIndex,
                                            pathToRef = pathToCurrentRef,
                                            directPackageReferenceFromRootProject = packageReferenceFromRootProject,
                                            chosenSuppressions = newImportDisposition
                                        });
                                }
                            }
                        }
                    }
                    else
                    {
                        //This is now the thing we think is the highest version of this ref
                        chosenResolvedItems.Add(
                            currentRefDependencyIndex,
                            new ResolvedDependencyGraphItem
                            {
                                libRef = currentRef,
                                rangeIndex = currentRefRangeIndex,
                                pathToRef = pathToCurrentRef,
                                directPackageReferenceFromRootProject = directPackageReferenceFromRootProject,
                                chosenSuppressions = new List<SuppressionsAndOverrides>
                                {
                                    new SuppressionsAndOverrides
                                    {
                                        currentSuppressions = currentSuppressions,
                                        currentOverrides = currentOverrides
                                    }
                                }
                            });
                    }
                    FindLibraryCachedAsyncResult refItemResult = null;
                    if (!allResolvedItems.TryGetValue(libraryRangeOfCurrentRef, out refItemResult))
                    {
                        GraphItem<RemoteResolveResult> refItem;
                        try
                        {
                            refItem = ResolverUtility.FindLibraryEntryAsync(
                                currentRef.LibraryRange,
                                newRTG.Framework,
                                newRTG.RuntimeIdentifier,
                                context,
                                CancellationToken.None).GetAwaiter().GetResult();
                        }
                        catch (FatalProtocolException)
                        {
                            foreach (FrameworkRuntimePair frameworkRuntimePair in RestoreCommand.CreateFrameworkRuntimePairs(_request.Project, RequestRuntimeUtility.GetRestoreRuntimes(_request)))
                            {
                                allGraphs.Add(RestoreTargetGraph.Create(_request.Project.RuntimeGraph, Enumerable.Empty<GraphNode<RemoteResolveResult>>(), context, _logger, frameworkRuntimePair.Framework, frameworkRuntimePair.RuntimeIdentifier));
                            }

                            _success = false;

                            return (_success, allGraphs);
                        }

                        totalDeepLookups++;

                        refItemResult = new FindLibraryCachedAsyncResult(
                            currentRef,
                            refItem,
                            currentRefDependencyIndex,
                            currentRefRangeIndex,
                            libraryDependencyInterningTable,
                            libraryRangeInterningTable);

                        allResolvedItems.Add(libraryRangeOfCurrentRef, refItemResult);
                    }

                    totalLookups++;

                    HashSet<LibraryDependencyIndex> suppressions = null;
                    IReadOnlyDictionary<LibraryDependencyIndex, VersionRange> finalVersionOverrides = null;
                    Dictionary<LibraryDependencyIndex, VersionRange> newOverrides = null;
                    //Scan for suppressions and overrides
                    for (int i = 0; i < refItemResult.Item.Data.Dependencies.Count; i++)
                    {
                        var dep = refItemResult.Item.Data.Dependencies[i];
                        LibraryDependencyIndex depIndex = refItemResult.GetDependencyIndexForDependency(i);
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
                                newOverrides = new Dictionary<LibraryDependencyIndex, VersionRange>(OverridesDictionarySize);
                            }
                            newOverrides[depIndex] = dep.VersionOverride;
                        }
                    }

                    // If the override set has been mutated, then add the rest of the overrides.
                    // Otherwise, just use the incoming set of overrides.
                    if (newOverrides != null)
                    {
                        Dictionary<LibraryDependencyIndex, VersionRange> allOverrides =
                            new Dictionary<LibraryDependencyIndex, VersionRange>(currentOverrides.Count + newOverrides.Count);
                        allOverrides.AddRange(currentOverrides);
                        foreach (var overridePair in newOverrides)
                        {
                            allOverrides[overridePair.Key] = overridePair.Value;
                        }
                        finalVersionOverrides = allOverrides;
                    }
                    else
                    {
                        finalVersionOverrides = currentOverrides;
                    }

                    // If the suppressions have been mutated, then add the rest of the suppressions.
                    // Otherwise just use teh incoming set of suppressions.
                    if (suppressions != null)
                    {
                        suppressions.AddRange(currentSuppressions);
                    }
                    else
                    {
                        suppressions = currentSuppressions;
                    }

                    for (int i = 0; i < refItemResult.Item.Data.Dependencies.Count; i++)
                    {
                        var dep = refItemResult.Item.Data.Dependencies[i];
                        LibraryDependencyIndex depIndex = refItemResult.GetDependencyIndexForDependency(i);

                        //Suppress this node
                        if (suppressions.Contains(depIndex))
                        {
                            continue;
                        }

                        refImport.Enqueue(new ImportRefItem()
                        {
                            Ref = dep,
                            DependencyIndex = depIndex,
                            RangeIndex = refItemResult.GetRangeIndexForDependency(i),
                            PathToRef = LibraryRangeInterningTable.CreatePathToRef(pathToCurrentRef, libraryRangeOfCurrentRef),
                            Suppressions = suppressions,
                            CurrentOverrides = finalVersionOverrides,
                            DirectPackageReferenceFromRootProject = (currentRefRangeIndex == rootProjectRefItem.RangeIndex) && (dep.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package),
                        });
                    }

                    // Add runtime dependencies of the current node if a runtime identifier has been specified.
                    if (!string.IsNullOrEmpty(pair.RuntimeIdentifier))
                    {
                        // Check for runtime dependencies.
                        HashSet<LibraryDependency> runtimeDependencies = new HashSet<LibraryDependency>();
                        LibraryRange libraryRange = currentRef.LibraryRange;

                        FindLibraryCachedAsyncResult findLibraryCachedAsyncResult = null;
                        RemoteDependencyWalker.EvaluateRuntimeDependencies(ref libraryRange, pair.RuntimeIdentifier, runtimeGraph, ref runtimeDependencies);

                        if (runtimeDependencies.Count > 0)
                        {
                            // Runtime dependencies start after non-runtime dependencies.
                            // Keep track of the first index for any runtime dependencies so that it can be used to enqueue later.
                            int runtimeDependencyIndex = refItemResult.Item.Data.Dependencies.Count;

                            // If there are runtime dependencies that need to be added, remove the currentRef from allResolvedItems,
                            // and add the newly created version that contains the previously detected dependencies and newly detected runtime dependencies.
                            allResolvedItems.Remove(libraryRangeOfCurrentRef);
                            bool rootHasInnerNodes = (refItemResult.Item.Data.Dependencies.Count + (runtimeDependencies == null ? 0 : runtimeDependencies.Count)) > 0;
                            GraphNode<RemoteResolveResult> rootNode = new GraphNode<RemoteResolveResult>(libraryRange, rootHasInnerNodes, false)
                            {
                                Item = refItemResult.Item,
                            };
                            RemoteDependencyWalker.MergeRuntimeDependencies(runtimeDependencies, rootNode);
                            findLibraryCachedAsyncResult = new FindLibraryCachedAsyncResult(
                                currentRef,
                                rootNode.Item,
                                currentRefDependencyIndex,
                                currentRefRangeIndex,
                                libraryDependencyInterningTable,
                                libraryRangeInterningTable);
                            allResolvedItems.Add(libraryRangeOfCurrentRef, findLibraryCachedAsyncResult);

                            // Enqueue each of the runtime dependencies, but only if they weren't already present in refItemResult before merging the runtime dependencies above.
                            if ((rootNode.Item.Data.Dependencies.Count - runtimeDependencyIndex) == runtimeDependencies.Count)
                            {
                                foreach (var dep in runtimeDependencies)
                                {
                                    refImport.Enqueue(new ImportRefItem()
                                    {
                                        Ref = dep,
                                        DependencyIndex = findLibraryCachedAsyncResult.GetDependencyIndexForDependency(runtimeDependencyIndex),
                                        RangeIndex = findLibraryCachedAsyncResult.GetRangeIndexForDependency(runtimeDependencyIndex),
                                        PathToRef = LibraryRangeInterningTable.CreatePathToRef(pathToCurrentRef, libraryRangeOfCurrentRef),
                                        Suppressions = suppressions,
                                        CurrentOverrides = finalVersionOverrides,
                                        DirectPackageReferenceFromRootProject = false,
                                    });

                                    runtimeDependencyIndex++;
                                }
                            }
                        }
                    }
                }

                sw5_fullImport.Stop();
                sw6_flatten.Start();

                //Now that we've completed import, figure out the short real flattened list
                var newFlattened = new HashSet<GraphItem<RemoteResolveResult>>();
                HashSet<LibraryDependencyIndex> visitedItems = new HashSet<LibraryDependencyIndex>();
                Queue<(LibraryDependencyIndex, GraphNode<RemoteResolveResult>)> itemsToFlatten = new Queue<(LibraryDependencyIndex, GraphNode<RemoteResolveResult>)>();
                var nGraph = new List<GraphNode<RemoteResolveResult>>();

                LibraryDependencyIndex initialProjectIndex = rootProjectRefItem.DependencyIndex;
                var cri = chosenResolvedItems[initialProjectIndex];
                LibraryDependency startRef = cri.libRef;

                var rootGraphNode = new GraphNode<RemoteResolveResult>(startRef.LibraryRange);
                LibraryRangeIndex startRefLibraryRangeIndex = cri.rangeIndex;
                rootGraphNode.Item = allResolvedItems[startRefLibraryRangeIndex].Item;
                nGraph.Add(rootGraphNode);

                var nodesById = new Dictionary<LibraryRangeIndex, GraphNode<RemoteResolveResult>>();

                var downgrades = new Dictionary<LibraryRangeIndex, (GraphNode<RemoteResolveResult> OuterNode, LibraryDependency LibraryDependency)>();

                var versionConflicts = new Dictionary<LibraryRangeIndex, GraphNode<RemoteResolveResult>>();

                itemsToFlatten.Enqueue((initialProjectIndex, rootGraphNode));
                while (itemsToFlatten.Count > 0)
                {
                    (LibraryDependencyIndex currentDependencyIndex, GraphNode<RemoteResolveResult> currentGraphNode) = itemsToFlatten.Dequeue();
                    if (!chosenResolvedItems.TryGetValue(currentDependencyIndex, out var foundItem))
                    {
                        continue;
                    }
                    LibraryDependency chosenRef = foundItem.libRef;
                    LibraryRangeIndex chosenRefRangeIndex = foundItem.rangeIndex;
                    LibraryRangeIndex[] pathToChosenRef = foundItem.pathToRef;
                    bool directPackageReferenceFromRootProject = foundItem.directPackageReferenceFromRootProject;
                    List<SuppressionsAndOverrides> chosenSuppressions = foundItem.chosenSuppressions;

                    if (allResolvedItems.TryGetValue(chosenRefRangeIndex, out var node))
                    {
                        newFlattened.Add(node.Item);

                        // If the package came from a remote library provider, it needs to be installed locally.
                        var isRemote = context.RemoteLibraryProviders.Contains(node.Item.Data.Match.Provider);
                        if (isRemote)
                        {
                            newRTG.Install.Add(node.Item.Data.Match);
                        }

                        for (int i = 0; i < node.Item.Data.Dependencies.Count; i++)
                        {
                            var dep = node.Item.Data.Dependencies[i];
                            if (StringComparer.OrdinalIgnoreCase.Equals(dep.Name, node.Item.Key.Name) || StringComparer.OrdinalIgnoreCase.Equals(dep.Name, rootGraphNode.Key.Name))
                            {
                                // Cycle
                                var nodeWithCycle = new GraphNode<RemoteResolveResult>(dep.LibraryRange)
                                {
                                    OuterNode = currentGraphNode,
                                    Disposition = Disposition.Cycle
                                };

                                newRTG.AnalyzeResult.Cycles.Add(nodeWithCycle);

                                continue;
                            }

                            LibraryDependencyIndex depIndex = node.GetDependencyIndexForDependency(i);

                            if (!chosenResolvedItems.TryGetValue(depIndex, out var chosenItem))
                            {
                                continue;
                            }

                            var chosenItemRangeIndex = chosenItem.rangeIndex;
                            LibraryDependency actualDep = chosenItem.libRef;

                            if (!visitedItems.Add(depIndex))
                            {
                                LibraryRangeIndex currentRangeIndex = node.GetRangeIndexForDependency(i);

                                if (pathToChosenRef.Contains(currentRangeIndex))
                                {
                                    // Cycle
                                    var nodeWithCycle = new GraphNode<RemoteResolveResult>(dep.LibraryRange);
                                    nodeWithCycle.OuterNode = currentGraphNode;
                                    nodeWithCycle.Disposition = Disposition.Cycle;
                                    newRTG.AnalyzeResult.Cycles.Add(nodeWithCycle);

                                    continue;
                                }

                                if (!RemoteDependencyWalker.IsGreaterThanOrEqualTo(actualDep.LibraryRange.VersionRange, dep.LibraryRange.VersionRange))
                                {
                                    if (node.DependencyIndex != rootProjectRefItem.DependencyIndex && dep.SuppressParent == LibraryIncludeFlags.All)
                                    {
                                        continue;
                                    }

                                    if (chosenSuppressions.Count > 0 && chosenSuppressions[0].currentSuppressions.Contains(depIndex))
                                    {
                                        continue;
                                    }

                                    // Downgrade
                                    if (!downgrades.ContainsKey(chosenItemRangeIndex))
                                    {
                                        downgrades.Add(chosenItemRangeIndex, (currentGraphNode, dep));
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

                            if (newGraphNode.Item.Key.Type != LibraryType.Project && !versionConflicts.ContainsKey(chosenItemRangeIndex) && dep.SuppressParent != LibraryIncludeFlags.All && !dep.LibraryRange.VersionRange.Satisfies(newGraphNode.Item.Key.Version))
                            {
                                currentGraphNode.InnerNodes.Remove(newGraphNode);

                                // Conflict
                                var conflictingNode = new GraphNode<RemoteResolveResult>(dep.LibraryRange)
                                {
                                    Disposition = Disposition.Acceptable
                                };

                                conflictingNode.Item = new GraphItem<RemoteResolveResult>(new LibraryIdentity(dep.Name, dep.LibraryRange.VersionRange.MinVersion, LibraryType.Package));
                                currentGraphNode.InnerNodes.Add(conflictingNode);
                                conflictingNode.OuterNode = currentGraphNode;

                                versionConflicts.Add(chosenItemRangeIndex, conflictingNode);

                                continue;
                            }

                            nodesById.Add(chosenItemRangeIndex, newGraphNode);
                            itemsToFlatten.Enqueue((depIndex, newGraphNode));

                            if (newGraphNode.Item.Key.Type == LibraryType.Unresolved)
                            {
                                newRTG.Unresolved.Add(actualDep.LibraryRange);

                                _success = false;

                                continue;
                            }

                            newRTG.ResolvedDependencies.Add(new ResolvedDependencyKey(
                                parent: newGraphNode.OuterNode.Item.Key,
                                range: newGraphNode.Key.VersionRange,
                                child: newGraphNode.Item.Key));
                        }
                    }
                }

                if (versionConflicts.Count > 0)
                {
                    foreach (var versionConflict in versionConflicts)
                    {
                        if (nodesById.TryGetValue(versionConflict.Key, out var selected))
                        {
                            newRTG.AnalyzeResult.VersionConflicts.Add(new VersionConflictResult<RemoteResolveResult>
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
                            Item = new GraphItem<RemoteResolveResult>(new LibraryIdentity(downgrade.Value.LibraryDependency.Name, downgrade.Value.LibraryDependency.LibraryRange.VersionRange.MinVersion, LibraryType.Package)),
                            OuterNode = downgrade.Value.OuterNode
                        };

                        newRTG.AnalyzeResult.Downgrades.Add(new DowngradeResult<RemoteResolveResult>
                        {
                            DowngradedFrom = downgradedFrom,
                            DowngradedTo = downgradedTo
                        });
                    }
                }

                sw6_flatten.Stop();

                newRTG.Flattened = newFlattened;

                newRTG.Graphs = nGraph;

                foreach (var profile in _request.Project.RuntimeGraph.Supports)
                {
                    var runtimes = allRuntimes;

                    CompatibilityProfile compatProfile;
                    if (profile.Value.RestoreContexts.Any())
                    {
                        // Just use the contexts from the project definition
                        compatProfile = profile.Value;
                    }
                    else if (!runtimes.Supports.TryGetValue(profile.Value.Name, out compatProfile))
                    {
                        // No definition of this profile found, so just continue to the next one
                        var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_UnknownCompatibilityProfile, profile.Key);

                        await _logger.LogAsync(RestoreLogMessage.CreateWarning(NuGetLogCode.NU1502, message));
                        continue;
                    }

                    foreach (var frameworkRuntimePair in compatProfile.RestoreContexts)
                    {
                        _logger.LogDebug($" {profile.Value.Name} -> +{frameworkRuntimePair}");
                        _request.CompatibilityProfiles.Add(frameworkRuntimePair);
                    }
                }

                allGraphs.Add(newRTG);

                if (string.IsNullOrEmpty(pair.RuntimeIdentifier))
                {
                    graphByTFM.Add(pair.Framework, newRTG);
                }
            }

            await InstallPackagesAsync(allGraphs);

            sw2_prototype.Stop();

            // Update the logger with the restore target graphs
            // This allows lazy initialization for the Transitive Warning Properties
            _logger.ApplyRestoreOutput(allGraphs);

            if (!_success)
            {
                // Log message for any unresolved dependencies
                await UnresolvedMessages.LogAsync(allGraphs, context, token);
            }
            return (_success, allGraphs);

            async Task<bool> InstallPackagesAsync(IEnumerable<RestoreTargetGraph> graphs)
            {
                DownloadDependencyResolutionResult[] downloadDependencyResolutionResults = await ProjectRestoreCommand.DownloadDependenciesAsync(_request.Project, context, _telemetryActivity, string.Empty, token);

                HashSet<LibraryIdentity> uniquePackages = new HashSet<LibraryIdentity>();

                _success &= await projectRestoreCommand.InstallPackagesAsync(
                    uniquePackages,
                graphs,
                    downloadDependencyResolutionResults,
                    userPackageFolder,
                    token);

                if (downloadDependencyResolutionResults.Any(e => e.Unresolved.Count > 0))
                {
                    _success = false;

                    await UnresolvedMessages.LogAsync(downloadDependencyResolutionResults, context, token);
                }

                return _success;
            }
        }

        public class FindLibraryCachedAsyncResult
        {
            private LibraryDependencyIndex[] _dependencyIndices;
            private LibraryRangeIndex[] _rangeIndices;

            public FindLibraryCachedAsyncResult(
                LibraryDependency libraryDependency,
                GraphItem<RemoteResolveResult> resolvedItem,
                LibraryDependencyIndex itemDependencyIndex,
                LibraryRangeIndex itemRangeIndex,
                LibraryDependencyInterningTable libraryDependencyInterningTable,
                LibraryRangeInterningTable libraryRangeInterningTable)
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

        public sealed class LibraryDependencyInterningTable
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

        public sealed class LibraryRangeInterningTable
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

            internal static LibraryRangeIndex[] CreatePathToRef(LibraryRangeIndex[] existingPath, LibraryRangeIndex currentRef)
            {
                LibraryRangeIndex[] newPath = new LibraryRangeIndex[existingPath.Length + 1];
                Array.Copy(existingPath, newPath, existingPath.Length);
                newPath[newPath.Length - 1] = currentRef;

                return newPath;
            }
        }

        internal class ImportRefItem
        {
            public IReadOnlyDictionary<LibraryDependencyIndex, VersionRange> CurrentOverrides { get; set; }
            public LibraryDependencyIndex DependencyIndex { get; set; } = LibraryDependencyIndex.Invalid;
            public bool DirectPackageReferenceFromRootProject { get; set; }
            public LibraryRangeIndex[] PathToRef { get; set; } = Array.Empty<LibraryRangeIndex>();
            public LibraryRangeIndex RangeIndex { get; set; } = LibraryRangeIndex.Invalid;

            public LibraryDependency Ref { get; set; }

            public HashSet<LibraryDependencyIndex> Suppressions { get; set; }
        }

        public struct ResolvedDependencyGraphItem
        {
            public LibraryDependency libRef { get; set; }
            public LibraryRangeIndex rangeIndex { get; set; }
            public LibraryRangeIndex [] pathToRef { get; set; }
            public bool directPackageReferenceFromRootProject { get; set; }

            public List<SuppressionsAndOverrides> chosenSuppressions { get; set; }
        }

        public struct SuppressionsAndOverrides
        {
            public HashSet<LibraryDependencyIndex> currentSuppressions { get; set; }
            public IReadOnlyDictionary<LibraryDependencyIndex, VersionRange> currentOverrides {get; set;}
        }
    }
}
