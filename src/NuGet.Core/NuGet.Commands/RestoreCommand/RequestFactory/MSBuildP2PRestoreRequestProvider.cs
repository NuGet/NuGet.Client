using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class MSBuildP2PRestoreRequestProvider : IRestoreRequestProvider
    {
        private readonly RestoreCommandProvidersCache _providerCache;

        public MSBuildP2PRestoreRequestProvider(RestoreCommandProvidersCache providerCache)
        {
            _providerCache = providerCache;
        }

        public virtual Task<IReadOnlyList<RestoreSummaryRequest>> CreateRequests(
            string inputPath,
            RestoreArgs restoreContext)
        {
            var paths = new List<string>();
            var requests = new List<RestoreSummaryRequest>();

            var lines = File.ReadAllLines(inputPath);
            var msbuildProvider = new MSBuildProjectReferenceProvider(lines);

            var entryPoints = msbuildProvider.GetEntryPoints();

            // Create a request for each top level project with project.json
            foreach (var entryPoint in entryPoints)
            {
                if (entryPoint.PackageSpecPath != null && entryPoint.MSBuildProjectPath != null)
                {
                    var request = Create(
                        entryPoint,
                        msbuildProvider,
                        restoreContext);

                    requests.Add(request);
                }
            }

            return Task.FromResult<IReadOnlyList<RestoreSummaryRequest>>(requests);
        }

        public virtual Task<bool> Supports(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            // True if dir or project.json file
            var result = (File.Exists(path) && path.EndsWith(".msbuildp2p", StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(result);
        }

        protected virtual RestoreSummaryRequest Create(
            ExternalProjectReference project,
            MSBuildProjectReferenceProvider msbuildProvider,
            RestoreArgs restoreContext)
        {
            // Get settings relative to the input file
            var rootPath = Path.GetDirectoryName(project.PackageSpecPath);
            var settings = restoreContext.GetSettings(rootPath);
            var globalPath = restoreContext.GetEffectiveGlobalPackagesFolder(rootPath, settings);

            var sources = restoreContext.GetEffectiveSources(settings);

            var sharedCache = _providerCache.GetOrCreate(
                globalPath,
                sources,
                restoreContext.CacheContext,
                restoreContext.Log);

            var request = new RestoreRequest(
                project.PackageSpec,
                sharedCache,
                restoreContext.Log,
                disposeProviders: false);

            restoreContext.ApplyStandardProperties(request);

            // Find all external references
            var externalReferences = msbuildProvider.GetReferences(project.MSBuildProjectPath).ToList();
            request.ExternalProjects = externalReferences;

            // Determine if this needs to fall back to an older lock file format
            request.LockFileVersion = LockFileUtilities.GetLockFileVersion(externalReferences);

            var summaryRequest = new RestoreSummaryRequest(
                request,
                project.MSBuildProjectPath,
                settings,
                sources);

            return summaryRequest;
        }
    }
}
