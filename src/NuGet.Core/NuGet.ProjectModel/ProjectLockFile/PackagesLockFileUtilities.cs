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
        public static bool IsNuGetLockFileSupported(PackageSpec project)
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
            return GetNuGetLockFilePath(project.BaseDirectory, projectName);
        }

        public static string GetNuGetLockFilePath(string baseDirectory, string projectName)
        {
            if (!string.IsNullOrEmpty(projectName))
            {
                var path = Path.Combine(baseDirectory, "packages." + projectName.Replace(' ', '_') + ".lock.json");

                if (File.Exists(path))
                {
                    return path;
                }
            }

            return Path.Combine(baseDirectory, PackagesLockFileFormat.LockFileName);
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

                var queue = new Queue<string>();
                var visitedP2PReference = new HashSet<string>();

                foreach (var projectReference in framework.ProjectReferences)
                {
                    if (visitedP2PReference.Add(projectReference.ProjectUniqueName))
                    {
                        queue.Enqueue(projectReference.ProjectUniqueName);

                        while (queue.Count > 0)
                        {
                            var p2pUniqueName = queue.Dequeue();
                            var p2pProjectName = Path.GetFileNameWithoutExtension(p2pUniqueName);

                            var projectDependency = target.Dependencies.FirstOrDefault(
                                dep => dep.Type == PackageDependencyType.Project &&
                                PathUtility.GetStringComparerBasedOnOS().Equals(dep.Id, p2pProjectName));

                            if (projectDependency == null)
                            {
                                // project dependency doesn't exist in lock file.
                                return false;
                            }

                            var p2pSpec = dgSpec.GetProjectSpec(p2pUniqueName);

                            // ignore if this p2p packageSpec not found in DGSpec, It'll fail restore with different error at later stage
                            if (p2pSpec != null)
                            {
                                var p2pSpecTarget = NuGetFrameworkUtility.GetNearest(p2pSpec.TargetFrameworks, framework.FrameworkName, e => e.FrameworkName);

                                // ignore if compatible framework not found for p2p reference which means current project didn't
                                // get anything transitively from this p2p
                                if (p2pSpecTarget != null)
                                {
                                    if (HasP2PDependencyChanged(p2pSpecTarget.Dependencies, projectDependency))
                                    {
                                        // P2P transitive package dependencies has changed
                                        return false;
                                    }

                                    var p2pSpecProjectRefTarget = p2pSpec.RestoreMetadata.TargetFrameworks.FirstOrDefault(
                                        t => PathUtility.GetStringComparerBasedOnOS().Equals(p2pSpecTarget.FrameworkName.GetShortFolderName(), t.FrameworkName.GetShortFolderName()));

                                    if (p2pSpecProjectRefTarget != null)
                                    {
                                        foreach (var reference in p2pSpecProjectRefTarget.ProjectReferences)
                                        {
                                            if (visitedP2PReference.Add(reference.ProjectUniqueName))
                                            {
                                                queue.Enqueue(reference.ProjectUniqueName);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }

        private static bool HasProjectDependencyChanged(IEnumerable<LibraryDependency> newDependencies, IEnumerable<LockFileDependency> lockFileDependencies)
        {
            foreach (var dependency in newDependencies.Where(dep => dep.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package))
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

        private static bool HasP2PDependencyChanged(IEnumerable<LibraryDependency> newDependencies, LockFileDependency projectDependency)
        {
            if (projectDependency == null)
            {
                // project dependency doesn't exists in lock file so it's out of sync.
                return true;
            }

            foreach (var dependency in newDependencies.Where(
                dep => (dep.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package && dep.SuppressParent != LibraryIncludeFlags.All)))
            {
                var matchedP2PLibrary = projectDependency.Dependencies.FirstOrDefault(dep => StringComparer.OrdinalIgnoreCase.Equals(dep.Id, dependency.Name));

                if (matchedP2PLibrary == null || !EqualityUtility.EqualsWithNullCheck(matchedP2PLibrary.VersionRange, dependency.LibraryRange.VersionRange))
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
