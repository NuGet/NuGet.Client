// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A get authentication credentials request
    /// </summary>
    public sealed class GetAuthenticationCredentialsRequest
    {
        /// <summary>
        /// Uri
        /// </summary>
        [JsonRequired]
        public Uri Uri { get; }

        /// <summary>
        /// isRetry
        /// </summary>
        [JsonRequired]
        public bool IsRetry { get; }

        /// <summary>
        /// IsNonInteractive
        /// </summary>
        [JsonRequired]
        public bool IsNonInteractive { get; }

        /// <summary>
        /// Create a GetAuthenticationCredentialsRequest
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="isRetry"></param>
        /// <param name="nonInteractive"></param>
        /// <exception cref="ArgumentNullException"> if <paramref name="uri"/> is null</exception>
        [JsonConstructor]
        public GetAuthenticationCredentialsRequest(Uri uri, bool isRetry, bool nonInteractive)
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            IsRetry = isRetry;
            IsNonInteractive = nonInteractive;
        }
    }
}
