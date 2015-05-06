using System.Collections.Generic;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Holds ExternalProjectReference for build integrated projects.
    /// </summary>
    public class NuGetProjectReference
    {
        /// <summary>
        /// Parent project
        /// </summary>
        public NuGetProject Project { get; set; }

        /// <summary>
        /// References to other projects
        /// </summary>
        public IReadOnlyList<NuGetProject> ProjectReferences { get; }
    }
}
