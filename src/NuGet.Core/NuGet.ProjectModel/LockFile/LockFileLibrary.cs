// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileLibrary : IEquatable<LockFileLibrary>
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public NuGetVersion Version { get; set; }

        public bool IsServiceable { get; set; }

        public string Sha512 { get; set; }

        public IList<string> Files { get; set; } = new List<string>();

        /// <summary>
        /// Relative path to the project.json file for projects
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Relative path to the msbuild project file. Ex: xproj, csproj
        /// </summary>
        public string MSBuildProject { get; set; }

        public bool Equals(LockFileLibrary other)
        {
            if (other == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Type, other.Type, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path, other.Path, StringComparison.Ordinal)
                && string.Equals(MSBuildProject, other.MSBuildProject, StringComparison.Ordinal)
                && IsServiceable == other.IsServiceable
                && string.Equals(Sha512, other.Sha512, StringComparison.Ordinal)
                && Version == other.Version)
            {
                return Files.OrderedEquals(other.Files, s => s, StringComparer.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase);
            }

            return false;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LockFileLibrary);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddStringIgnoreCase(Name);
            combiner.AddStringIgnoreCase(Type);
            combiner.AddObject(Sha512);
            combiner.AddObject(IsServiceable);
            combiner.AddObject(Version);
            combiner.AddObject(Path);
            combiner.AddObject(MSBuildProject);

            foreach (var file in Files.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddStringIgnoreCase(file);
            }

            return combiner.CombinedHash;
        }
    }
}
