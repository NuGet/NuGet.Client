// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// A public API for retrieving the list of NuGet package sources.
    /// </summary>
    [ComImport]
    [Guid("E98A1293-4F14-4CC9-8573-4E3565720AF3")]
    public interface IVsPackageSourceProvider
    {
        /// <summary>
        /// Provides the list of package sources.
        /// </summary>
        /// <param name="includeUnOfficial">Unofficial sources will be included in the results</param>
        /// <param name="includeDisabled">Disabled sources will be included in the results</param>
        /// <returns>Key: source name Value: source URI</returns>
        IEnumerable<KeyValuePair<string, string>> GetSources(bool includeUnOfficial, bool includeDisabled);

        /// <summary>
        /// Raised when sources are added, removed, disabled, or modified.
        /// </summary>
        event EventHandler SourcesChanged;
    }
}
