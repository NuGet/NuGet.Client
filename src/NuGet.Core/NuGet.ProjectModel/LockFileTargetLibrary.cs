// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileTargetLibrary : IEquatable<LockFileTargetLibrary>
    {
        public string Name { get; set; }

        public NuGetVersion Version { get; set; }

        public IList<PackageDependency> Dependencies { get; set; } = new List<PackageDependency>();

        public IList<string> FrameworkAssemblies { get; set; } = new List<string>();

        public IList<LockFileItem> RuntimeAssemblies { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> ResourceAssemblies { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> CompileTimeAssemblies { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> NativeLibraries { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> ContentFiles { get; set; } = new List<LockFileItem>();

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
                && Dependencies.OrderBy(dependency => dependency.Id, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(other.Dependencies.OrderBy(dependency => dependency.Id, StringComparer.OrdinalIgnoreCase))
                && FrameworkAssemblies.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(other.FrameworkAssemblies.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                && RuntimeAssemblies.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(other.RuntimeAssemblies.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
                && ResourceAssemblies.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(other.ResourceAssemblies.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
                && CompileTimeAssemblies.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(other.CompileTimeAssemblies.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
                && NativeLibraries.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(other.NativeLibraries.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
                && ContentFiles.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(other.ContentFiles.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase));
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

            return combiner.CombinedHash;
        }
    }
}
