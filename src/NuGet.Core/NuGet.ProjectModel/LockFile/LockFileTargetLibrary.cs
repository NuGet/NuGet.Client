// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileTargetLibrary : IEquatable<LockFileTargetLibrary>
    {
        public string Name { get; set; }

        public string Framework { get; set; }

        public NuGetVersion Version { get; set; }

        public string Type { get; set; }

        public ISet<PackageDependency> Dependencies { get; set; } = new HashSet<PackageDependency>();

        public IList<string> FrameworkAssemblies { get; set; } = new List<string>();

        public IList<LockFileItem> RuntimeAssemblies { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> ResourceAssemblies { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> CompileTimeAssemblies { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> NativeLibraries { get; set; } = new List<LockFileItem>();

        public IList<LockFileContentFile> ContentFiles { get; set; } = new List<LockFileContentFile>();

        public IList<LockFileRuntimeTarget> RuntimeTargets { get; set; } = new List<LockFileRuntimeTarget>();

        public bool Equals(LockFileTargetLibrary other)
        {
            if (other == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Name, other.Name)
                && VersionComparer.Default.Equals(Version, other.Version)
                && string.Equals(Type, other.Type, StringComparison.Ordinal)
                && string.Equals(Framework, other.Framework, StringComparison.Ordinal)
                && Dependencies.OrderedEquals(other.Dependencies, dependency => dependency.Id, StringComparer.OrdinalIgnoreCase)
                && FrameworkAssemblies.OrderedEquals(other.FrameworkAssemblies, s => s, StringComparer.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase)
                && RuntimeAssemblies.OrderedEquals(other.RuntimeAssemblies, item => item.Path, StringComparer.OrdinalIgnoreCase)
                && ResourceAssemblies.OrderedEquals(other.ResourceAssemblies, item => item.Path, StringComparer.OrdinalIgnoreCase)
                && CompileTimeAssemblies.OrderedEquals(other.CompileTimeAssemblies, item => item.Path, StringComparer.OrdinalIgnoreCase)
                && NativeLibraries.OrderedEquals(other.NativeLibraries, item => item.Path, StringComparer.OrdinalIgnoreCase)
                && ContentFiles.OrderedEquals(other.ContentFiles, item => item.Path, StringComparer.OrdinalIgnoreCase)
                && RuntimeTargets.OrderedEquals(other.RuntimeTargets, item => item.Path, StringComparer.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LockFileTargetLibrary);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(Name);
            combiner.AddObject(Version);
            combiner.AddObject(Type);
            combiner.AddObject(Framework);

            foreach (var dependency in Dependencies.OrderBy(dependency => dependency.Id, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(dependency);
            }

            foreach (var reference in FrameworkAssemblies.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddStringIgnoreCase(reference);
            }

            foreach (var item in RuntimeAssemblies.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(item);
            }

            foreach (var item in ResourceAssemblies.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(item);
            }

            foreach (var item in CompileTimeAssemblies.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(item);
            }

            foreach (var item in NativeLibraries.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(item);
            }

            foreach (var item in ContentFiles.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(item);
            }

            foreach (var item in RuntimeTargets.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddObject(item);
            }

            return combiner.CombinedHash;
        }
    }
}
