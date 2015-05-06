using NuGet.Commands;
using NuGet.Configuration;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Helper class for calling DNU restore
    /// </summary>
    public static class BuildIntegratedRestoreUtility
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public static async Task RestoreForBuild(
            BuildIntegratedNuGetProject project,
            INuGetProjectContext projectContext,
            IEnumerable<string> additionalSources,
            CancellationToken token)
        {
            await Restore(project, projectContext, additionalSources, token);
        }

        public static async Task<RestoreResult> Restore(
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

                return await RestoreCore(project, projectContext, additionalSources, token);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static async Task<RestoreResult> RestoreCore(BuildIntegratedNuGetProject project,
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

            var projectReferences = await GetExternalProjectReferences(project);
            request.ExternalProjects = projectReferences.ToList();

            RestoreCommand command = new RestoreCommand(new ProjectContextLogger(projectContext));

            return await command.ExecuteAsync(request);
        }

        public static async Task<IEnumerable<ExternalProjectReference>> GetExternalProjectReferences(BuildIntegratedNuGetProject project)
        {
            var references = new List<ExternalProjectReference>();

            var projectReferences = await project.GetProjectReferenceClosure();

            return projectReferences.Select(ConvertProjectReference);
        }

        public static ExternalProjectReference ConvertProjectReference(NuGetProjectReference reference)
        {
            // this may be null for non-build integrated dependencies
            var project = reference.Project as BuildIntegratedNuGetProject;

            var references = reference.ProjectReferences.Select(projectReference =>
                projectReference.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName));

            return new ExternalProjectReference(reference.Project.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName),
                project?.JsonConfigPath,
                references);
        }
    }
}
