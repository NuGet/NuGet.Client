// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NuGet.DependencyResolver;

namespace NuGet.Commands
{
    public class TransitiveNoWarnUtils
    {
        // static should be fine across multiple calls as this solely depends on the csproj file of the project.
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<NuGetFramework, WarningPropertiesCollection>> _warningPropertiesPerFrameworkCache =
            new ConcurrentDictionary<string, ConcurrentDictionary<NuGetFramework, WarningPropertiesCollection>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a PackageSpecificWarningProperties for a project generated by traversing the dependency graph.
        /// </summary>
        /// <param name="targetGraphs">Parent project restore target graphs.</param>
        /// <param name="parentProjectSpec">PackageSpec of the parent project.</param>
        /// <returns>WarningPropertiesCollection with the project frameworks and the transitive package specific no warn properties.</returns>
        public WarningPropertiesCollection CreateTransitiveWarningPropertiesCollection(
            IEnumerable<RestoreTargetGraph> targetGraphs,
            PackageSpec parentProjectSpec)
        {
            var transitivePackageSpecificProperties = new PackageSpecificWarningProperties();
            var projectFrameworks = new List<NuGetFramework>();
            var parentWarningProperties = new WarningPropertiesCollection(
                    parentProjectSpec.RestoreMetadata?.ProjectWideWarningProperties,
                    PackageSpecificWarningProperties.CreatePackageSpecificWarningProperties(parentProjectSpec),
                    parentProjectSpec.TargetFrameworks.Select(f => f.FrameworkName).AsList().AsReadOnly());

            foreach (var targetGraph in targetGraphs)
            {
                if (string.IsNullOrEmpty(targetGraph.RuntimeIdentifier))
                {
                    var transitiveNoWarnFromTargetGraph = ExtractTransitiveNoWarnProperties(
                        targetGraph,
                        parentProjectSpec.RestoreMetadata.ProjectName,
                        parentWarningProperties);

                    projectFrameworks.Add(targetGraph.Framework);

                    transitivePackageSpecificProperties = MergePackageSpecificWarningProperties(
                        transitivePackageSpecificProperties, 
                        transitiveNoWarnFromTargetGraph);
                }
            }

            return new WarningPropertiesCollection(
                projectWideWarningProperties: null,
                packageSpecificWarningProperties: transitivePackageSpecificProperties,
                projectFrameworks: projectFrameworks
                );
        }

