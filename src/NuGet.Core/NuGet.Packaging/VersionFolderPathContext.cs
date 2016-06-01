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
        public ILogger Logger { get; }
        public bool FixNuspecIdCasing { get; }
        public PackageSaveMode PackageSaveMode { get; }
        public bool NormalizeFileNames { get; }
        public XmlDocFileSaveMode XmlDocFileSaveMode { get; set; }

        public VersionFolderPathContext(
            PackageIdentity package,
            string packagesDirectory,
            ILogger logger,
            bool fixNuspecIdCasing,
            PackageSaveMode packageSaveMode,
            bool normalizeFileNames,
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
            Logger = logger;
            FixNuspecIdCasing = fixNuspecIdCasing;
            PackageSaveMode = packageSaveMode;
            NormalizeFileNames = normalizeFileNames;
            XmlDocFileSaveMode = xmlDocFileSaveMode;
        }
    }
}
