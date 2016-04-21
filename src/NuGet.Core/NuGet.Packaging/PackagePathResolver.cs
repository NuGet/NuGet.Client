// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    public class PackagePathResolver
    {
        private readonly bool _useSideBySidePaths;
        private readonly string _rootDirectory;

        public PackagePathResolver(string rootDirectory, bool useSideBySidePaths = true)
        {
            if (string.IsNullOrEmpty(rootDirectory))
            {
                throw new ArgumentException(
                    string.Format(Strings.StringCannotBeNullOrEmpty, nameof(rootDirectory)),
                    nameof(rootDirectory));
            }
            _rootDirectory = rootDirectory;
            _useSideBySidePaths = useSideBySidePaths;
        }

        protected internal string Root
        {
            get { return _rootDirectory; }
        }

        public string GetPackageDirectoryName(PackageIdentity packageIdentity)
        {
            var directory = GetPathBase(packageIdentity);

            return directory.ToString();
        }

        public string GetPackageFileName(PackageIdentity packageIdentity)
        {
            var fileNameBase = GetPathBase(packageIdentity);

            fileNameBase.Append(PackagingCoreConstants.NupkgExtension);

            return fileNameBase.ToString();
        }

        public string GetManifestFileName(PackageIdentity packageIdentity)
        {
            return packageIdentity.Id.ToLowerInvariant() + PackagingCoreConstants.NuspecExtension;
        }

        public string GetInstallPath(PackageIdentity packageIdentity)
        {
            return Path.Combine(_rootDirectory, GetPackageDirectoryName(packageIdentity));
        }

        public string GetInstalledPath(PackageIdentity packageIdentity)
        {
            var installedPackageFilePath = GetInstalledPackageFilePath(packageIdentity);

            return string.IsNullOrEmpty(installedPackageFilePath) ? null : Path.GetDirectoryName(installedPackageFilePath);
        }

        public string GetInstalledPackageFilePath(PackageIdentity packageIdentity)
        {
            return PackagePathHelper.GetInstalledPackageFilePath(packageIdentity, this);
        }

        private StringBuilder GetPathBase(PackageIdentity packageIdentity)
        {
            var builder = new StringBuilder();

            builder.Append(packageIdentity.Id.ToLowerInvariant());

            if (_useSideBySidePaths)
            {
                builder.Append('.');

                // Always use legacy package install path. Otherwise, restore may be broken for
                // packages like 'Microsoft.Web.Infrastructure.1.0.0.0', installed using old clients.
                builder.Append(packageIdentity.Version.ToString().ToLowerInvariant());
            }

            return builder;
        }
    }
}
