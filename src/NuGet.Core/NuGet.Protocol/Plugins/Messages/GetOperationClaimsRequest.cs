// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A query to a plugin about which operations it supports for a specific package source.
    /// </summary>
    public sealed class GetOperationClaimsRequest
    {
        /// <summary>
        /// Gets the package source location for the <see cref="ServiceIndex" />.
        /// </summary>
        [JsonRequired]
        public string PackageSourceRepository { get; }

        /// <summary>
        /// Gets the service index (index.json) for the <see cref="PackageSourceRepository" />.
        /// </summary>
        [JsonRequired]
        public JObject ServiceIndex { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetOperationClaimsRequest" /> class.
        /// </summary>
        /// <param name="packageSourceRepository">The package source location.</param>
        /// <param name="serviceIndex">The service index (index.json).</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="packageSourceRepository" /> is either
        /// <c>null</c> or an empty string.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceIndex" /> is <c>null</c>.</exception>
        [JsonConstructor]
        public GetOperationClaimsRequest(string packageSourceRepository, JObject serviceIndex)
        {
            if (string.IsNullOrEmpty(packageSourceRepository))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(packageSourceRepository));
            }

            if (serviceIndex == null)
            {
                throw new ArgumentNullException(nameof(serviceIndex));
            }

            PackageSourceRepository = packageSourceRepository;
            ServiceIndex = serviceIndex;
        }
    }
}