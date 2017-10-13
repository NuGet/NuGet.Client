// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    public class VersionFolderPathContext
    {
        public PackageIdentity Package { get; }
        public string PackagesDirectory { get; }
        public bool IsLowercasePackagesDirectory { get; }
        public ILogger Logger { get; }
        public PackageSaveMode PackageSaveMode { get; }
        public XmlDocFileSaveMode XmlDocFileSaveMode { get; set; }

        public VersionFolderPathContext(
            PackageIdentity package,
            string packagesDirectory,
            bool isLowercasePackagesDirectory,
            ILogger logger,
            PackageSaveMode packageSaveMode,
            XmlDocFileSaveMode xmlDocFileSaveMode)
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
            Logger = logger;
            PackageSaveMode = packageSaveMode;
            XmlDocFileSaveMode = xmlDocFileSaveMode;
        }

        public VersionFolderPathContext(
            PackageIdentity package,
            string packagesDirectory,
            ILogger logger,
            PackageSaveMode packageSaveMode,
            XmlDocFileSaveMode xmlDocFileSaveMode) : this(
                package,
                packagesDirectory,
                isLowercasePackagesDirectory: true,
                logger: logger,
                packageSaveMode: packageSaveMode,
                xmlDocFileSaveMode: xmlDocFileSaveMode)
        {
        }
    }
}
