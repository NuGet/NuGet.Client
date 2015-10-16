using NuGet.Logging;
using NuGet.Packaging.Core;
using System;
using System.Globalization;

namespace NuGet.Packaging
{
    public class VersionFolderPathContext
    {
        public PackageIdentity Package { get; }
        public string PackagesDirectory { get; }
        public ILogger Logger { get; }
        public bool FixNuspecIdCasing { get; }
        public bool ExtractNuspecOnly { get; }
        public bool NormalizeFileNames { get; }

        public VersionFolderPathContext(
            PackageIdentity package,
            string packagesDirectory,
            ILogger logger,
            bool fixNuspecIdCasing,
            bool extractNuspecOnly,
            bool normalizeFileNames)
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
            Logger = logger;
            FixNuspecIdCasing = fixNuspecIdCasing;
            ExtractNuspecOnly = extractNuspecOnly;
            NormalizeFileNames = normalizeFileNames;
        }
    }
}
