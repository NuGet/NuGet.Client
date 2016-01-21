// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.PackageExtraction;

namespace NuGet.Packaging
{
    public class PackageExtractionContext
    {
        public bool CopySatelliteFiles { get; set; } = true;

        /// <summary>
        /// If True package folder paths will use the non-normalized version path,
        /// even for new installs.
        /// </summary>
        public bool UseLegacyPackageInstallPath { get; set; }

        public PackageSaveMode PackageSaveMode { get; set; } = PackageSaveMode.Nupkg | PackageSaveMode.Files;

        public XmlDocFileSaveMode XmlDocFileSaveMode { get; set; } = PackageExtractionBehavior.XmlDocFileSaveMode;
    }
}
