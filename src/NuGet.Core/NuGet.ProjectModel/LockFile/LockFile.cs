// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFile : IEquatable<LockFile>
    {
        // Tools run under a hard coded framework.
        public static readonly NuGetFramework ToolFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;

        public static readonly char DirectorySeparatorChar = '/';

        // Set the version to the current default for new files.
        public int Version { get; set; } = LockFileFormat.Version;
        public string Path { get; set; }
        public bool IsLocked { get; set; }
        public IList<ProjectFileDependencyGroup> ProjectFileDependencyGroups { get; set; } = new List<ProjectFileDependencyGroup>();
        public IList<LockFileLibrary> Libraries { get; set; } = new List<LockFileLibrary>();
        public IList<LockFileTarget> Targets { get; set; } = new List<LockFileTarget>();
        public IList<LockFileTarget> Tools { get; set; } = new List<LockFileTarget>();
        public IList<ProjectFileDependencyGroup> ProjectFileToolGroups { get; set; } = new List<ProjectFileDependencyGroup>();

        public bool IsValidForPackageSpec(PackageSpec spec)
        {
            return IsValidForPackageSpec(spec, Version);
        }

        public bool IsValidForPackageSpec(PackageSpec spec, int requestLockFileVersion)
        {
            if (Version != requestLockFileVersion)
            {
                return false;
            }

            if (!ValidateDependencies(spec))
            {
                return false;
            }

            if (!ValidateTools(spec))
            {
                return false;
            }

            return true;
        }

        private bool ValidateDependencies(PackageSpec spec)
        {
            var actualTargetFrameworks = spec.TargetFrameworks;

            // The lock file should contain dependencies for each framework plus dependencies shared by all frameworks
            if (ProjectFileDependencyGroups.Count != actualTargetFrameworks.Count() + 1)
            {
                return false;
            }

            foreach (var group in ProjectFileDependencyGroups)
            {
                IOrderedEnumerable<string> actualDependencies;
                var expectedDependencies = @group.Dependencies.OrderBy(x => x, StringComparer.Ordinal);

                // If the framework name is empty, the associated dependencies are shared by all frameworks
                if (string.IsNullOrEmpty(@group.FrameworkName))
                {
                    actualDependencies = spec.Dependencies
                        .Select(x => x.LibraryRange.ToLockFileDependencyGroupString())
                        .OrderBy(x => x, StringComparer.Ordinal);
                }
                else
                {
                    var framework = actualTargetFrameworks.FirstOrDefault(f => string.Equals(
                                f.FrameworkName.DotNetFrameworkName,
                                @group.FrameworkName,
                                StringComparison.OrdinalIgnoreCase));

                    if (framework == null)
                    {
                        return false;
                    }

                    actualDependencies = framework
                        .Dependencies
                        .Select(d => d.LibraryRange.ToLockFileDependencyGroupString())
                        .OrderBy(x => x, StringComparer.Ordinal);
                }

                if (!actualDependencies.SequenceEqual(expectedDependencies))
                {
                    return false;
                }
            }
            return true;
        }

        private bool ValidateTools(PackageSpec spec)
        {
            // Skip this check if there are no tools at all.
            if (ProjectFileToolGroups.Count == 0 && spec.Tools.Count == 0)
            {
                return true;
            }

            // The lock file should only contain tools for a single framework
            if (ProjectFileToolGroups.Count != 1)
            {
                return false;
            }

            var group = ProjectFileToolGroups.First();
            if (!StringComparer.OrdinalIgnoreCase.Equals(
                group.FrameworkName,
                ToolFramework.ToString()))
            {
                return false;
            }

            var lockDependencies = group
                .Dependencies
                .OrderBy(x => x, StringComparer.Ordinal);

            var specDependencies = spec.Tools
                .Select(x => x.LibraryRange.ToLockFileDependencyGroupString())
                .OrderBy(x => x, StringComparer.Ordinal);

            if (!specDependencies.SequenceEqual(lockDependencies))
            {
                return false;
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
                && ProjectFileDependencyGroups.OrderedEquals(other.ProjectFileDependencyGroups, group => group.FrameworkName, StringComparer.OrdinalIgnoreCase)
                && Libraries.OrderedEquals(other.Libraries, library => library.Name, StringComparer.OrdinalIgnoreCase)
                && Targets.OrderedEquals(other.Targets, target => target.Name, StringComparer.Ordinal)
                && ProjectFileToolGroups.OrderedEquals(other.ProjectFileToolGroups, group => group.FrameworkName, StringComparer.OrdinalIgnoreCase)
                && Tools.OrderedEquals(other.Tools, target => target.Name, StringComparer.Ordinal);
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

            HashProjectFileDependencyGroups(combiner, ProjectFileDependencyGroups);

            foreach (var item in Libraries.OrderBy(library => library.Name, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(item);
            }

            HashLockFileTargets(combiner, Targets);
            HashProjectFileDependencyGroups(combiner, ProjectFileToolGroups);
            HashLockFileTargets(combiner, Tools);

            return combiner.CombinedHash;
        }

        private static void HashLockFileTargets(HashCodeCombiner combiner, IList<LockFileTarget> targets)
        {
            foreach (var item in targets.OrderBy(target => target.Name, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(item);
            }
        }

        private static void HashProjectFileDependencyGroups(HashCodeCombiner combiner, IList<ProjectFileDependencyGroup> groups)
        {
            foreach (var item in groups.OrderBy(
                group => @group.FrameworkName, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(item);
            }
        }
    }
}
