// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;

namespace NuGet.Protocol
{
    /// <summary>
    /// Records whether a versions list came from the HTTP cache or from origin.
    /// Network wins if both are observed for the same id. Allocation-free (no AddOrUpdate closures).
    /// </summary>
    internal static class VersionListSourceMap
    {
        internal static void Record(
            ConcurrentDictionary<string, VersionListFetchKind> map,
            string id,
            HttpSourceResultStatus status)
        {
            VersionListFetchKind incoming = status == HttpSourceResultStatus.OpenedFromDisk
                ? VersionListFetchKind.HttpCache
                : VersionListFetchKind.Network;

            while (true)
            {
                if (map.TryGetValue(id, out VersionListFetchKind existing))
                {
                    if (existing == VersionListFetchKind.Network
                        || incoming != VersionListFetchKind.Network)
                    {
                        return;
                    }

                    if (map.TryUpdate(id, VersionListFetchKind.Network, existing))
                    {
                        return;
                    }
                }
                else if (map.TryAdd(id, incoming))
                {
                    return;
                }
            }
        }
    }
}
