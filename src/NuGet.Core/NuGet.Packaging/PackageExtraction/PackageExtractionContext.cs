// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.Packaging.PackageExtraction;

namespace NuGet.Packaging
{
    public class PackageExtractionContext
    {
        public ILogger Logger { get; private set; }
        public bool CopySatelliteFiles { get; set; } = true;

        /// <summary>
        /// If True package folder paths will use the non-normalized version path,
        /// even for new installs.
        /// </summary>
        public bool UseLegacyPackageInstallPath { get; set; }

        public PackageSaveMode PackageSaveMode { get; set; } = PackageSaveMode.Defaultv2;

        public XmlDocFileSaveMode XmlDocFileSaveMode { get; set; } = PackageExtractionBehavior.XmlDocFileSaveMode;

        public PackageExtractionContext(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            Logger = logger;
        }
    }
}
