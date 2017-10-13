// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;

namespace NuGet.Packaging
{
    public class PackageExtractionV3Context : PackageExtractionContextBase
    {
        public PackageIdentity Package { get; }
        public string PackagesDirectory { get; }
        public bool IsLowercasePackagesDirectory { get; }

        public PackageExtractionV3Context(
            PackageIdentity package,
            string packagesDirectory,
            bool isLowercasePackagesDirectory,
            ILogger logger,
            PackageSaveMode packageSaveMode,
            XmlDocFileSaveMode xmlDocFileSaveMode,
            SignedPackageVerifier signedPackageVerifier) : base(
                packageSaveMode,
                xmlDocFileSaveMode,
                logger,
                signedPackageVerifier)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (string.IsNullOrEmpty(packagesDirectory))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.StringCannotBeNullOrEmpty,
                    nameof(packagesDirectory)));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            Package = package;
            PackagesDirectory = packagesDirectory;
            IsLowercasePackagesDirectory = isLowercasePackagesDirectory;
        }

        public PackageExtractionV3Context(
            PackageIdentity package,
            string packagesDirectory,
            ILogger logger,
            PackageSaveMode packageSaveMode,
            XmlDocFileSaveMode xmlDocFileSaveMode,
            SignedPackageVerifier signedPackageVerifier): this(
                package,
                packagesDirectory,
                isLowercasePackagesDirectory: true,
                logger: logger,
                packageSaveMode: packageSaveMode,
                xmlDocFileSaveMode: xmlDocFileSaveMode,
                signedPackageVerifier: signedPackageVerifier)
        {
        }
    }
}
