﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Packaging.Core;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Finds the download url of a nupkg
    /// </summary>
    public abstract class DownloadResource : INuGetResource
    {
        /// <summary>
        /// Downloads a package .nupkg with the provided identity. If the package is not available
        /// on the source but the source itself is not down or unavailable, the
        /// <see cref="DownloadResourceResult.Status"/> will be <see cref="DownloadResourceResultStatus.NotFound"/>.
        /// If the operation was cancelled, the <see cref="DownloadResourceResult.Status"/> will be
        /// <see cref="DownloadResourceResultStatus.Cancelled"/>.
        /// </summary>
        public abstract Task<DownloadResourceResult> GetDownloadResourceResultAsync(
            PackageIdentity identity,
            ISettings settings,
            NuGet.Common.ILogger logger,
            CancellationToken token);

        public event EventHandler<PackageProgressEventArgs> Progress;
    }
}
