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
        /// Returns the <see cref="DownloadResourceResult"/> for a given <paramref name="packageIdentity" /> from the given
        /// <paramref name="sourceRepository" />.
        /// </summary>
        public static async Task<DownloadResourceResult> GetDownloadResourceResultAsync(SourceRepository sourceRepository, PackageIdentity packageIdentity, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>(token);

            if (downloadResource == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.DownloadResourceNotFound, sourceRepository.PackageSource.Source));
            }

            var downloadResourceResult = await downloadResource.GetDownloadResourceResultAsync(packageIdentity, token);
            if (downloadResourceResult == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.DownloadStreamNotAvailable, packageIdentity, sourceRepository.PackageSource.Source));
            }

            return new DownloadResourceResult(
                await GetSeekableStream(downloadResourceResult.PackageStream, token),
                downloadResourceResult.PackageReader);
        }

        private static async Task<Stream> GetSeekableStream(Stream downloadStream, CancellationToken token)
        {
            if (!downloadStream.CanSeek)
            {
                var memoryStream = new MemoryStream();
                try
                {
                    token.ThrowIfCancellationRequested();
                    await downloadStream.CopyToAsync(memoryStream);
                }
                catch
                {
                    memoryStream.Dispose();
                    throw;
                }
                finally
                {
                    downloadStream.Dispose();
                }

                memoryStream.Position = 0;
                return memoryStream;
            }

            downloadStream.Position = 0;
            return downloadStream;
        }
    }
}
