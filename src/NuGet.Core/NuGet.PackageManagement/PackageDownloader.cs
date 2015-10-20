// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using System.IO.Compression;
using NuGet.Packaging;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Abstracts the logic to get a package stream for a given package identity from a given source repository
    /// </summary>
    public static class PackageDownloader
    {
        /// <summary>
        /// Returns the <see cref="DownloadResourceResult"/> for a given <paramref name="packageIdentity" />
        /// from the given <paramref name="sources" />.
        /// </summary>
        public static async Task<DownloadResourceResult> GetDownloadResourceResultAsync(IEnumerable<SourceRepository> sources,
            PackageIdentity packageIdentity,
            Configuration.ISettings settings,
            CancellationToken token)
        {
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                // Create a group of local sources that will go first, then everything else.
                var groups = new Queue<List<SourceRepository>>();
                var localGroup = new List<SourceRepository>();
                var otherGroup = new List<SourceRepository>();
                groups.Enqueue(localGroup);
                groups.Enqueue(otherGroup);

                foreach (var source in sources)
                {
                    if (source.PackageSource.IsLocal)
                    {
                        localGroup.Add(source);
                    }
                    else
                    {
                        otherGroup.Add(source);
                    }
                }

                while (groups.Count > 0)
                {
                    token.ThrowIfCancellationRequested();

                    var sourceGroup = groups.Dequeue();

                    var tasks = sourceGroup.Select(s =>
                        GetDownloadResourceResultAsync(s, packageIdentity, settings, linkedTokenSource.Token))
                        .ToList();

                    while (tasks.Any())
                    {
                        var completedTask = await Task.WhenAny(tasks);

                        if (completedTask.Status == TaskStatus.RanToCompletion)
                        {
                            // Cancel the other tasks, since, they may still be running
                            linkedTokenSource.Cancel();

                            return completedTask.Result;
                        }
                        else
                        {
                            token.ThrowIfCancellationRequested();

                            // In this case, completedTask did not run to completion.
                            // That is, it faulted or got canceled. Remove it, and try Task.WhenAny again
                            tasks.Remove(completedTask);
                        }
                    }
                }
            }

            // no matches were found
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                Strings.UnknownPackageSpecificVersion,
                packageIdentity.Id,
                packageIdentity.Version.ToNormalizedString()));
        }

        /// <summary>
        /// Returns the <see cref="DownloadResourceResult"/> for a given <paramref name="packageIdentity" /> from the given
        /// <paramref name="sourceRepository" />.
        /// </summary>
        public static async Task<DownloadResourceResult> GetDownloadResourceResultAsync(SourceRepository sourceRepository,
            PackageIdentity packageIdentity,
            Configuration.ISettings settings,
            CancellationToken token)
        {
            if (sourceRepository == null)
            {
                throw new ArgumentNullException(nameof(sourceRepository));
            }

            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            token.ThrowIfCancellationRequested();

            var downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>(token);

            if (downloadResource == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.DownloadResourceNotFound, sourceRepository.PackageSource.Source));
            }

            var downloadResourceResult
                = await downloadResource.GetDownloadResourceResultAsync(packageIdentity, settings, token);

            if (downloadResourceResult == null)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.DownloadStreamNotAvailable,
                    packageIdentity,
                    sourceRepository.PackageSource.Source));
            }

            if (downloadResourceResult.PackageReader == null)
            {
                downloadResourceResult.PackageStream.Seek(0, SeekOrigin.Begin);
                var zipArchive = new ZipArchive(downloadResourceResult.PackageStream);
                var packageReader = new PackageReader(zipArchive);
                downloadResourceResult.PackageStream.Seek(0, SeekOrigin.Begin);
                downloadResourceResult = new DownloadResourceResult(downloadResourceResult.PackageStream, packageReader);
            }

            return downloadResourceResult;
        }
    }
}
