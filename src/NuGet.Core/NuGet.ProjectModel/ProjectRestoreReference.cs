using System;
using NuGet.LibraryModel;

namespace NuGet.ProjectModel
{
    public class ProjectRestoreReference
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
            return StringComparer.Ordinal.GetHashCode(ProjectUniqueName);
        }

        public override bool Equals(object obj)
        {
            var other = obj as ProjectRestoreReference;

            return StringComparer.Ordinal.Equals(ProjectUniqueName, other?.ProjectUniqueName);
        }

        public override string ToString()
        {
            return ProjectUniqueName;
        }
    }
}
