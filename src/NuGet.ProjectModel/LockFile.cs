// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.LibraryModel;

namespace NuGet.ProjectModel
{
    public class LockFile
    {
        public bool IsLocked { get; set; }
        public int Version { get; set; }
        public IList<ProjectFileDependencyGroup> ProjectFileDependencyGroups { get; set; } = new List<ProjectFileDependencyGroup>();
        public IList<LockFileLibrary> Libraries { get; set; } = new List<LockFileLibrary>();
        public IList<LockFileTarget> Targets { get; set; } = new List<LockFileTarget>();

        public bool IsValidForPackageSpec(PackageSpec spec)
        {
            if (Version != LockFileFormat.Version)
            {
                return false;
            }

            var actualTargetFrameworks = spec.TargetFrameworks;

            // The lock file should contain dependencies for each framework plus dependencies shared by all frameworks
            if (ProjectFileDependencyGroups.Count != actualTargetFrameworks.Count() + 1)
            {
                return false;
            }

            foreach (var group in ProjectFileDependencyGroups)
            {
                IOrderedEnumerable<string> actualDependencies;
                var expectedDependencies = group.Dependencies.OrderBy(x => x);

                // If the framework name is empty, the associated dependencies are shared by all frameworks
                if (string.IsNullOrEmpty(group.FrameworkName))
                {
                    actualDependencies = spec.Dependencies.Select(x => RuntimeStyleLibraryRangeToString(x.LibraryRange)).OrderBy(x => x);
                }
                else
                {
                    var framework = actualTargetFrameworks
                        .FirstOrDefault(f =>
                            string.Equals(f.FrameworkName.ToString(), group.FrameworkName, StringComparison.OrdinalIgnoreCase));
                    if (framework == null)
                    {
                        return false;
                    }

                    actualDependencies = framework.Dependencies.Select(d => RuntimeStyleLibraryRangeToString(d.LibraryRange)).OrderBy(x => x);
                }

                if (!actualDependencies.SequenceEqual(expectedDependencies))
                {
                    return false;
                }
            }

            return true;
        }

        // DNU REFACTORING TODO: temp hack to make generated lockfile work with runtime lockfile validation
        private static string RuntimeStyleLibraryRangeToString(LibraryRange libraryRange)
        {
            var sb = new StringBuilder();
            sb.Append(libraryRange.Name);
            sb.Append(" ");

            if (libraryRange.VersionRange == null)
            {
                return sb.ToString();
            }

            var minVersion = libraryRange.VersionRange.MinVersion;
            var maxVersion = libraryRange.VersionRange.MaxVersion;

            sb.Append(">= ");

            if (libraryRange.VersionRange.IsFloating)
            {
                sb.Append(libraryRange.VersionRange.Float.ToString());
            }
            else
            {
                sb.Append(minVersion.ToString());
            }

            if (maxVersion != null)
            {
                sb.Append(libraryRange.VersionRange.IsMaxInclusive ? "<= " : "< ");
                sb.Append(maxVersion.Version.ToString());
            }

            return sb.ToString();
        }
    }
}
