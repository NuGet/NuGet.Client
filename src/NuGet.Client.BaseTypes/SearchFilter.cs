using System.Collections.Generic;

namespace NuGet.Client
{
    /// <summary>
    /// A search filter context that represents the UI settings
    /// </summary>
    public class SearchFilter
    {
        public IEnumerable<string> SupportedFrameworks { get; set; }

        public bool IncludePrerelease { get; set; }

        public bool IncludeDelisted { get; set; }
    }
}
