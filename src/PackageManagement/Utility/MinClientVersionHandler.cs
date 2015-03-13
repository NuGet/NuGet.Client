using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace NuGet.PackageManagement
{
    public static class MinClientVersionHandler
    {
        public static void CheckMinClientVersion(Stream packageStream, PackageIdentity packageIdentity)
        {
            var packageZipArchive = new ZipArchive(packageStream);
            var packageReader = new PackageReader(packageZipArchive);
            var nuspecReader = new NuspecReader(packageReader.GetNuspec());
            var packageMinClientVersion = nuspecReader.GetMinClientVersion();
            // validate that the current version of NuGet satisfies the minVersion attribute specified in the .nuspec
            if (Constants.NuGetSemanticVersion < packageMinClientVersion)
            {
                throw new NuGetVersionNotSatisfiedException(
                    String.Format(CultureInfo.CurrentCulture, Strings.PackageMinVersionNotSatisfied, packageIdentity,
                    packageMinClientVersion.ToNormalizedString(), Constants.NuGetSemanticVersion.ToNormalizedString()));
            }
        }
    }
}
