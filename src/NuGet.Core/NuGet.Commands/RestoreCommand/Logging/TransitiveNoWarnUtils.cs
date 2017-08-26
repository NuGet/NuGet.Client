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
    public static class TransitiveNoWarnUtils
    {

        /// <summary>
        /// Creates a PackageSpecificWarningProperties for a project generated by traversing the dependency graph.
        /// </summary>
        /// <param name="targetGraphs">Parent project restore target graphs.</param>
        /// <param name="parentProjectSpec">PackageSpec of the parent project.</param>
        /// <returns>WarningPropertiesCollection with the project frameworks and the transitive package specific no warn properties.</returns>
        public static WarningPropertiesCollection CreateTransitiveWarningPropertiesCollection(
            IEnumerable<RestoreTargetGraph> targetGraphs,
            PackageSpec parentProjectSpec)
        {
            var transitivePackageSpecificProperties = new PackageSpecificWarningProperties();
            var projectFrameworks = new List<NuGetFramework>();
            var parentWarningProperties = new WarningPropertiesCollection(
                    parentProjectSpec.RestoreMetadata?.ProjectWideWarningProperties,
                    PackageSpecificWarningProperties.CreatePackageSpecificWarningProperties(parentProjectSpec),
                    parentProjectSpec.TargetFrameworks.Select(f => f.FrameworkName).AsList().AsReadOnly());

            var parentPackageSpecifcNoWarn = ExtractPackageSpecificNoWarnPerFramework(
                parentWarningProperties.PackageSpecificWarningProperties);

            var warningPropertiesCache = new Dictionary<string, Dictionary<NuGetFramework, WarningPropertiesCollection>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var targetGraph in targetGraphs)
            {
                if (string.IsNullOrEmpty(targetGraph.RuntimeIdentifier))
                {
                    var transitiveNoWarnFromTargetGraph = ExtractTransitiveNoWarnProperties(
                        targetGraph,
                        parentProjectSpec.RestoreMetadata.ProjectName,
                        parentWarningProperties.ProjectWideWarningProperties.NoWarn.AsHashSet(),
                        parentPackageSpecifcNoWarn?[targetGraph.Framework],
                        warningPropertiesCache);

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
        /// <param name="parentProjectWideNoWarn">Project Wide NoWarn properties of the parent project.</param>
        /// <param name="parentPackageSpecificNoWarn">Package Specific NoWarn properties of the parent project.</param>
        /// <returns>PackageSpecificWarningProperties containing all the NoWarn's for each package seen in the graph accumulated while traversing the graph.</returns>
        private static PackageSpecificWarningProperties ExtractTransitiveNoWarnProperties(
            RestoreTargetGraph targetGraph,
            string parentProjectName,
            HashSet<NuGetLogCode> parentProjectWideNoWarn,
            Dictionary<string, HashSet<NuGetLogCode>> parentPackageSpecificNoWarn,
            Dictionary<string, Dictionary<NuGetFramework, WarningPropertiesCollection>> warningPropertiesCache)
        {
            var dependencyMapping = new Dictionary<string, LookUpNode>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<DependencyNode>();
            var seen = new HashSet<DependencyNode>();
            var resultWarningProperties = new PackageSpecificWarningProperties();
            var packageNoWarn = new Dictionary<string, HashSet<NuGetLogCode>>(StringComparer.OrdinalIgnoreCase);

            // All the packages in parent project's closure. 
            // Once we have collected data for all of these, we can exit.
            var parentPackageDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var parentTargetFramework = targetGraph.Framework;

            // Add all dependencies into a dict for a quick transitive lookup
            foreach (var dependencyGraphItem in targetGraph.Flattened)
            {
                WarningPropertiesCollection nodeWarningProperties = null;
                HashSet<NuGetLogCode> nodeProjectWideNoWarn = null;
                Dictionary<string, HashSet<NuGetLogCode>> nodePackageSpecificNoWarn = null;

                if (IsProject(dependencyGraphItem.Key.Type))
                {
                    var localMatch = (LocalMatch)dependencyGraphItem.Data.Match;
                    var nodeProjectSpec = GetNodePackageSpec(localMatch);
                    var nearestFramework = nodeProjectSpec.GetTargetFramework(parentTargetFramework).FrameworkName;

                    if (nearestFramework != null)
                    {
                        // Get the WarningPropertiesCollection from the PackageSpec
                        nodeWarningProperties = GetNodeWarningProperties(nodeProjectSpec, nearestFramework, warningPropertiesCache);

                        nodeProjectWideNoWarn = nodeWarningProperties.ProjectWideWarningProperties.NoWarn.AsHashSet();

                        var nodePackageSpecificWarningProperties = ExtractPackageSpecificNoWarnForFramework(
                            nodeWarningProperties.PackageSpecificWarningProperties,
                            nearestFramework);

                        if (nodePackageSpecificWarningProperties != null)
                        {
                            nodePackageSpecificNoWarn = nodePackageSpecificWarningProperties;
                        }
                    }
                }
                else
                {
                    parentPackageDependencies.Add(dependencyGraphItem.Key.Name);
                }

                var lookUpNode = new LookUpNode()
                {
                    Dependencies = dependencyGraphItem.Data.Dependencies,
                    NodeWarningProperties = new NodeWarningProperties(nodeProjectWideNoWarn, nodePackageSpecificNoWarn)
                };

                dependencyMapping[dependencyGraphItem.Key.Name] = lookUpNode;
            }

            // Get the direct dependencies for the parent project to seed the queue
            var parentDependencies = dependencyMapping[parentProjectName];

            // Seed the queue with the parent project's direct dependencies
            AddDependenciesToQueue(parentDependencies.Dependencies,
                queue,
                parentProjectWideNoWarn,
                parentPackageSpecificNoWarn);

            // Add the parent project to the seen set to prevent adding it back to the queue
            seen.Add(new DependencyNode(id: parentProjectName,
                isProject: true,
                projectWideNoWarn: parentProjectWideNoWarn,
                packageSpecificNoWarn: parentPackageSpecificNoWarn));

            // start taking one node from the queue and get all of it's dependencies
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (seen.Add(node) && dependencyMapping.TryGetValue(node.Id, out var nodeLookUp))
                {
                    var nodeId = node.Id;
                    var nodeIsProject = node.IsProject;

                    var nodeDependencies = nodeLookUp.Dependencies;
                    var nodeWarningProperties = nodeLookUp.NodeWarningProperties;

                    var nodeProjectWideNoWarn = nodeWarningProperties.ProjectWide;
                    var nodePackageSpecificNoWarn = nodeWarningProperties.PackageSpecific;
                    var pathWarningProperties = node.NodeWarningProperties;
                    var pathProjectWideNoWarn = pathWarningProperties.ProjectWide;
                    var pathPackageSpecificNoWarn = pathWarningProperties.PackageSpecific;

                    // If the node is a project then we need to extract the warning properties and 
                    // add those to the warning properties of the current path.
                    if (nodeIsProject)
                    {
                        // Merge the node's project wide no warn to the one in the path.
                        var mergedProjectWideNoWarn = MergeProjectWideNoWarn(pathProjectWideNoWarn, nodeProjectWideNoWarn);

                        // Merge the node's package specific no warn to the one in the path.
                        var mergedPackageSpecificNoWarn = MergePackageSpecificNoWarn(pathPackageSpecificNoWarn, nodePackageSpecificNoWarn);

                        AddDependenciesToQueue(nodeDependencies, 
                            queue, 
                            mergedProjectWideNoWarn,
                            mergedPackageSpecificNoWarn);

                    }
                    else if (parentPackageDependencies.Contains(nodeId))
                    {                 
                        // Evaluate the current path for package properties
                        var packageNoWarnFromPath = ExtractPathNoWarnProperties(pathWarningProperties, nodeId);
                        if (packageNoWarn.TryGetValue(nodeId, out var noWarnCodes))
                        {
                            // We have seen atleast one path which contained a NoWarn for the package
                            // We need to update the 
                            noWarnCodes.IntersectWith(packageNoWarnFromPath);
                        }
                        else
                        {
                            noWarnCodes = packageNoWarnFromPath;
                            packageNoWarn.Add(nodeId, noWarnCodes);
                        }

                        // Check if there was any NoWarn in the path
                        if (noWarnCodes.Count == 0)
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

                        AddDependenciesToQueue(nodeDependencies,
                            queue,
                            pathWarningProperties.ProjectWide,
                            pathWarningProperties.PackageSpecific);
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

        private static WarningPropertiesCollection GetNodeWarningProperties(
            PackageSpec nodeProjectSpec,
            NuGetFramework framework,
            Dictionary<string, Dictionary<NuGetFramework, WarningPropertiesCollection>> warningPropertiesCache)
        {
            var key = nodeProjectSpec.RestoreMetadata.ProjectPath;

            if (!warningPropertiesCache.TryGetValue(key, out var frameworkCollection))
            {
                frameworkCollection
                    = new Dictionary<NuGetFramework, WarningPropertiesCollection>(new NuGetFrameworkFullComparer());

                warningPropertiesCache[key] = frameworkCollection;
            }

            if (!frameworkCollection.TryGetValue(framework, out var collection))
            {
                collection = new WarningPropertiesCollection(
                    nodeProjectSpec.RestoreMetadata?.ProjectWideWarningProperties,
                    PackageSpecificWarningProperties.CreatePackageSpecificWarningProperties(nodeProjectSpec, framework),
                    nodeProjectSpec.TargetFrameworks.Select(f => f.FrameworkName).AsList().AsReadOnly());

                frameworkCollection.Add(framework, collection);
            }

            return collection;
        }

        private static void AddDependenciesToQueue(IEnumerable<LibraryDependency> dependencies, 
            Queue<DependencyNode> queue, 
            HashSet<NuGetLogCode> projectWideNoWarn,
            Dictionary<string, HashSet<NuGetLogCode>> packageSpecificNoWarn)
        {
            // Add all the project's dependencies to the Queue with the merged WarningPropertiesCollection
            foreach (var dependency in dependencies)
            {
                var queueNode = new DependencyNode(
                    dependency.Name,
                    IsProject(dependency.LibraryRange.TypeConstraint),
                    projectWideNoWarn,
                    packageSpecificNoWarn);

                    // Add the metadata from the parent project here.
                    queue.Enqueue(queueNode);
            }
        }

        private static PackageSpec GetNodePackageSpec(LocalMatch localMatch)
        {
            return (PackageSpec) localMatch.LocalLibrary.Items[KnownLibraryProperties.PackageSpec];
        }

        /// <summary>
        /// Extracts the no warn  codes for a libraryId from the warning properties at the node in the graph.
        /// </summary>
        /// <param name="nodeWarningProperties">warning properties at the node in the graph.</param>
        /// <param name="libraryId">libraryId for which the no warn codes have to be extracted.</param>
        /// <returns>HashSet of NuGetLogCodes containing the no warn codes for the libraryId.</returns>
        public static HashSet<NuGetLogCode> ExtractPathNoWarnProperties(
            NodeWarningProperties nodeWarningProperties,
            string libraryId)
        {
            var result = new HashSet<NuGetLogCode>();
            if (nodeWarningProperties?.ProjectWide?.Count > 0)
            {
                result.UnionWith(nodeWarningProperties.ProjectWide);
            }

            if (nodeWarningProperties?.PackageSpecific?.Count > 0 &&
                nodeWarningProperties.PackageSpecific.TryGetValue(libraryId, out var codes) &&
                codes?.Count > 0)
            {
                result.UnionWith(codes);
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
        public static HashSet<NuGetLogCode> MergeProjectWideNoWarn(
            HashSet<NuGetLogCode> first,
            HashSet<NuGetLogCode> second)
        {
            HashSet<NuGetLogCode> result = null;

            if (TryMergeNullObjects(first, second, out var merged))
            {
                result = merged;
            }
            else
            {
                // Merge NoWarn Sets.
                result = new HashSet<NuGetLogCode>(first.Concat(second));
            }

            return result;
        }

        /// <summary>
        /// Merge 2 PackageSpecific NoWarns.
        /// This method will combine the warning properties from both the collections.
        /// </summary>
        /// <param name="first">First Object to be merged.</param>
        /// <param name="second">Second Object to be merged.</param>
        /// <returns>Returns a PackageSpecificWarningProperties with the combined warning properties.
        /// Will return the reference to one of the inputs if the other input is Null.
        /// Returns a Null if both the input properties are Null. </returns>
        public static Dictionary<string, HashSet<NuGetLogCode>> MergePackageSpecificNoWarn(
            Dictionary<string, HashSet<NuGetLogCode>> first,
            Dictionary<string, HashSet<NuGetLogCode>> second)
        {
            Dictionary<string, HashSet<NuGetLogCode>> result = null;

            if (TryMergeNullObjects(first, second, out var merged))
            {
                result = merged;
            }
            else
            {
                result = new Dictionary<string, HashSet<NuGetLogCode>>(StringComparer.OrdinalIgnoreCase);

                result.UnionWith(first);
                result.UnionWith(second);

            }

            return result;
        }

        private static void UnionWith(
            this Dictionary<string, HashSet<NuGetLogCode>> result,
            Dictionary<string, HashSet<NuGetLogCode>> other)
        {
            if (other.Count > 0)
            {
                foreach (var pair in other)
                {
                    var id = pair.Key;
                    var codes = pair.Value;

                    if (codes.Count > 0)
                    {
                        if (!result.TryGetValue(id, out var resultCodes))
                        {
                            resultCodes = new HashSet<NuGetLogCode>();
                            result[id] = resultCodes;
                        }
                        resultCodes.UnionWith(codes);
                    }
                }
            }
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

            if (TryMergeNullObjects(first, second, out var merged))
            {
                result = merged;
            }
            else
            {
                result = new PackageSpecificWarningProperties();
                if (first.Properties != null)
                {
                    foreach (var codePair in first.Properties)
                    {
                        var code = codePair.Key;
                        var libraryCollection = codePair.Value;

                        foreach (var libraryPair in libraryCollection)
                        {
                            var libraryId = libraryPair.Key;
                            var frameworks = libraryPair.Value;

                            result.AddRangeOfFrameworks(code, libraryId, frameworks);
                        }
                    }
                }

                if (second.Properties != null)
                {
                    foreach (var codePair in second.Properties)
                    {
                        var code = codePair.Key;
                        var libraryCollection = codePair.Value;

                        foreach (var libraryPair in libraryCollection)
                        {
                            var libraryId = libraryPair.Key;
                            var frameworks = libraryPair.Value;

                            result.AddRangeOfFrameworks(code, libraryId, frameworks);
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
        public static bool TryMergeNullObjects<T>(T first, T second, out T merged) where T : class
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
        /// Indexes a PackageSpecificWarningProperties collection on framework.
        /// </summary>
        /// <param name="packageSpecificWarningProperties">PackageSpecificWarningProperties to be converted.</param>
        /// <returns>New dictionary containing the data of a PackageSpecificWarningProperties collection on framework.</returns>
        public static Dictionary<NuGetFramework, Dictionary<string, HashSet<NuGetLogCode>>> ExtractPackageSpecificNoWarnPerFramework(
            PackageSpecificWarningProperties packageSpecificWarningProperties)
        {
            Dictionary<NuGetFramework, Dictionary<string, HashSet<NuGetLogCode>>> result = null;

            if (packageSpecificWarningProperties?.Properties != null)
            {
                result = new Dictionary<NuGetFramework, Dictionary<string, HashSet<NuGetLogCode>>>(new NuGetFrameworkFullComparer());

                foreach (var codePair in packageSpecificWarningProperties.Properties)
                {
                    var code = codePair.Key;
                    var libraryCollection = codePair.Value;

                    foreach (var libraryPair in libraryCollection)
                    {
                        var libraryId = libraryPair.Key;
                        var frameworks = libraryPair.Value;

                        foreach (var framework in frameworks)
                        {
                            if (!result.TryGetValue(framework, out var frameworkCollection))
                            {
                                frameworkCollection = new Dictionary<string, HashSet<NuGetLogCode>>(StringComparer.OrdinalIgnoreCase);

                                result[framework] = frameworkCollection;
                            }

                            if (!frameworkCollection.TryGetValue(libraryId, out var codes))
                            {
                                codes = new HashSet<NuGetLogCode>();
                                frameworkCollection[libraryId] = codes;
                            }

                            codes.Add(code);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Indexes a PackageSpecificWarningProperties collection on framework.
        /// </summary>
        /// <param name="packageSpecificWarningProperties">PackageSpecificWarningProperties to be converted.</param>
        /// <returns>New dictionary containing the data of a PackageSpecificWarningProperties collection on framework.</returns>
        public static Dictionary<string, HashSet<NuGetLogCode>> ExtractPackageSpecificNoWarnForFramework(
            PackageSpecificWarningProperties packageSpecificWarningProperties,
            NuGetFramework framework)
        {
            Dictionary<string, HashSet<NuGetLogCode>> result = null;

            if (packageSpecificWarningProperties?.Properties != null && framework != null)
            {
                result = new Dictionary<string, HashSet<NuGetLogCode>>(StringComparer.OrdinalIgnoreCase);

                foreach (var codePair in packageSpecificWarningProperties.Properties)
                {
                    var code = codePair.Key;
                    var libraryCollection = codePair.Value;

                    foreach (var libraryPair in libraryCollection)
                    {
                        var libraryId = libraryPair.Key;
                        var frameworks = libraryPair.Value;

                        if (frameworks.Contains(framework))
                        {
                            if (!result.TryGetValue(libraryId, out var codes))
                            {
                                codes = new HashSet<NuGetLogCode>();
                                result[libraryId] = codes;
                            }
                            codes.Add(code);
                        }
                    }
                }
            }

            return result;
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

            // If a node is a project then it will hold these properties
            public NodeWarningProperties NodeWarningProperties { get; }

            public DependencyNode(string id, bool isProject, HashSet<NuGetLogCode> projectWideNoWarn, Dictionary<string, HashSet<NuGetLogCode>> packageSpecificNoWarn)
            {
                Id = id ?? throw new ArgumentNullException(nameof(id));
                NodeWarningProperties = new NodeWarningProperties(projectWideNoWarn, packageSpecificNoWarn);
                IsProject = isProject;
            }

            public DependencyNode(string id, bool isProject, NodeWarningProperties nodeWarningProperties)
            {
                Id = id ?? throw new ArgumentNullException(nameof(id));
                NodeWarningProperties = nodeWarningProperties ?? throw new ArgumentNullException(nameof(nodeWarningProperties));
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
                    EqualityUtility.EqualsWithNullCheck(NodeWarningProperties, other.NodeWarningProperties);
            }

            public override string ToString()
            {
                return $"{(IsProject ? "Project" : "Package")}/{Id}";
            }
        }

        /// <summary>
        /// A simple node class to hold the outgoing dependency edges for a quick look up.
        /// </summary>
        private class LookUpNode
        {
            // List of dependencies for this node
            public IEnumerable<LibraryDependency> Dependencies { get; set; }

            // If a node is a project then it will hold these properties
            public NodeWarningProperties NodeWarningProperties { get; set; }

        }


        /// <summary>
        /// A class to hold minimal version of project wide nowarn and package specific no warn for a project.
        /// </summary>
        public class NodeWarningProperties : IEquatable<NodeWarningProperties>
        {
            // ProjectWide NoWarn properties
            public HashSet<NuGetLogCode> ProjectWide { get; }

            // PackageSpecific NoWarn
            // We do not use framework here as DependencyNode is created per parent project framework.
            public Dictionary<string, HashSet<NuGetLogCode>> PackageSpecific { get; }


            public NodeWarningProperties(
                HashSet<NuGetLogCode> projectWide,
                Dictionary<string, HashSet<NuGetLogCode>> packageSpecific)
            {
                ProjectWide = projectWide;
                PackageSpecific = packageSpecific;
            }

            public override int GetHashCode()
            {
                var hashCode = new HashCodeCombiner();

                hashCode.AddSequence(ProjectWide);
                hashCode.AddDictionary(PackageSpecific);

                return hashCode.CombinedHash;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as NodeWarningProperties);
            }


            public bool Equals(NodeWarningProperties other)
            {
                if (other == null)
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                return EqualityUtility.SetEqualWithNullCheck(ProjectWide, other.ProjectWide) &&
                    EqualityUtility.DictionaryEquals(PackageSpecific, other.PackageSpecific, (s, o) => EqualityUtility.SetEqualWithNullCheck(s, o));
            }
        }
    }
}
