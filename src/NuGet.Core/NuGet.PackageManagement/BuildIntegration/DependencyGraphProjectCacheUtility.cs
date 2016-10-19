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
        public static async Task<DependencyGraphSpec> GetSolutionRestoreSpec(
            IEnumerable<IDependencyGraphProject> projects,
                DependencyGraphCacheContext context)
        {
            var dgSpec = new DependencyGraphSpec();
            foreach (var project in projects)
            {
                var packageSpec = await project.GetPackageSpecAsync(context);
                
                dgSpec.AddProject(packageSpec);

                if (packageSpec.RestoreMetadata.OutputType == RestoreOutputType.NETCore ||
                    packageSpec.RestoreMetadata.OutputType == RestoreOutputType.UAP ||
                    packageSpec.RestoreMetadata.OutputType == RestoreOutputType.DotnetCliTool ||
                    packageSpec.RestoreMetadata.OutputType == RestoreOutputType.Standalone)
                {
                    dgSpec.AddRestore(packageSpec.RestoreMetadata.ProjectUniqueName);
                }
                
            }
            // Return dg file
            return dgSpec;
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
