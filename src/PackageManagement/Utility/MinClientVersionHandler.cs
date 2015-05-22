// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.IO.Compression;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.PackageManagement
{
    public static class MinClientVersionHandler
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope",
            Justification = "Disposing the PackageReader will dispose the backing stream that we want to leave open.")]
        public static void CheckMinClientVersion(DownloadResourceResult downloadResourceResult, PackageIdentity packageIdentity)
        {
            NuGetVersion packageMinClientVersion;
            if (downloadResourceResult.PackageReader != null)
            {
                packageMinClientVersion = downloadResourceResult.PackageReader.GetMinClientVersion();
            }
            else
            {
                var packageZipArchive = new ZipArchive(downloadResourceResult.PackageStream);
                var packageReader = new PackageReader(packageZipArchive);
                var nuspecReader = new NuspecReader(packageReader.GetNuspec());
                packageMinClientVersion = nuspecReader.GetMinClientVersion();
            }

            // validate that the current version of NuGet satisfies the minVersion attribute specified in the .nuspec
            if (Constants.NuGetSemanticVersion < packageMinClientVersion)
            {
                throw new NuGetVersionNotSatisfiedException(
                    string.Format(CultureInfo.CurrentCulture, Strings.PackageMinVersionNotSatisfied, packageIdentity,
                        packageMinClientVersion.ToNormalizedString(), Constants.NuGetSemanticVersion.ToNormalizedString()));
            }
        }
    }
}
