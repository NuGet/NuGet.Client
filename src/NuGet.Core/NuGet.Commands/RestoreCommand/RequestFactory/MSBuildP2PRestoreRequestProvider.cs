using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class MSBuildP2PRestoreRequestProvider : MSBuildP2PRestoreRequestProviderBase, IRestoreRequestProvider
    {
        public MSBuildP2PRestoreRequestProvider(RestoreCommandProvidersCache providerCache)
            : base (providerCache)
        {
        }

        public virtual Task<IReadOnlyList<RestoreSummaryRequest>> CreateRequests(
            string inputPath,
            RestoreArgs restoreContext)
        {
            var lines = File.ReadAllLines(inputPath);
            var requests = GetRequestsFromGraph(restoreContext, lines);

            return Task.FromResult<IReadOnlyList<RestoreSummaryRequest>>(requests);
        }

        public virtual Task<bool> Supports(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            // True if dir or project.json file
            var result = (File.Exists(path) && path.EndsWith(".dg", StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(result);
        }
    }
}
