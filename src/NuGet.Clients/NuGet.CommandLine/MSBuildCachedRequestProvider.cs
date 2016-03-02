using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    public class MSBuildCachedRequestProvider : MSBuildP2PRestoreRequestProvider
    {
        private readonly RestoreCommandProvidersCache _providerCache;
        private readonly MSBuildProjectReferenceProvider _projectProvider;

        public MSBuildCachedRequestProvider(
            RestoreCommandProvidersCache providerCache,
            MSBuildProjectReferenceProvider projectProvider)
            : base (providerCache)
        {
            if (providerCache == null)
            {
                throw new ArgumentNullException(nameof(providerCache));
            }

            if (projectProvider == null)
            {
                throw new ArgumentNullException(nameof(projectProvider));
            }

            _providerCache = providerCache;
            _projectProvider = projectProvider;
        }

        public override Task<IReadOnlyList<RestoreSummaryRequest>> CreateRequests(
            string inputPath,
            RestoreArgs restoreContext)
        {
            var paths = new List<string>();
            var requests = new List<RestoreSummaryRequest>();
            var rootPath = Path.GetDirectoryName(inputPath);

            // Get settings relative to the input file
            var settings = restoreContext.GetSettings(rootPath);

            var entryPoints = _projectProvider.GetEntryPoints();

            // Create a request for each top level project with project.json
            foreach (var entryPoint in entryPoints)
            {
                if (entryPoint.PackageSpecPath != null && entryPoint.MSBuildProjectPath != null)
                {
                    var request = Create(
                        entryPoint,
                        _projectProvider,
                        restoreContext,
                        settings);

                    requests.Add(request);
                }
            }

            return Task.FromResult<IReadOnlyList<RestoreSummaryRequest>>(requests);
        }

        public override Task<bool> Supports(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var supported = _projectProvider.GetReferences(path).Any();

            return Task.FromResult(supported);
        }
    }
}
