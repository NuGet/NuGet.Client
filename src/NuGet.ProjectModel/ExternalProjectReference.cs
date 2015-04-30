using System.Collections.Generic;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Represents a reference to a project produced by an external build system, such as msbuild.
    /// </summary>
    public class ExternalProjectReference
    {
        /// <summary>
        /// The name of the external project
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The path to the nuget.json file representing the NuGet dependencies of the project
        /// </summary>
        public string PackageSpecPath { get; } 

        /// <summary>
        /// A list of other external projects this project references
        /// </summary>
        public IReadOnlyList<string> ExternalProjectReferences { get; }
    }
}