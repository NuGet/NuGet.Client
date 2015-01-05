using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio.Models
{
    public interface IPowerShellSearch
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        Task<IEnumerable<PowershellSearchMetadata>> GetSearchResultsForPowerShell(
           string searchTerm,
           SearchFilter filters,
           int skip,
           int take,
           CancellationToken cancellationToken);
    }
}
