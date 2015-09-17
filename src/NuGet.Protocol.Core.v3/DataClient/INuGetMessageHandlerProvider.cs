// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using NuGet.Configuration;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Extension point for providing Http Message handlers to do proxy and authentication
    /// </summary>
    public interface INuGetMessageHandlerProvider
    {
        bool TryCreate(PackageSource source, out DelegatingHandler handler);
    }
}
