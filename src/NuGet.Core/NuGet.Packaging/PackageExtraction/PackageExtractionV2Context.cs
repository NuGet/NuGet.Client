// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;

namespace NuGet.Packaging
{
    public class PackageExtractionV2Context : PackageExtractionContextBase
    {
        public bool CopySatelliteFiles { get; set; } = true;

        /// <summary>
        /// If True package folder paths will use the non-normalized version path,
        /// even for new installs.
        /// </summary>
        public bool UseLegacyPackageInstallPath { get; set; }

        public PackageExtractionV2Context(ILogger logger, SignedPackageVerifier signedPackageVerifier) :
            base(PackageSaveMode.Defaultv2,
                PackageExtractionBehavior.XmlDocFileSaveMode,
                logger,
                signedPackageVerifier)
        {            
        }
    }
}
