using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Configuration;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class MSBuildP2PRestoreRequestProviderBase
    {
        private readonly RestoreCommandProvidersCache _providerCache;

        public MSBuildP2PRestoreRequestProviderBase(RestoreCommandProvidersCache providerCache)
        {
            _providerCache = providerCache;
        }

        protected virtual RestoreSummaryRequest Create(
            ExternalProjectReference project,
            MSBuildProjectReferenceProvider msbuildProvider,
            RestoreArgs restoreContext,
            ISettings settingsOverride)
        {
            // Get settings relative to the input file
            var rootPath = Path.GetDirectoryName(project.PackageSpecPath);

            var settings = settingsOverride;

            if (settings == null)
            {
                settings = restoreContext.GetSettings(rootPath);
            }

            var globalPath = restoreContext.GetEffectiveGlobalPackagesFolder(rootPath, settings);
            var fallbackPaths = restoreContext.GetEffectiveFallbackPackageFolders(settings);

            var sources = restoreContext.GetEffectiveSources(settings);

            var sharedCache = _providerCache.GetOrCreate(
                globalPath,
                fallbackPaths,
                sources,
                restoreContext.CacheContext,
                restoreContext.Log);

            var request = new RestoreRequest(
                project.PackageSpec,
                sharedCache,
                restoreContext.CacheContext,
                restoreContext.Log);

            restoreContext.ApplyStandardProperties(request);

            // Find all external references
            var externalReferences = msbuildProvider.GetReferences(project.MSBuildProjectPath).ToList();
            request.ExternalProjects = externalReferences;

            // Set output type
            if (StringComparer.OrdinalIgnoreCase.Equals("netcore", GetPropertyValue(project, "RestoreOutputType")))
            {
                request.RestoreOutputType = RestoreOutputType.NETCore;
                request.RestoreOutputPath = GetPropertyValue(project, "RestoreOutputPath");
                request.LockFilePath = Path.Combine(request.RestoreOutputPath, "project.assets.json");
            }

            // The lock file is loaded later since this is an expensive operation

            var summaryRequest = new RestoreSummaryRequest(
                request,
                project.MSBuildProjectPath,
                settings,
                sources);

            return summaryRequest;
        }

        protected List<RestoreSummaryRequest> GetRequestsFromGraph(RestoreArgs restoreContext, string[] lines)
        {
            var requests = new List<RestoreSummaryRequest>();
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
                        restoreContext,
                        settingsOverride: null);

                    requests.Add(request);
                }
            }

            return requests;
        }

        private static string GetPropertyValue(ExternalProjectReference project, string key)
        {
            List<string> restoreOutputType;
            if (project.Properties.TryGetValue(key, out restoreOutputType))
            {
                var value = restoreOutputType.FirstOrDefault();

                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
