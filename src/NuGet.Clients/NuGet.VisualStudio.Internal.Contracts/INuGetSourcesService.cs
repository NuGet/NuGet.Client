// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public interface INuGetSourcesService : IDisposable
    {
        ValueTask<IReadOnlyList<PackageSource>> GetPackageSourcesAsync(CancellationToken cancellationToken);
#pragma warning disable CS0618 // Type or member is obsolete
        ValueTask SavePackageSourcesAsync(IReadOnlyList<PackageSource> sources, PackageSourceUpdateOptions packageSourceUpdateOptions, CancellationToken cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
