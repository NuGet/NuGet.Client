// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public sealed class InstalledAndTransitivePackages : IInstalledAndTransitivePackages
    {
        public InstalledAndTransitivePackages(IReadOnlyCollection<IPackageReferenceContextInfo>? installedPackages, IReadOnlyCollection<IPackageReferenceContextInfo>? transitivePackages)
        {
            if (installedPackages is null)
            {
                InstalledPackages = Array.Empty<IPackageReferenceContextInfo>();
            }
            else
            {
                InstalledPackages = installedPackages;
            }
            if (transitivePackages is null)
            {
                TransitivePackages = Array.Empty<IPackageReferenceContextInfo>();
            }
            else
            {
                TransitivePackages = transitivePackages;
            }
        }

        public IReadOnlyCollection<IPackageReferenceContextInfo> InstalledPackages { get; }
        public IReadOnlyCollection<IPackageReferenceContextInfo> TransitivePackages { get; }

    }
}
