// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Core;

namespace NuGet.VisualStudio
{
    internal class VsPackageMetadata : IVsPackageMetadata
    {
        private readonly PackageIdentity _package;

        public VsPackageMetadata(PackageIdentity package, string installPath)
            :
                this(package, string.Empty, Enumerable.Empty<string>(), string.Empty, installPath)
        {
        }

        public VsPackageMetadata(PackageIdentity package, string title, IEnumerable<string> authors, string description, string installPath)
        {
            _package = package;
            InstallPath = installPath ?? string.Empty;
            Title = title ?? package.Id;
            Authors = authors ?? Enumerable.Empty<string>();
            Description = description ?? string.Empty;
        }

        public string Id
        {
            get { return _package.Id; }
        }

        public SemanticVersion Version
        {
            get { return new SemanticVersion(_package.Version.ToNormalizedString()); }
        }

        public string VersionString
        {
            get { return _package.Version.ToString(); }
        }

        public string Title { get; }

        public IEnumerable<string> Authors { get; }

        public string Description { get; }

        public string InstallPath { get; }
    }
}
