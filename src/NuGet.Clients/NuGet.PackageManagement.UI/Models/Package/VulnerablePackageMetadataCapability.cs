// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class VulnerablePackageMetadataCapability : VulnerableCapability
    {
        INuGetSearchService _nuGetSearchService;
        PackageIdentity _packageIdentity;
        IReadOnlyCollection<PackageSourceContextInfo> _packageSources;
        bool _includePrerelease;

        public VulnerablePackageMetadataCapability(INuGetSearchService nuGetSearchService,
            PackageIdentity packageIdentity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease)
        {
            _nuGetSearchService = nuGetSearchService ?? throw new ArgumentNullException(nameof(nuGetSearchService));
            _packageIdentity = packageIdentity ?? throw new ArgumentNullException(nameof(packageIdentity));
            _packageSources = packageSources ?? throw new ArgumentNullException(nameof(packageSources));
            _includePrerelease = includePrerelease;
        }

        public async override Task RefreshAsync(CancellationToken cancellationToken)
        {
            (var packageMetadata, _) = await _nuGetSearchService.GetPackageMetadataAsync(_packageIdentity, _packageSources, _includePrerelease, cancellationToken); ;
            _vulnerabilities = packageMetadata.Vulnerabilities.ToList();
        }
    }
}
