// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public class TransitivePackageReferenceContextInfo : ITransitivePackageReferenceContextInfo
    {
        public TransitivePackageReferenceContextInfo(PackageIdentity identity, NuGetFramework? framework)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            // framework is null for project.json package references. 

            Identity = identity;
            Framework = framework;
        }

        internal static TransitivePackageReferenceContextInfo Create(PackageIdentity identity, NuGetFramework? framework)
        {
            return new TransitivePackageReferenceContextInfo(identity, framework);
        }

        public static TransitivePackageReferenceContextInfo Create(PackageReference packageReference)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IPackageReferenceContextInfo> TransitiveOrigins => throw new NotImplementedException();

        public PackageIdentity Identity { get; internal set; }

        public NuGetFramework? Framework { get; internal set; }

        public VersionRange? AllowedVersions { get; internal set; }

        public bool IsAutoReferenced { get; internal set; }

        public bool IsUserInstalled { get; internal set; }

        public bool IsDevelopmentDependency { get; internal set; }
    }
}
