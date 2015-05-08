// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Protocol.Core.Types
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
            : this(supportedFrameworks, includePrerelease, includeDelisted, Enumerable.Empty<string>())
        {
        }

        /// <summary>
        /// Search filter
        /// </summary>
        /// <param name="supportedFrameworks">filter to packages compatible with these frameworks</param>
        /// <param name="includePrerelease">allow prerelease results</param>
        /// <param name="includeDelisted">allow unlisted packages</param>
        public SearchFilter(IEnumerable<string> supportedFrameworks, bool includePrerelease, bool includeDelisted, IEnumerable<string> packageTypes)
        {
            if (supportedFrameworks == null)
            {
                throw new ArgumentNullException("supportedFrameworks");
            }

            if (packageTypes == null)
            {
                throw new ArgumentNullException("packageTypes");
            }

            SupportedFrameworks = supportedFrameworks.ToArray();
            IncludeDelisted = includeDelisted;
            IncludePrerelease = includePrerelease;
            PackageTypes = packageTypes;
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

        /// <summary>
        /// Restrict the search to certain package types.
        /// </summary>
        public IEnumerable<string> PackageTypes { get; set; }
    }
}
