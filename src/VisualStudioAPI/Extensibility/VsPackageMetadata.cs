extern alias Legacy;
using LegacyNuGet = Legacy.NuGet;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.VisualStudio
{
    internal class VsPackageMetadata : IVsPackageMetadata
    {
        private readonly PackageIdentity _package;
        private readonly string _installPath;
        private readonly string _title;
        private readonly IEnumerable<string> _authors;
        private readonly string _description;

        public VsPackageMetadata(PackageIdentity package, string installPath) :
            this(package, string.Empty, Enumerable.Empty<string>(), string.Empty, installPath)
        {
        }

        public VsPackageMetadata(PackageIdentity package, string title, IEnumerable<string> authors, string description, string installPath)
        {
            _package = package;
            _installPath = installPath ?? string.Empty;
            _title = title ?? package.Id;
            _authors = authors ?? Enumerable.Empty<string>();
            _description = description ?? string.Empty;
        }

        public string Id
        {
            get { return _package.Id; }
        }

        public LegacyNuGet.SemanticVersion Version
        {
            get { return new LegacyNuGet.SemanticVersion(_package.Version.ToNormalizedString()); }
        }

        public string VersionString
        {
            get { return _package.Version.ToString(); }
        }

        public string Title
        {
            get { return _title; }
        }

        public IEnumerable<string> Authors
        {
            get { return _authors; }
        }

        public string Description
        {
            get { return _description; }
        }

        public string InstallPath
        {
            get { return _installPath; }
        }
    }
}
