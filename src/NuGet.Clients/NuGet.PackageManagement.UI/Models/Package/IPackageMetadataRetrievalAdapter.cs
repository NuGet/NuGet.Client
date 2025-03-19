// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    internal interface IPackageMetadataRetrievalAdapter
    {
        public Task<PackageSearchMetadataContextInfo> GetPackageMetadataAsync(
            PackageIdentity packageIdentity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease,
            CancellationToken cancellationToken);

        public Task<PackageDeprecationMetadataContextInfo?> GetPackageDeprecationInfoAsync(
            PackageIdentity packageIdentity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease,
            CancellationToken cancellationToken);
    }
}
