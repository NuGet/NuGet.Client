// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3.LocalRepositories
{
    /// <summary>
    /// A v2-style package repository that has nupkgs at the root.
    /// </summary>
    public class LocalV2FindPackageByIdResourceProvider : ResourceProvider
    {
        private readonly ConcurrentDictionary<string, List<CachedPackageInfo>> _packageInfoCache =
            new ConcurrentDictionary<string, List<CachedPackageInfo>>(StringComparer.Ordinal);

        public LocalV2FindPackageByIdResourceProvider()
            : base(typeof(FindPackageByIdResource),
                  nameof(LocalV2FindPackageByIdResourceProvider),
                  before: nameof(LocalV3FindPackageByIdResourceProvider))
        {
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            INuGetResource resource = null;

            Uri uri;
            if (!Uri.TryCreate(source.PackageSource.Source, UriKind.Absolute, out uri)
                ||
                !uri.IsFile)
            {
                return Task.FromResult(Tuple.Create(false, resource));
            }

            if (!LocalV2FindPackageByIdResource.GetNupkgFiles(source.PackageSource.Source, id: string.Empty).Any())
            {
                return Task.FromResult(Tuple.Create(false, resource));
            }

            resource = new LocalV2FindPackageByIdResource(source.PackageSource, _packageInfoCache);
            return Task.FromResult(Tuple.Create(true, resource));
        }
    }
}
