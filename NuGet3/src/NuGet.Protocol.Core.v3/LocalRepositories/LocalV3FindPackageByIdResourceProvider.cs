// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3.LocalRepositories
{
    /// <summary>
    /// A v3-style package repository that has expanded packages.
    /// </summary>
    public class LocalV3FindPackageByIdResourceProvider : ResourceProvider
    {
        public LocalV3FindPackageByIdResourceProvider()
            : base(typeof(FindPackageByIdResource), nameof(LocalV3FindPackageByIdResourceProvider))
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

            if (Directory.Exists(source.PackageSource.Source)
                &&
                Directory.EnumerateFiles(source.PackageSource.Source, "*.nupkg").Any())
            {
                return Task.FromResult(Tuple.Create(false, resource));
            }

            resource = new LocalV3FindPackageByIdResource(source.PackageSource);
            return Task.FromResult(Tuple.Create(true, resource));
        }
    }
}