        /// <summary>
        /// Traverses a Dependency grpah starting from the parent project in BF style.
        /// </summary>
        /// <param name="targetGraph">Parent project restore target graph.</param>
        /// <param name="parentProjectName">File path of the parent project.</param>
        /// <param name="parentWarningPropertiesCollection">WarningPropertiesCollection of the parent project.</param>
        /// <returns>PackageSpecificWarningProperties containing all the NoWarn's for each package seen in the graph accumulated while traversing the graph.</returns>
        private PackageSpecificWarningProperties ExtractTransitiveNoWarnProperties(
            RestoreTargetGraph targetGraph,
            string parentProjectName,
            WarningPropertiesCollection parentWarningPropertiesCollection)
        {
            var dependencyMapping = new Dictionary<string, LookUpNode>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<DependencyNode>();
            var seen = new HashSet<DependencyNode>();
            var frameworkReducer = new FrameworkReducer();
            var resultWarningProperties = new PackageSpecificWarningProperties();
            var packageNoWarn = new Dictionary<string, ISet<NuGetLogCode>>(StringComparer.OrdinalIgnoreCase);

            // All the packages in parent project's closure. 
            // Once we have collected data for all of these, we can exit.
            var parentPackageDependencies = new HashSet<string>(
                targetGraph.Flattened.Where(d => d.Key.Type == LibraryType.Package).Select(d => d.Key.Name));

            var parentTargetFramework = targetGraph.Framework;

            // Add all dependencies into a dict for a quick transitive lookup
            foreach (var dependencyGraphItem in targetGraph.Flattened.OrderBy(d => d.Key.Name))
            {
                WarningPropertiesCollection nodeWarningProperties = null;

                if (IsProject(dependencyGraphItem.Key.Type))
                {
                    var localMatch = dependencyGraphItem.Data.Match as LocalMatch;
                    var nodeProjectSpec = GetNodePackageSpec(localMatch);
                    var nearestFramework = frameworkReducer.GetNearest(
                        parentTargetFramework, 
                        nodeProjectSpec.TargetFrameworks.Select(tfi => tfi.FrameworkName));

                    // Get the WarningPropertiesCollection from the PackageSpec
                    nodeWarningProperties = GetNodeWarningProperties(nodeProjectSpec, nearestFramework);
                }

                var lookUpNode = new LookUpNode()
                {
                    Dependencies = dependencyGraphItem.Data.Dependencies,
                    WarningPropertiesCollection = nodeWarningProperties
                };

                dependencyMapping[dependencyGraphItem.Key.Name] = lookUpNode;
            }

            // Get the direct dependencies for the parent project to seed the queue
            var parentDependencies = dependencyMapping[parentProjectName];

            // Seed the queue with the parent project's direct dependencies
            AddDependenciesToQueue(parentDependencies.Dependencies,
                queue,
                seen,
                parentWarningPropertiesCollection);

            // Add the parent project to the seen set to prevent adding it back to the queue
            seen.Add(new DependencyNode(id: parentProjectName,
                isProject: true,
                warningPropertiesCollection: parentWarningPropertiesCollection));

            // start taking one node from the queue and get all of it's dependencies
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (!seen.Contains(node))
                {
                    // Add the node to seen set
                    seen.Add(node);

                    var nodeId = node.Id;
                    var nodeIsProject = node.IsProject;
                    var nodeDependencies = dependencyMapping[nodeId].Dependencies;
                    var nodeWarningProperties = dependencyMapping[nodeId].WarningPropertiesCollection;
                    var pathWarningProperties = node.WarningPropertiesCollection;

                    // If the node is a project then we need to extract the warning properties and 
                    // add those to the warning properties of the current path.
                    if (nodeIsProject)
                    {
                        // Merge the WarningPropertiesCollection to the one in the path
                        var mergedWarningProperties = MergeWarningPropertiesCollection(pathWarningProperties,
                            nodeWarningProperties);

                        AddDependenciesToQueue(dependencyMapping[nodeId].Dependencies, 
                            queue, 
                            seen, 
                            mergedWarningProperties);

                    }
                    else if (parentPackageDependencies.Contains(nodeId))
                    {                 
                        // Evaluate the current path for package properties
                        var packageNoWarnFromPath = ExtractPathNoWarnProperties(pathWarningProperties, nodeId);

                        if (packageNoWarn.ContainsKey(nodeId))
                        {
                            // We have seen atleast one path which contained a NoWarn for the package
                            // We need to update the 
                            packageNoWarn[nodeId].IntersectWith(packageNoWarnFromPath);
                        }
                        else
                        {
                            packageNoWarn[nodeId] = packageNoWarnFromPath;
                        }

                        // Check if there was any NoWarn in the path
                        if (packageNoWarn[nodeId].Count == 0)
                        {
                            // If the path does not "NoWarn" for this package then remove the path from parentPackageDependencies
                            // This is done because if there are no "NoWarn" in one path, the the warnings must come through
                            // We no longer care about this package in the graph
                            parentPackageDependencies.Remove(nodeId);

                            // If parentPackageDependencies is empty then exit the graph traversal
                            if (parentPackageDependencies.Count == 0)
                            {
                                break;
                            }
                        }

                        AddDependenciesToQueue(dependencyMapping[nodeId].Dependencies,
                            queue,
                            seen,
                            pathWarningProperties);
                    }
                }
            }

            // At the end of the graph traversal add the remaining package no warn lists into the result
            foreach(var packageId in packageNoWarn.Keys)
            {
                resultWarningProperties.AddRangeOfCodes(packageNoWarn[packageId], packageId, parentTargetFramework);
            }

            return resultWarningProperties;
        }

        private WarningPropertiesCollection GetNodeWarningProperties(PackageSpec nodeProjectSpec, NuGetFramework framework)
        {
            var key = nodeProjectSpec.RestoreMetadata.ProjectPath;

            if (!_warningPropertiesPerFrameworkCache.ContainsKey(key))
            {
                _warningPropertiesPerFrameworkCache[key] =
                    new ConcurrentDictionary<NuGetFramework, WarningPropertiesCollection>(new NuGetFrameworkFullComparer());
            }

            return _warningPropertiesPerFrameworkCache[key].GetOrAdd(framework,
                (s) => new WarningPropertiesCollection(
                    nodeProjectSpec.RestoreMetadata?.ProjectWideWarningProperties,
                    PackageSpecificWarningProperties.CreatePackageSpecificWarningProperties(nodeProjectSpec, framework),
                    nodeProjectSpec.TargetFrameworks.Select(f => f.FrameworkName).AsList().AsReadOnly()));
        }

