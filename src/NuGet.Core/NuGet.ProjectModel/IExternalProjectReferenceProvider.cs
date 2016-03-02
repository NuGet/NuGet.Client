using System.Collections.Generic;
using NuGet.ProjectModel;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Provides external project reference closures.
    /// </summary>
    public interface IExternalProjectReferenceProvider
    {
        /// <summary>
        /// Get the full p2p closure from an msbuild project path.
        /// </summary>
        IReadOnlyList<ExternalProjectReference> GetReferences(string entryPointPath);

        /// <summary>
        /// Returns all known entry points.
        /// </summary>
        IReadOnlyList<ExternalProjectReference> GetEntryPoints();
    }
}
