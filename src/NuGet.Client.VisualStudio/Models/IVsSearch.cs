using NuGet.Client;
using NuGet.Client.VisualStudio.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio.Models
{
    public interface IVsSearch
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
         Task<IEnumerable<VisualStudioUISearchMetadata>> GetSearchResultsForVisualStudioUI(
            string searchTerm,
            SearchFilter filters,
            int skip,
            int take,
            CancellationToken cancellationToken);
    }
}
