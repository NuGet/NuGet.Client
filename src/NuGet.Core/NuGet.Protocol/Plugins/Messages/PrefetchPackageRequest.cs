﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A request to a plugin to prefetch a package.
    /// </summary>
    public sealed class PrefetchPackageRequest
    {
        /// <summary>
        /// Gets the package ID.
        /// </summary>
        [JsonRequired]
        public string PackageId { get; }

        /// <summary>
        /// Gets the package source repository location.
        /// </summary>
        [JsonRequired]
        public string PackageSourceRepository { get; }

        /// <summary>
        /// Gets the package version.
        /// </summary>
        [JsonRequired]
        public string PackageVersion { get; }

        /// <summary>
        /// Initializes a new <see cref="PrefetchPackageRequest" /> class.
        /// </summary>
        /// <param name="packageSourceRepository">The package source repository location.</param>
        /// <param name="packageId">The package ID.</param>
        /// <param name="packageVersion">The package version.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="packageSourceRepository" />
        /// is either <c>null</c> or an empty string.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="packageId" />
        /// is either <c>null</c> or an empty string.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="packageVersion" />
        /// is either <c>null</c> or an empty string.</exception>
        [JsonConstructor]
        public PrefetchPackageRequest(
            string packageSourceRepository,
            string packageId,
            string packageVersion)
        {
            if (string.IsNullOrEmpty(packageSourceRepository))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(packageSourceRepository));
            }

            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(packageId));
            }

            if (string.IsNullOrEmpty(packageVersion))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(packageVersion));
            }

            PackageSourceRepository = packageSourceRepository;
            PackageId = packageId;
            PackageVersion = packageVersion;
        }
    }
}