// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol
{
    /// <summary>
    /// How the package versions list was obtained for a package id during this restore.
    /// </summary>
    public enum VersionListFetchKind
    {
        /// <summary>
        /// The resource does not report a source (plugins, local feeds, or HTTP paths that do not go through <see cref="HttpSource"/>).
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Served from the on-disk HTTP cache (or an in-memory copy of that document).
        /// </summary>
        HttpCache,

        /// <summary>
        /// Fetched from origin during this restore.
        /// </summary>
        Network
    }
}
