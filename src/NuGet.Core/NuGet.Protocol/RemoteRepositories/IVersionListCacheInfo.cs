// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol
{
    /// <summary>
    /// Reports whether a package versions list came from the HTTP cache or from origin.
    /// Used to avoid a second origin GET on refresh-on-miss.
    /// </summary>
    public interface IVersionListCacheInfo
    {
        /// <summary>
        /// Tries to get how <paramref name="id"/>'s versions list was obtained.
        /// </summary>
        /// <returns><see langword="true"/> if a lookup has been recorded; otherwise <see langword="false"/> (<paramref name="kind"/> is <see cref="VersionListFetchKind.Unknown"/>).</returns>
        bool TryGetVersionListSource(string id, out VersionListFetchKind kind);
    }
}
