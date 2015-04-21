using NuGet.Configuration;
using NuGet.ProjectModel;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Strawman.Commands
{
    public class RestoreRequest
    {
        public RestoreRequest(PackageSpec project, IEnumerable<PackageSource> sources, string packagesDirectory)
        {
            Project = project;
            Sources = sources.ToList().AsReadOnly();
            PackagesDirectory = packagesDirectory;
        }

        /// <summary>
        /// The project to perform the restore on
        /// </summary>
        public PackageSpec Project { get; }

        /// <summary>
        /// The complete list of sources to retrieve packages from (excluding caches)
        /// </summary>
        public IReadOnlyList<PackageSource> Sources { get; }

        /// <summary>
        /// The directory in which to install packages
        /// </summary>
        public string PackagesDirectory { get; }
        
        // TODO: NoCache
        // TODO: Lock/Unlock
        // TODO: ScriptExecutor
        // TODO: Parallel

    }
}