using System;

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