        private static void AddDependenciesToQueue(IEnumerable<LibraryDependency> dependencies, 
            Queue<DependencyNode> queue, 
            HashSet<DependencyNode> seen,
            WarningPropertiesCollection pathWarningPropertiesCollection)
        {
            // Add all the project's dependencies to the Queue with the merged WarningPropertiesCollection
            foreach (var dependency in dependencies)
            {
                var queueNode = new DependencyNode(
                    dependency.Name,
                    IsProject(dependency.LibraryRange.TypeConstraint),
                    pathWarningPropertiesCollection);

                    // Add the metadata from the parent project here.
                    queue.Enqueue(queueNode);
            }
        }

        private static PackageSpec GetNodePackageSpec(LocalMatch localMatch)
        {
            return (PackageSpec) localMatch.LocalLibrary.Items[KnownLibraryProperties.PackageSpec];
        }

        public static ISet<NuGetLogCode> ExtractPathNoWarnProperties(WarningPropertiesCollection pathWarningProperties, string id)
        {
            var result = new HashSet<NuGetLogCode>();
            if (pathWarningProperties?.ProjectWideWarningProperties?.NoWarn?.Count > 0)
            {
                result.UnionWith(pathWarningProperties.ProjectWideWarningProperties.NoWarn);
            }

            if (pathWarningProperties?.PackageSpecificWarningProperties?.Properties?.Count > 0)
            {
                foreach(var codeIdCollection in pathWarningProperties.PackageSpecificWarningProperties.Properties)
                {
                    var code = codeIdCollection.Key;
                    var IdCollection = codeIdCollection.Value;
                    if (IdCollection.ContainsKey(id))
                    {
                        result.Add(code);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Merge 2 WarningPropertiesCollection objects.
        /// This method will combine the warning properties from both the collections.
        /// </summary>
        /// <param name="first">First Object to be merged.</param>
        /// <param name="second">Second Object to be merged.</param>
        /// <returns>Returns a WarningPropertiesCollection with the combined warning properties.
        /// Returns the reference to one of the inputs if the other input is Null.
        /// Returns a Null if both the input properties are Null. </returns>
        public static WarningPropertiesCollection MergeWarningPropertiesCollection(
            WarningPropertiesCollection first, 
            WarningPropertiesCollection second)
        {
            WarningPropertiesCollection result = null;

            if (TryMergeNullObjects(first, second, out object merged))
            {
                result = merged as WarningPropertiesCollection;
            }
            else
            {
                // Merge Project Wide Warning Properties
                var mergedProjectWideWarningProperties = MergeProjectWideWarningProperties(
                    first.ProjectWideWarningProperties, 
                    second.ProjectWideWarningProperties);

                // Merge Package Specific Warning Properties
                var mergedPackageSpecificWarnings = MergePackageSpecificWarningProperties(
                    first.PackageSpecificWarningProperties,
                    second.PackageSpecificWarningProperties);

                // Ignore the project frameworks as the final collection will contain the parent project frameworks

                result = new WarningPropertiesCollection(
                    mergedProjectWideWarningProperties, 
                    mergedPackageSpecificWarnings,
                    projectFrameworks: null);
            }

            return result;
        }

        /// <summary>
        /// Merge 2 WarningProperties objects.
        /// This method will combine the warning properties from both the collections.
        /// </summary>
        /// <param name="first">First Object to be merged.</param>
        /// <param name="second">Second Object to be merged.</param>
        /// <returns>Returns a WarningProperties with the combined warning properties.
        /// Returns the reference to one of the inputs if the other input is Null.
        /// Returns a Null if both the input properties are Null. </returns>
        public static WarningProperties MergeProjectWideWarningProperties(
            WarningProperties first, 
            WarningProperties second)
        {
            WarningProperties result = null;

            if (TryMergeNullObjects(first, second, out object merged))
            {
                result = merged as WarningProperties;
            }
            else
            {
                // Merge WarningsAsErrors Sets.
                var mergedWarningsAsErrors = new HashSet<NuGetLogCode>();
                mergedWarningsAsErrors.UnionWith(first.WarningsAsErrors);
                mergedWarningsAsErrors.UnionWith(second.WarningsAsErrors);

                // Merge NoWarn Sets.
                var mergedNoWarn = new HashSet<NuGetLogCode>();
                mergedNoWarn.UnionWith(first.NoWarn);
                mergedNoWarn.UnionWith(second.NoWarn);

                // Merge AllWarningsAsErrors. If one project treats all warnigs as errors then the chain will too.
                var mergedAllWarningsAsErrors = first.AllWarningsAsErrors || second.AllWarningsAsErrors;

                result = new WarningProperties(mergedWarningsAsErrors, 
                    mergedNoWarn, 
                    mergedAllWarningsAsErrors);
            }

            return result;
        }

        /// <summary>
        /// Merge 2 PackageSpecificWarningProperties objects.
        /// This method will combine the warning properties from both the collections.
        /// </summary>
        /// <param name="first">First Object to be merged.</param>
        /// <param name="second">Second Object to be merged.</param>
        /// <returns>Returns a PackageSpecificWarningProperties with the combined warning properties.
        /// Will return the reference to one of the inputs if the other input is Null.
        /// Returns a Null if both the input properties are Null. </returns>
        public static PackageSpecificWarningProperties MergePackageSpecificWarningProperties(
            PackageSpecificWarningProperties first,
            PackageSpecificWarningProperties second)
        {
            PackageSpecificWarningProperties result = null;

            if (TryMergeNullObjects(first, second, out object merged))
            {
                result = merged as PackageSpecificWarningProperties;
            }
            else
            {
                result = new PackageSpecificWarningProperties();
                if (first.Properties != null)
                {
                    foreach (var code in first.Properties.Keys)
                    {
                        foreach (var libraryId in first.Properties[code].Keys)
                        {
                            result.AddRangeOfFrameworks(code, libraryId, first.Properties[code][libraryId]);
                        }
                    }
                }

                if (second.Properties != null)
                {
                    foreach (var code in second.Properties.Keys)
                    {
                        foreach (var libraryId in second.Properties[code].Keys)
                        {
                            result.AddRangeOfFrameworks(code, libraryId, second.Properties[code][libraryId]);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Try to merge 2 objects if one or both of them are null.
        /// </summary>
        /// <param name="first">First Object to be merged.</param>
        /// <param name="second">Second Object to be merged.</param>
        /// <param name="merged">Out Merged Object.</param>
        /// <returns>Returns true if atleast one of the objects was Null. 
        /// If none of them is null then the returns false, indicating that the merge failed.</returns>
        public static bool TryMergeNullObjects(object first, object second, out object merged)
        {
            merged = null;
            var result = false;

            if (first == null && second == null)
            {
                merged = null;
                result = true;
            }
            else if (first == null)
            {
                merged = second;
                result = true;
            }
            else if (second == null)
            {
                merged = first;
                result = true;
            }

            return result;
        }

        /// <summary>
        /// Checks if a LibraryDependencyTarget is a project.
        /// </summary>
        /// <param name="type">LibraryDependencyTarget to be checked.</param>
        /// <returns>True if a LibraryDependencyTarget is Project or ExternalProject.</returns>
        private static bool IsProject(LibraryDependencyTarget type)
        {
            return (type == LibraryDependencyTarget.ExternalProject || type == LibraryDependencyTarget.Project);
        }

        /// <summary>
        /// Checks if a LibraryType is a project.
        /// </summary>
        /// <param name="type">LibraryType to be checked.</param>
        /// <returns>True if a LibraryType is Project or ExternalProject.</returns>
        private static bool IsProject(LibraryType type)
        {
            return (type == LibraryType.ExternalProject || type == LibraryType.Project);
        }

        /// <summary>
        /// A simple node class to hold the outgoing dependency edge during the graph walk.
        /// </summary>
        public class DependencyNode : IEquatable<DependencyNode>
        {
            // ID of the Node 
            public string Id { get; }

            // bool to indicate if the node is a project node
            // if false then the node is a package
            public bool IsProject { get; }

            // WarningPropertiesCollection of the path taken to the Node
            public WarningPropertiesCollection WarningPropertiesCollection { get; }

            public DependencyNode(string id, bool isProject, WarningPropertiesCollection warningPropertiesCollection)
            {
                Id = id ?? throw new ArgumentNullException(nameof(id));
                WarningPropertiesCollection = warningPropertiesCollection ?? throw new ArgumentNullException(nameof(warningPropertiesCollection));
                IsProject = isProject;
            }

            public override int GetHashCode()
            {
                var hashCode = new HashCodeCombiner();

                hashCode.AddStringIgnoreCase(Id);
                hashCode.AddObject(IsProject);

                return hashCode.CombinedHash;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as DependencyNode);
            }

            public bool Equals(DependencyNode other)
            {
                if (other == null)
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                return IsProject == other.IsProject &&
                    string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase) &&
                    WarningPropertiesCollection.Equals(other.WarningPropertiesCollection);
            }

            public override string ToString()
            {
                return $"{(IsProject ? "Project" : "Package")}/{Id}";
            }
        }

        /// <summary>
        /// A simple node class to hold the outgoing dependency edge for a quick look up.
        /// </summary>
        private class LookUpNode
        {
            // List of dependencies for this node
            public IEnumerable<LibraryDependency> Dependencies { get; set; }

            // If the node is a project then this will hold the
            public WarningPropertiesCollection WarningPropertiesCollection { get; set; }
        }
    }
}
