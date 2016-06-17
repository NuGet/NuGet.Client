﻿using System.Collections.Generic;

namespace NuGet.Common
{
    /// <summary>
    /// Common NuGet paths. These values may be overridden in NuGet.Config or by 
    /// environment variables, resolving the paths here requires NuGet.Configuration.
    /// </summary>
    public interface INuGetPathContext
    {
        /// <summary>
        /// User package folder directory.
        /// </summary>
        string UserPackageFolder { get; }

        /// <summary>
        /// Fallback package folder locations.
        /// </summary>
        IReadOnlyList<string> FallbackPackageFolders { get; }

        /// <summary>
        /// Http file cache.
        /// </summary>
        string HttpCacheFolder { get; }
    }
}
