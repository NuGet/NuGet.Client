// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Finds the download url of a nupkg
    /// </summary>
    public abstract class ReadmeResource : INuGetResource
    {
        public abstract Task<string> GetReadmeAsync(
            PackageIdentity identity,
            ILogger logger,
            CancellationToken token);

    }
}
