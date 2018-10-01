// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    /// <summary>
    /// A V2 path resolver.
    /// </summary>
    public class PackagePathResolver
    {
        public bool UseSideBySidePaths { get; }
        protected internal string Root { get; }

        public PackagePathResolver(string rootDirectory, bool useSideBySidePaths = true)
        {
            if (string.IsNullOrEmpty(rootDirectory))
            {
                throw new ArgumentException(
                    string.Format(Strings.StringCannotBeNullOrEmpty, nameof(rootDirectory)),
                    nameof(rootDirectory));
            }

            Root = Path.IsPathRooted(rootDirectory) ?
                rootDirectory :
                PathUtility.GetAbsolutePath(Directory.GetCurrentDirectory(), rootDirectory);
            UseSideBySidePaths = useSideBySidePaths;
        }

        public virtual string GetPackageDirectoryName(PackageIdentity packageIdentity)
        {
            var directory = GetPathBase(packageIdentity);

            return directory.ToString();
        }

        public virtual string GetPackageFileName(PackageIdentity packageIdentity)
        {
            var fileNameBase = GetPathBase(packageIdentity);

            fileNameBase.Append(PackagingCoreConstants.NupkgExtension);

            return fileNameBase.ToString();
        }

        public string GetPackageDownloadMarkerFileName(PackageIdentity packageIdentity)
        {
            var builder = new StringBuilder();

            builder.Append(GetId(packageIdentity));
            builder.Append(PackagingCoreConstants.PackageDownloadMarkerFileExtension);

            return builder.ToString();
        }

        public string GetManifestFileName(PackageIdentity packageIdentity)
        {
            return GetId(packageIdentity) + PackagingCoreConstants.NuspecExtension;
        }

        public virtual string GetInstallPath(PackageIdentity packageIdentity)
        {
            return Path.Combine(Root, GetPackageDirectoryName(packageIdentity));
        }

        public virtual string GetInstalledPath(PackageIdentity packageIdentity)
        {
            var installedPackageFilePath = GetInstalledPackageFilePath(packageIdentity);

            return string.IsNullOrEmpty(installedPackageFilePath) ? null : Path.GetDirectoryName(installedPackageFilePath);
        }

        public virtual string GetInstalledPackageFilePath(PackageIdentity packageIdentity)
        {
            return PackagePathHelper.GetInstalledPackageFilePath(packageIdentity, this);
        }

        private string GetId(PackageIdentity identity)
        {
            // We use original case for the ID (no normalization).
            return identity.Id;
        }

        private string GetVersion(PackageIdentity identity)
        {
            // We use original case for the version (no normalization).
            return identity.Version.ToString();
        }

        private StringBuilder GetPathBase(PackageIdentity packageIdentity)
        {
            var builder = new StringBuilder();

            builder.Append(GetId(packageIdentity));

            if (UseSideBySidePaths)
            {
                builder.Append('.');

                // Always use legacy package install path. Otherwise, restore may be broken for
                // packages like 'Microsoft.Web.Infrastructure.1.0.0.0', installed using old clients.
                builder.Append(GetVersion(packageIdentity));
            }

            return builder;
        }
    }
}
