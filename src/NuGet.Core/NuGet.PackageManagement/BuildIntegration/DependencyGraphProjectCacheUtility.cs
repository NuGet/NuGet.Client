// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement
{
    public static class DependencyGraphProjectCacheUtility
    {
        /// <summary>
        /// Creates an index of the project unique name to the cache entry.
        /// The cache entry contains the project and the closure of project.json files.
        /// </summary>
        public static async Task<Dictionary<string, DependencyGraphProjectCacheEntry>>
            CreateDependencyGraphProjectCache(
                IEnumerable<IDependencyGraphProject> projects,
                ExternalProjectReferenceContext context)
        {
            var cache = new Dictionary<string, DependencyGraphProjectCacheEntry>();

            // Find all project closures
            foreach (var project in projects)
            {
                // Get all project.json file paths in the closure
                var closure = await project.GetProjectReferenceClosureAsync(context);

                var files = new HashSet<string>(StringComparer.Ordinal);

                // Store the last modified date of the project.json file
                // If there are any changes a restore is needed
                var lastModified = project.LastModified;

                foreach (var reference in closure)
                {
                    if (!string.IsNullOrEmpty(reference.MSBuildProjectPath))
                    {
                        files.Add(reference.MSBuildProjectPath);
                    }

                    if (!string.IsNullOrEmpty(reference.ProjectJsonPath))
                    {
                        files.Add(reference.ProjectJsonPath);
                    }
                }

                var projectInfo = new DependencyGraphProjectCacheEntry(files, lastModified);
                var projectPath = project.MSBuildProjectPath;

                if (!cache.ContainsKey(projectPath))
                {
                    cache.Add(projectPath, projectInfo);
                }
                else
                {
                    Debug.Fail("project list contains duplicate projects");
                }
            }

            return cache;
        }

        /// <summary>
        /// Verifies that the caches contain the same projects and that each project contains the same closure.
        /// This is used to detect if any projects have changed before verifying the lock files.
        /// </summary>
        public static bool CacheHasChanges(
            IReadOnlyDictionary<string, DependencyGraphProjectCacheEntry> previousCache,
            IReadOnlyDictionary<string, DependencyGraphProjectCacheEntry> currentCache)
        {
            if (previousCache == null)
            {
                return true;
            }

            foreach (var item in currentCache)
            {
                var projectName = item.Key;
                DependencyGraphProjectCacheEntry projectInfo;
                if (!previousCache.TryGetValue(projectName, out projectInfo))
                {
                    // A new project was added, this needs a restore
                    return true;
                }

                if (item.Value.ProjectConfigLastModified?.Equals(projectInfo.ProjectConfigLastModified) != true)
                {
                    // project.json has been modified
                    return true;
                }

                if (!item.Value.ReferenceClosure.SetEquals(projectInfo.ReferenceClosure))
                {
                    // The project closure has changed
                    return true;
                }
            }

            // no project changes have occurred
            return false;
        }

        /// <summary>
        /// Find direct project references from a larger set of references.
        /// </summary>
        public static ISet<ExternalProjectReference> GetDirectReferences(
            string rootUniqueName,
            ISet<ExternalProjectReference> references)
        {
            var directReferences = new HashSet<ExternalProjectReference>();
            var uniqueNameToReference = references.ToLookup(x => x.UniqueName, StringComparer.Ordinal);

            var root = uniqueNameToReference[rootUniqueName].FirstOrDefault();
            if (root == null)
            {
                return directReferences;
            }

            foreach (var uniqueName in root.ExternalProjectReferences)
            {
                var directReference = uniqueNameToReference[uniqueName].FirstOrDefault();
                if (directReference != null)
                {
                    directReferences.Add(directReference);
                }
            }

            return directReferences;
        }

        /// <summary>
        /// Find the project closure from a larger set of references.
        /// </summary>
        public static ISet<ExternalProjectReference> GetExternalClosure(
            string rootUniqueName,
            ISet<ExternalProjectReference> references)
        {
            var closure = new HashSet<ExternalProjectReference>();

            // Start with the parent node
            var parent = references.FirstOrDefault(project =>
                    rootUniqueName.Equals(project.UniqueName, StringComparison.Ordinal));

            if (parent != null)
            {
                closure.Add(parent);
            }

            // Loop adding child projects each time
            var notDone = true;
            while (notDone)
            {
                notDone = false;

                foreach (var childName in closure
                    .Where(project => project.ExternalProjectReferences != null)
                    .SelectMany(project => project.ExternalProjectReferences)
                    .ToArray())
                {
                    var child = references.FirstOrDefault(project =>
                        childName.Equals(project.UniqueName, StringComparison.Ordinal));

                    // Continue until nothing new is added
                    if (child != null)
                    {
                        notDone |= closure.Add(child);
                    }
                }
            }

            return closure;
        }
    }
}
