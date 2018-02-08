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
        /// Saves trusted sources.
        /// </summary>
        /// <param name="sources">IEnumerable of TrustedSource to be saved.</param>
        void SaveTrustedSources(IEnumerable<TrustedSource> sources);

        /// <summary>
        /// Loads a trusted source corresponding to a package source.
        /// </summary>
        /// <param name="packageSourceName">Name of the PackageSource used for lookup.</param>
        /// <returns>TrustedSource corresponding to the PackageSource. Null if none found.</returns>
        TrustedSource LoadTrustedSource(string packageSourceName);

        /// <summary>
        /// Saves a trusted source.
        /// </summary>
        /// <param name="trustedSource">TrustedSource to be stored.</param>
        void SaveTrustedSource(TrustedSource trustedSource);
    }
}
