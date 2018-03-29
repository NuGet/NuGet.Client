// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace NuGet.Packaging
{
    public class PackageExtractionContext
    {
        public ILogger Logger { get; }

        public PackageSaveMode PackageSaveMode { get; set; }

        public XmlDocFileSaveMode XmlDocFileSaveMode { get; set; }

        public IPackageSignatureVerifier SignedPackageVerifier { get; set; }

        public SignedPackageVerifierSettings SignedPackageVerifierSettings { get; set; }

        public bool CopySatelliteFiles { get; set; } = true;

        public PackageExtractionContext(
            PackageSaveMode packageSaveMode,
            XmlDocFileSaveMode xmlDocFileSaveMode,
            ILogger logger,
            IPackageSignatureVerifier signedPackageVerifier,
            SignedPackageVerifierSettings signedPackageVerifierSettings)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            PackageSaveMode = packageSaveMode;
            XmlDocFileSaveMode = xmlDocFileSaveMode;
            SignedPackageVerifier = signedPackageVerifier;
            SignedPackageVerifierSettings = signedPackageVerifierSettings;
        }
    }
}
