﻿using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class ProjectRestoreReference : IEquatable<ProjectRestoreReference>
    {
        /// <summary>
        /// Project unique name.
        /// </summary>
        public string ProjectUniqueName { get; set; }

        /// <summary>
        /// Full path to the msbuild project file.
        /// </summary>
        public string ProjectPath { get; set; }

        public LibraryIncludeFlags IncludeAssets { get; set; } = LibraryIncludeFlags.All;

        public LibraryIncludeFlags ExcludeAssets { get; set; }

        public LibraryIncludeFlags PrivateAssets { get; set; } = LibraryIncludeFlagUtils.DefaultSuppressParent;

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddInt32(StringComparer.Ordinal.GetHashCode(ProjectPath));
            combiner.AddInt32(StringComparer.OrdinalIgnoreCase.GetHashCode(ProjectUniqueName));
            combiner.AddObject(IncludeAssets);
            combiner.AddObject(ExcludeAssets);
            combiner.AddObject(PrivateAssets);

            return combiner.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProjectRestoreReference);
        }

        public override string ToString()
        {
            return $"{ProjectUniqueName} : {ProjectPath}";
        }

        public bool Equals(ProjectRestoreReference other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return StringComparer.Ordinal.Equals(ProjectPath, other.ProjectPath)
                && StringComparer.OrdinalIgnoreCase.Equals(ProjectUniqueName, other.ProjectUniqueName)
                && IncludeAssets == other.IncludeAssets
                && ExcludeAssets == other.ExcludeAssets
                && PrivateAssets == other.PrivateAssets;
        }
    }
}
