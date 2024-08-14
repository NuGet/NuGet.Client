// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    /// <summary>
    /// Finds the download url of a nupkg
    /// </summary>
    public class LocalReadmeResource : ReadmeResource
    {
        private readonly FindLocalPackagesResource _localResource;

        public LocalReadmeResource(FindLocalPackagesResource localResource)
        {
            if (localResource == null)
            {
                throw new ArgumentNullException(nameof(localResource));
            }

            _localResource = localResource;
        }

        public override async Task<string> GetReadmeAsync(
            PackageIdentity identity,
            ILogger logger,
            CancellationToken token)
        {
            using var cacheContext = new SourceCacheContext();
            var package = _localResource.GetPackage(identity, logger, token);
            if (package is not null && !string.IsNullOrEmpty(package.Path) && package.Nuspec is not null)
            {
                var readMePath = package.Nuspec.GetReadme();

                if (!string.IsNullOrEmpty(readMePath))
                {
                    var packageDirectory = Path.GetDirectoryName(package.Path);
                    var readMeFullPath = Path.Combine(packageDirectory, readMePath);
                    var readMeFileInfo = new FileInfo(readMeFullPath);
                    if (readMeFileInfo.Exists)
                    {
                        using var readMeStreamReader = readMeFileInfo.OpenText();
                        var readMeContents = await readMeStreamReader.ReadToEndAsync();
                        return readMeContents;
                    }
                }
            }
            return null;
        }

    }
}
