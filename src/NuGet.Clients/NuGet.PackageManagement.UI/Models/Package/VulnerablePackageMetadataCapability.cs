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
    public class VulnerablePackageMetadataCapability : VulnerableCapabilityBase
    {
        private IPackageMetadataRetrievalAdapter _packageMetadataRetrievalAdapter;
        private PackageIdentity _packageIdentity;
        private IReadOnlyCollection<PackageSourceContextInfo> _packageSources;
        private bool _includePrerelease;

        public VulnerablePackageMetadataCapability(IPackageMetadataRetrievalAdapter packageMetadataRetrievalAdapter,
            PackageIdentity packageIdentity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease)
        {
            _packageMetadataRetrievalAdapter = packageMetadataRetrievalAdapter ?? throw new ArgumentNullException(nameof(packageMetadataRetrievalAdapter));
            _packageIdentity = packageIdentity ?? throw new ArgumentNullException(nameof(packageIdentity));
            _packageSources = packageSources ?? throw new ArgumentNullException(nameof(packageSources));
            _includePrerelease = includePrerelease;
        }

        public async override Task PopulateDataAsync(CancellationToken cancellationToken)
        {
            var packageMetadata = await _packageMetadataRetrievalAdapter.GetPackageMetadataAsync(_packageSources, _includePrerelease, cancellationToken);
            Vulnerabilities = packageMetadata.Vulnerabilities?.ToList() ?? new List<PackageVulnerabilityMetadataContextInfo>();
        }
    }
}
