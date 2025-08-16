// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    public class FallbackPackagePathInfo
    {
        /// <summary>
        /// Path resolver for the root package folder containing this package.
        /// </summary>
        public VersionFolderPathResolver PathResolver { get; }

        /// <summary>
        /// Package id.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Package version.
        /// </summary>
        public NuGetVersion Version { get; }

        public FallbackPackagePathInfo(string id, NuGetVersion version, VersionFolderPathResolver resolver)
        {
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            Id = id ?? throw new ArgumentNullException(nameof(id));
            Version = version;
            PathResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }
    }
}
