// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public static class PackagesLockFileUtilities
    {
        public static bool IsNuGetLockFileEnabled(PackageSpec project)
        {
            var restorePackagesWithLockFile = project.RestoreMetadata?.RestoreLockProperties.RestorePackagesWithLockFile;
            return MSBuildStringUtility.IsTrue(restorePackagesWithLockFile) || File.Exists(GetNuGetLockFilePath(project));
        }

        public static string GetNuGetLockFilePath(PackageSpec project)
        {
            if (project.RestoreMetadata == null || project.BaseDirectory == null)
            {
                // RestoreMetadata or project BaseDirectory is not set which means it's probably called through test.
                return null;
            }

            var path = project.RestoreMetadata.RestoreLockProperties.NuGetLockFilePath;

            if (!string.IsNullOrEmpty(path))
            {
                return Path.Combine(project.BaseDirectory, path);
            }

            var projectName = Path.GetFileNameWithoutExtension(project.RestoreMetadata.ProjectPath);
            path = Path.Combine(project.BaseDirectory, "packages." + projectName.Replace(' ', '_') + ".lock.json");

            if (!File.Exists(path))
            {
                path = Path.Combine(project.BaseDirectory, PackagesLockFileFormat.LockFileName);
            }

            return path;
        }

        public static bool IsLockFileStillValid(DependencyGraphSpec dgSpec, PackagesLockFile nuGetLockFile)
        {
            var uniqueName = dgSpec.Restore.First();
            var project = dgSpec.GetProjectSpec(uniqueName);

            // Validate all the direct dependencies
            foreach (var framework in project.TargetFrameworks)
            {
                var target = nuGetLockFile.Targets.FirstOrDefault(
                    t => EqualityUtility.EqualsWithNullCheck(t.TargetFramework, framework.FrameworkName));

                if (target == null)
                {
                    // a new target found in the dgSpec so invalidate existing lock file.
                    return false;
                }

                var directDependencies = target.Dependencies.Where(dep => dep.Type == PackageDependencyType.Direct);

                if (HasProjectDependencyChanged(framework.Dependencies, directDependencies))
                {
                    // lock file is out of sync
                    return false;
                }
            }

            // Validate all P2P references
            foreach (var framework in project.RestoreMetadata.TargetFrameworks)
            {
                var target = nuGetLockFile.Targets.FirstOrDefault(
                    t => EqualityUtility.EqualsWithNullCheck(t.TargetFramework, framework.FrameworkName));

                if (target == null)
                {
                    // a new target found in the dgSpec so invalidate existing lock file.
                    return false;
                }

                var queue = new Queue<Tuple<string, string>>();
                var visitedP2PReference = new HashSet<string>();

                foreach (var projectReference in framework.ProjectReferences)
                {
                    if (visitedP2PReference.Add(projectReference.ProjectUniqueName))
                    {
                        var spec = dgSpec.GetProjectSpec(projectReference.ProjectUniqueName);
                        queue.Enqueue(new Tuple<string, string>(spec.Name, projectReference.ProjectUniqueName));

                        while (queue.Count > 0)
                        {
                            var projectNames = queue.Dequeue();
                            var p2pUniqueName = projectNames.Item2;
                            var p2pProjectName = projectNames.Item1;

                            var projectDependency = target.Dependencies.FirstOrDefault(
                                dep => dep.Type == PackageDependencyType.Project &&
                                StringComparer.OrdinalIgnoreCase.Equals(dep.Id, p2pProjectName));

                            if (projectDependency == null)
                            {
                                // project dependency doesn't exist in lock file.
                                return false;
                            }

                            var p2pSpec = dgSpec.GetProjectSpec(p2pUniqueName);

                            // The package spec not found in the dg spec. This could mean that the project does not exist anymore.
                            if (p2pSpec != null)
                            {
                                var p2pSpecTarget = NuGetFrameworkUtility.GetNearest(p2pSpec.TargetFrameworks, framework.FrameworkName, e => e.FrameworkName);

                                // No compatible framework found
                                if (p2pSpecTarget != null)
                                {
                                    var p2pSpecProjectRefTarget = p2pSpec.RestoreMetadata.TargetFrameworks.FirstOrDefault(
                                        t => EqualityUtility.EqualsWithNullCheck(p2pSpecTarget.FrameworkName, t.FrameworkName));

                                    if (p2pSpecProjectRefTarget != null) // This should never happen.
                                    {
                                        if (HasP2PDependencyChanged(p2pSpecTarget.Dependencies, p2pSpecProjectRefTarget.ProjectReferences, projectDependency, dgSpec))
                                        {
                                            // P2P transitive package dependencies have changed
                                            return false;
                                        }

                                        foreach (var reference in p2pSpecProjectRefTarget.ProjectReferences)
                                        {
                                            if (visitedP2PReference.Add(reference.ProjectUniqueName))
                                            {
                                                var referenceSpec = dgSpec.GetProjectSpec(reference.ProjectUniqueName);
                                                queue.Enqueue(new Tuple<string, string>(referenceSpec.Name, reference.ProjectUniqueName));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                }
                                else
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        private static bool HasProjectDependencyChanged(IEnumerable<LibraryDependency> newDependencies, IEnumerable<LockFileDependency> lockFileDependencies)
        {
            // If the count is not the same, something has changed.
            // Otherwise we N^2 walk below determines whether anything has changed.
            var newPackageDependencies = newDependencies.Where(dep => dep.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package);
            if(newPackageDependencies.Count() != lockFileDependencies.Count())
            {
                return true;
            }

            foreach (var dependency in newPackageDependencies)
            {
                var lockFileDependency = lockFileDependencies.FirstOrDefault(d => StringComparer.OrdinalIgnoreCase.Equals(d.Id, dependency.Name));

                if (lockFileDependency == null || !EqualityUtility.EqualsWithNullCheck(lockFileDependency.RequestedVersion, dependency.LibraryRange.VersionRange))
                {
                    // dependency has changed and lock file is out of sync.
                    return true;
                }
            }

            // no dependency changed. Lock file is still valid.
            return false;
        }

        private static bool HasP2PDependencyChanged(IEnumerable<LibraryDependency> newDependencies, IEnumerable<ProjectRestoreReference> projectRestoreReferences, LockFileDependency projectDependency, DependencyGraphSpec dgSpec)
        {
            if (projectDependency == null)
            {
                // project dependency doesn't exists in lock file so it's out of sync.
                return true;
            }

            // If the count is not the same, something has changed.
            // Otherwise we N^2 walk below determines whether anything has changed.
            var transitivelyFlowingDependencies = newDependencies.Where(
                dep => (dep.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package && dep.SuppressParent != LibraryIncludeFlags.All));

            if (transitivelyFlowingDependencies.Count() + projectRestoreReferences.Count() != projectDependency.Dependencies.Count)
            {
                return true;
            }

            foreach (var dependency in transitivelyFlowingDependencies)
            {
                var matchedP2PLibrary = projectDependency.Dependencies.FirstOrDefault(dep => StringComparer.OrdinalIgnoreCase.Equals(dep.Id, dependency.Name));

                if (matchedP2PLibrary == null || !EqualityUtility.EqualsWithNullCheck(matchedP2PLibrary.VersionRange, dependency.LibraryRange.VersionRange))
                {
                    // P2P dependency has changed and lock file is out of sync.
                    return true;
                }
            }

            foreach (var dependency in projectRestoreReferences)
            {
                var referenceSpec = dgSpec.GetProjectSpec(dependency.ProjectUniqueName);
                var matchedP2PLibrary = projectDependency.Dependencies.FirstOrDefault(dep => StringComparer.OrdinalIgnoreCase.Equals(dep.Id, referenceSpec.Name));

                if (matchedP2PLibrary == null) // Do not check the version for the projects, or else https://github.com/nuget/home/issues/7935
                {
                    // P2P dependency has changed and lock file is out of sync.
                    return true;
                }
            }

            // no dependency changed. Lock file is still valid.
            return false;
        }

    }
}
