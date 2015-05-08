// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;

namespace NuGet.Protocol.Core.v3.Data
{
    /// <summary>
    /// Request modifiers add auth and proxy settings to outgoing requests.
    /// </summary>
    /// <remarks>
    /// Modifiers should retrieve their settings and a list of package sources at creation time.
    /// They should NOT make calls to the request endpoint to determine if they should handle it.
    /// </remarks>
    public interface INuGetRequestModifier
    {
        /// <summary>
        /// Provides an opportunity to modify a request before it is sent.
        /// </summary>
        /// <param name="request">Request</param>
        void Modify(HttpRequestMessage request);
    }
}
