﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public abstract class FindLocalPackagesResource : INuGetResource
    {
        public string Root { get; protected set; }

        public virtual bool Exists(PackageIdentity identity, ILogger logger, CancellationToken token)
        {
            return GetPackage(identity, logger, token) != null;
        }

        public virtual bool Exists(string packageId, ILogger logger, CancellationToken token)
        {
            return FindPackagesById(packageId, logger, token).Any();
        }

        public abstract LocalPackageInfo GetPackage(Uri path, ILogger logger, CancellationToken token);

        public abstract LocalPackageInfo GetPackage(PackageIdentity identity, ILogger logger, CancellationToken token);

        public abstract IEnumerable<LocalPackageInfo> FindPackagesById(string id, ILogger logger, CancellationToken token);

        public abstract IEnumerable<LocalPackageInfo> GetPackages(ILogger logger, CancellationToken token);
    }
}
