using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Commands
{
    /// <summary>
    /// In Memory dg file provider.
    /// </summary>
    public class PreLoadedRestoreRequestProvider : MSBuildP2PRestoreRequestProviderBase, IPreLoadedRestoreRequestProvider
    {
        private readonly string[] _graphLines;

        public PreLoadedRestoreRequestProvider(
            RestoreCommandProvidersCache providerCache,
            string[] graphLines)
            : base(providerCache)
        {
            _graphLines = graphLines;
        }

        public Task<IReadOnlyList<RestoreSummaryRequest>> CreateRequests(RestoreArgs restoreContext)
        {
            var requests = GetRequestsFromGraph(restoreContext, _graphLines);

            return Task.FromResult<IReadOnlyList<RestoreSummaryRequest>>(requests);
        }
    }
}
