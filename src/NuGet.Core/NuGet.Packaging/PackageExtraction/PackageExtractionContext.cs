﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;

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

        public PackageSaveModes PackageSaveMode { get; set; } = PackageSaveModes.Nupkg;

        public PackageExtractionContext() { }
    }

    [Flags]
    public enum PackageSaveModes
    {
        None = 0,
        Nuspec = 1,

        [SuppressMessage(
            "Microsoft.Naming",
            "CA1704:IdentifiersShouldBeSpelledCorrectly",
            MessageId = "Nupkg",
            Justification = "nupkg is the file extension of the package file")]
        Nupkg = 2
    }
}
