﻿using System;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.Protocol
{
    public class LocalPackageInfo
    {
        private readonly Lazy<NuspecReader> _nuspecHelper;
        private readonly Func<PackageReaderBase> _getPackageReader;

        /// <summary>
        /// Local nuget package.
        /// </summary>
        /// <param name="identity">Package id and version.</param>
        /// <param name="path">Path to the nupkg.</param>
        /// <param name="lastWriteTimeUtc">Last nupkg write time for publish date.</param>
        /// <param name="nuspec">Nuspec XML.</param>
        /// <param name="getPackageReader">Method to retrieve the package as a reader.</param>
        public LocalPackageInfo(
            PackageIdentity identity,
            string path,
            DateTime lastWriteTimeUtc,
            Lazy<NuspecReader> nuspec,
            Func<PackageReaderBase> getPackageReader)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (lastWriteTimeUtc == null)
            {
                throw new ArgumentNullException(nameof(lastWriteTimeUtc));
            }

            if (nuspec == null)
            {
                throw new ArgumentNullException(nameof(nuspec));
            }

            if (getPackageReader == null)
            {
                throw new ArgumentNullException(nameof(getPackageReader));
            }

            Identity = identity;
            Path = path;
            LastWriteTimeUtc = lastWriteTimeUtc;
            _nuspecHelper = nuspec;
            _getPackageReader = getPackageReader;
        }

        protected LocalPackageInfo()
        {

        }

        /// <summary>
        /// Package id and version.
        /// </summary>
        public virtual PackageIdentity Identity { get; }

        /// <summary>
        /// Nupkg or folder path.
        /// </summary>
        public virtual string Path { get; }

        /// <summary>
        /// Last file write time. This is used for the publish date.
        /// </summary>
        public virtual DateTime LastWriteTimeUtc { get; }

        /// <summary>
        /// Package reader.
        /// </summary>
        /// <remarks>This creates a new instance each time. Callers need to dispose of it.</remarks>
        public virtual PackageReaderBase GetReader()
        {
            return _getPackageReader();
        }

        /// <summary>
        /// Nuspec reader.
        /// </summary>
        public virtual NuspecReader Nuspec
        {
            get
            {
                return _nuspecHelper.Value;
            }
        }

        public virtual bool IsNupkg
        {
            get
            {
                return Path.EndsWith(PackagingCoreConstants.NupkgExtension, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
