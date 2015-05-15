// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
/// <summary>
    /// Abstracts the logic to get a package stream for a given package identity from a given source repository
    /// </summary>
    public static class PackageDownloader
    {
        /// <summary>
        /// Sets <paramref name="targetPackageStream" /> for a given <paramref name="packageIdentity" /> from the given
        /// <paramref name="sourceRepository" />. If successfully set, returns true. Otherwise, false.
        /// </summary>
        public static async Task GetPackageStreamAsync(SourceRepository sourceRepository, PackageIdentity packageIdentity, Stream targetPackageStream, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            using (var downloadStream = await GetDownloadStreamAsync(sourceRepository, packageIdentity, token))
            {
                token.ThrowIfCancellationRequested();

                await downloadStream.CopyToAsync(targetPackageStream);
            }
        }

        private static async Task<Stream> GetDownloadStreamAsync(SourceRepository sourceRepository, PackageIdentity packageIdentity, CancellationToken token)
        {
            var downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>(token);

            if (downloadResource == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.DownloadResourceNotFound, sourceRepository.PackageSource.Source));
            }

            var downloadStream = await downloadResource.GetStreamAsync(packageIdentity, token);
            if (downloadStream == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.DownloadStreamNotAvailable, packageIdentity, sourceRepository.PackageSource.Source));
            }

            return downloadStream;
        }
    }
}
