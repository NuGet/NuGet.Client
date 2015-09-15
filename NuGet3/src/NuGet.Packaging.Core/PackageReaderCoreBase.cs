// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Versioning;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// A base package reader
    /// </summary>
    public abstract class PackageReaderCoreBase : IPackageReaderCore
    {
        public abstract Stream GetStream(string path);

        public abstract IEnumerable<string> GetFiles();

        public virtual PackageIdentity GetIdentity()
        {
            return NuspecCore.GetIdentity();
        }

        public virtual NuGetVersion GetMinClientVersion()
        {
            return NuspecCore.GetMinClientVersion();
        }

        public virtual PackageType GetPackageType() => NuspecCore.GetPackageType();

        public virtual Stream GetNuspec()
        {
            var path = GetFiles().Where(f => f.EndsWith(PackagingCoreConstants.NuspecExtension, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            if (String.IsNullOrEmpty(path))
            {
                throw new PackagingException(Strings.MissingNuspec);
            }

            return GetStream(path);
        }

        /// <summary>
        /// Internal low level nuspec reader
        /// </summary>
        /// <remarks>
        /// This should be overriden and the higher level nuspec reader returned to avoid parsing
        /// the nuspec multiple times
        /// </remarks>
        protected virtual NuspecCoreReaderBase NuspecCore
        {
            get { return new NuspecCoreReader(GetNuspec()); }
        }

        protected virtual void Dispose(bool disposing)
        {
            // do nothing here
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
