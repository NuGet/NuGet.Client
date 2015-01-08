using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Returns raw JSON from search
    /// </summary>
    public abstract class RawSearchResource : INuGetResource
    {

        /// <summary>
        /// Gives the full search page in JSON format.
        /// </summary>
        public abstract Task<JObject> SearchPage(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the search entries 
        /// </summary>
        public abstract Task<IEnumerable<JObject>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken);
    }
}
