using System;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace NuGet.Packaging
{
    public abstract class PackageExtractionContextBase
    {
        public ILogger Logger { get; }

        public PackageSaveMode PackageSaveMode { get; set; }

        public XmlDocFileSaveMode XmlDocFileSaveMode { get; set; }

        public SignedPackageVerifier SignedPackageVerifier { get; }

        public PackageExtractionContextBase(
            PackageSaveMode packageSaveMode,
            XmlDocFileSaveMode xmlDocFileSaveMode,
            ILogger logger,
            SignedPackageVerifier signedPackageVerifier
            )
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            PackageSaveMode = packageSaveMode;
            XmlDocFileSaveMode = xmlDocFileSaveMode;
            SignedPackageVerifier = signedPackageVerifier;
        }
    }
}
