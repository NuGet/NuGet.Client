using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Client
{
    /// <summary>
    /// A search filter context that represents the UI settings
    /// </summary>
    public class SearchFilter
    {
        /// <summary>
        /// Defaults
        /// </summary>
        public SearchFilter()
            : this(Enumerable.Empty<string>(), false, false)
        {

        }

        /// <summary>
        /// Search filter
        /// </summary>
        /// <param name="supportedFrameworks">filter to packages compatible with these frameworks</param>
        /// <param name="includePrerelease">allow prerelease results</param>
        /// <param name="includeDelisted">allow unlisted packages</param>
        public SearchFilter(IEnumerable<string> supportedFrameworks, bool includePrerelease, bool includeDelisted)
        {
            if (supportedFrameworks == null)
            {
                throw new ArgumentNullException("supportedFrameworks");
            }

            SupportedFrameworks = supportedFrameworks.ToArray();
            IncludeDelisted = includeDelisted;
            IncludePrerelease = includePrerelease;
        }

        /// <summary>
        /// Filter to only the list of packages compatible with these frameworks.
        /// </summary>
        public IEnumerable<string> SupportedFrameworks { get; set; }

        /// <summary>
        /// Include prerelease packages in search
        /// </summary>
        public bool IncludePrerelease { get; set; }

        /// <summary>
        /// Include unlisted packages in search
        /// </summary>
        public bool IncludeDelisted { get; set; }
    }
}
