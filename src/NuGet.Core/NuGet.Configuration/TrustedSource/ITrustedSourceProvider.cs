// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Configuration
{
    public interface ITrustedSourceProvider
    {
        /// <summary>
        /// Loads all trusted sources.
        /// </summary>
        /// <returns>IEnumerable of TrustedSource.</returns>
        IEnumerable<TrustedSource> LoadTrustedSources();

        /// <summary>
        /// Loads a trusted source corresponding to a package source.
        /// </summary>
        /// <param name="packageSourceName">Name of the PackageSource used for lookup.</param>
        /// <returns>TrustedSource corresponding to the PackageSource. Null if none found.</returns>
        TrustedSource LoadTrustedSource(string packageSourceName);

        /// <summary>
        /// Saves all trusted sources. Creates new entries or updates existing trusted source entries.
        /// </summary>
        /// <param name="sources">IEnumerable of TrustedSource to be saved.</param>
        void SaveTrustedSources(IEnumerable<TrustedSource> sources);

        /// <summary>
        /// Saves a trusted sources. Creates new entry or updates existing trusted source entry.
        /// </summary>
        /// <param name="source">TrustedSource to be saved.</param>
        void SaveTrustedSource(TrustedSource source);

        /// <summary>
        /// Deletes a trusted source.
        /// </summary>
        /// <param name="sourceName">Name of the TrustedSource to be deleted.</param>
        void DeleteTrustedSource(string sourceName);
    }
}
