// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// id, version, original PackageReference if available.
    /// </summary>
    public class PackageCollectionItem : PackageIdentity
    {
        /// <summary>
        /// Installed package references.
        /// </summary>
        public List<PackageReference> PackageReferences { get; }

        public PackageCollectionItem(string id, NuGetVersion version, IEnumerable<PackageReference> installedReferences)
            : base(id, version)
        {
            PackageReferences = installedReferences?.ToList() ?? new List<PackageReference>();
        }
    }
}
