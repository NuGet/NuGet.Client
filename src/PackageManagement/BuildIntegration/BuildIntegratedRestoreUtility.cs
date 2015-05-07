using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Helper class for calling DNU restore
    /// </summary>
    public static class BuildIntegratedRestoreUtility
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public static async Task RestoreForBuildAsync(
            BuildIntegratedNuGetProject project,
            INuGetProjectContext projectContext,
            IEnumerable<string> additionalSources,
            CancellationToken token)
        {
            await RestoreAsync(project, projectContext, additionalSources, token);
        }

        /// <summary>
        /// Restore a build integrated project and update the lock file
        /// </summary>
        /// <param name="projectContext">Logging context</param>
        /// <param name="additionalSources">repository sources</param>
        public static async Task<RestoreResult> RestoreAsync(
            BuildIntegratedNuGetProject project,
            INuGetProjectContext projectContext,
            IEnumerable<string> additionalSources,
            CancellationToken token)
        {
            // Limit to only 1 restore at a time
            await _semaphore.WaitAsync(token);

            try
            {
                token.ThrowIfCancellationRequested();

                return await RestoreCoreAsync(project, projectContext, additionalSources, token);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static async Task<RestoreResult> RestoreCoreAsync(BuildIntegratedNuGetProject project,
            INuGetProjectContext projectContext,
            IEnumerable<string> sources,
            CancellationToken token)
        {
            FileInfo file = new FileInfo(project.JsonConfigPath);

            PackageSpec spec;

            using (var configStream = file.OpenRead())
            {
                spec = JsonPackageSpecReader.GetPackageSpec(configStream, project.ProjectName, project.JsonConfigPath);
            }

            var packageSources = sources.Select(source => new PackageSource(source));
            RestoreRequest request = new RestoreRequest(spec, packageSources, BuildIntegratedProjectUtility.GetGlobalPackagesFolder());

            request.LockFilePath = BuildIntegratedProjectUtility.GetLockFilePath(file.FullName);
            request.MaxDegreeOfConcurrency = 4;

            // Find the full closure of project.json files and referenced projects
            var projectReferences = await project.GetProjectReferenceClosureAsync();
            request.ExternalProjects = projectReferences.Select(reference => ConvertProjectReference(reference)).ToList();

            RestoreCommand command = new RestoreCommand(new ProjectContextLogger(projectContext));

            // Execute the restore
            return await command.ExecuteAsync(request);
        }

        /// <summary>
        /// BuildIntegratedProjectReference -> ExternalProjectReference
        /// </summary>
        private static ExternalProjectReference ConvertProjectReference(BuildIntegratedProjectReference reference)
        {
            return new ExternalProjectReference(reference.Name, reference.PackageSpecPath, reference.ExternalProjectReferences);
        }
    }
}
