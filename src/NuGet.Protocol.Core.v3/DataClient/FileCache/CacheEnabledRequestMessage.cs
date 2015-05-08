// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;

namespace NuGet.Protocol.Core.v3.Data
{
    /// <summary>
    /// HttpRequestMessage wrapper that holds additional flags for caching
    /// </summary>
    internal sealed class CacheEnabledRequestMessage : HttpRequestMessage
    {
        private readonly DataCacheOptions _options;

        /// <summary>
        /// Request wrapper
        /// </summary>
        public CacheEnabledRequestMessage(Uri requestUri, DataCacheOptions options)
            : base(HttpMethod.Get, requestUri)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            _options = options;
        }

        public DataCacheOptions CacheOptions
        {
            get { return _options; }
        }
    }
}
