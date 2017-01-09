using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Commands
{
    /// <summary>
    /// Retrieves pre-loaded restore requests. The inputs here have already been determined.
    /// </summary>
    public interface IPreLoadedRestoreRequestProvider
    {
        /// <summary>
        /// Create RestoreRequest objects.
        /// </summary>
        Task<IReadOnlyList<RestoreSummaryRequest>> CreateRequests(RestoreArgs restoreContext);
    }
}
