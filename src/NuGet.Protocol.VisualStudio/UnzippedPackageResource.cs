// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.VisualStudio
{
    /// <summary>
    /// Retrieves unzipped packages from a folder.
    /// </summary>
    public abstract class UnzippedPackageResource : INuGetResource
    {
        /// <summary>
        /// True if the nupkg exists for the unzipped resource
        /// </summary>
        public abstract bool HasNupkg(PackageIdentity package);

        /// <summary>
        /// Returns the nupkg path
        /// </summary>
        public abstract FileInfo GetNupkgFile(PackageIdentity package);

        /// <summary>
        /// Returns the root directory of the unzipped package
        /// </summary>
        public abstract DirectoryInfo GetPackageRoot(PackageIdentity package);

        /// <summary>
        /// Returns all package identities
        /// </summary>
        public abstract IEnumerable<PackageIdentity> GetPackages();
    }
}
