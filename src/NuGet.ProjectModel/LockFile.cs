// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFile : IEquatable<LockFile>
    {
        // Set the version to the current default for new files.
        public int Version { get; set; } = LockFileFormat.Version;
        public bool IsLocked { get; set; }
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
                    actualDependencies = spec.Dependencies.Select(x => x.LibraryRange.ToLockFileDependencyGroupString()).OrderBy(x => x);
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

                    actualDependencies = framework.Dependencies.Select(d => d.LibraryRange.ToLockFileDependencyGroupString()).OrderBy(x => x);
                }

                if (!actualDependencies.SequenceEqual(expectedDependencies))
                {
                    return false;
                }
            }

            return true;
        }

        public LockFileTarget GetTarget(NuGetFramework framework, string runtimeIdentifier)
        {
            return Targets.FirstOrDefault(t =>
                t.TargetFramework.Equals(framework) &&
                ((string.IsNullOrEmpty(runtimeIdentifier) && string.IsNullOrEmpty(t.RuntimeIdentifier) ||
                 string.Equals(runtimeIdentifier, t.RuntimeIdentifier, StringComparison.OrdinalIgnoreCase))));
        }

        public LockFileLibrary GetLibrary(string name, NuGetVersion version)
        {
            return Libraries.FirstOrDefault(l =>
                string.Equals(l.Name, name) &&
                l.Version.Equals(version));
        }

        public bool Equals(LockFile other)
        {
            if (other == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            return IsLocked == other.IsLocked
                && Version == other.Version
                && ProjectFileDependencyGroups.OrderBy(group => group.FrameworkName, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(other.ProjectFileDependencyGroups.OrderBy(
                        group => group.FrameworkName, StringComparer.OrdinalIgnoreCase))
                && Libraries.OrderBy(library => library.Name, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(other.Libraries.OrderBy(library => library.Name, StringComparer.OrdinalIgnoreCase))
                && Targets.OrderBy(target => target.Name).SequenceEqual(other.Targets.OrderBy(target => target.Name));
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LockFile);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(IsLocked);
            combiner.AddObject(Version);

            foreach (var item in ProjectFileDependencyGroups.OrderBy(
                group => group.FrameworkName, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(item);
            }

            foreach (var item in Libraries.OrderBy(library => library.Name, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(item);
            }

            foreach (var item in Targets.OrderBy(target => target.Name))
            {
                combiner.AddObject(item);
            }

            return combiner.CombinedHash;
        }
    }
}
