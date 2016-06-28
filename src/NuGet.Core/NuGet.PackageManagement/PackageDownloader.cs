// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
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
        /// Returns the <see cref="DownloadResourceResult"/> for a given <paramref name="packageIdentity" />
        /// from the given <paramref name="sources" />.
        /// </summary>
        public static async Task<DownloadResourceResult> GetDownloadResourceResultAsync(IEnumerable<SourceRepository> sources,
            PackageIdentity packageIdentity,
            Configuration.ISettings settings,
            SourceCacheContext cacheContext,
            Common.ILogger logger,
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

            var failedTasks = new List<Task<DownloadResourceResult>>();
            var tasksLookup = new Dictionary<Task<DownloadResourceResult>, SourceRepository>();

            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            try
            {
                // Create a group of local sources that will go first, then everything else.
                var groups = new Queue<List<SourceRepository>>();
                var localGroup = new List<SourceRepository>();
                var otherGroup = new List<SourceRepository>();

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

                groups.Enqueue(localGroup);
                groups.Enqueue(otherGroup);

                while (groups.Count > 0)
                {
                    token.ThrowIfCancellationRequested();

                    var sourceGroup = groups.Dequeue();
                    var tasks = new List<Task<DownloadResourceResult>>();

                    foreach (var source in sourceGroup)
                    {
                        var task = GetDownloadResourceResultAsync(source, packageIdentity, settings, cacheContext, logger, linkedTokenSource.Token);
                        tasksLookup.Add(task, source);
                        tasks.Add(task);
                    }

                    while (tasks.Any())
                    {
                        var completedTask = await Task.WhenAny(tasks);

                        if (completedTask.Status == TaskStatus.RanToCompletion)
                        {
                            tasks.Remove(completedTask);

                            // Cancel the other tasks, since, they may still be running
                            linkedTokenSource.Cancel();

                            if (tasks.Any())
                            {
                                // NOTE: Create a Task out of remainingTasks which waits for all the tasks to complete
                                // and disposes the linked token source safely. One of the tasks could try to access
                                // its incoming CancellationToken to register a callback. If the linkedTokenSource was
                                // disposed before being accessed, it will throw an ObjectDisposedException.
                                // At the same time, we do not want to wait for all the tasks to complete before
                                // before this method returns with a DownloadResourceResult.
                                var remainingTasks = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await Task.WhenAll(tasks);
                                    }
                                    catch
                                    {
                                        // Any exception from one of the remaining tasks is not actionable.
                                        // And, this code is running on the threadpool and the task is not awaited on.
                                        // Catch all and do nothing.
                                    }
                                    finally
                                    {
                                        linkedTokenSource.Dispose();
                                    }
                                });
                            }

                            return completedTask.Result;
                        }
                        else
                        {
                            token.ThrowIfCancellationRequested();

                            // In this case, completedTask did not run to completion.
                            // That is, it faulted or got canceled. Remove it, and try Task.WhenAny again
                            tasks.Remove(completedTask);
                            failedTasks.Add(completedTask);
                        }
                    }
                }

                // no matches were found
                var errors = new StringBuilder();

                errors.AppendLine(string.Format(CultureInfo.CurrentCulture,
                    Strings.UnknownPackageSpecificVersion, packageIdentity.Id,
                    packageIdentity.Version.ToNormalizedString()));

                foreach (var task in failedTasks)
                {
                    var message = ExceptionUtilities.DisplayMessage(task.Exception);

                    errors.AppendLine($"  {tasksLookup[task].PackageSource.Source}: {message}");
                }

                throw new FatalProtocolException(errors.ToString());
            }
            catch
            {
                linkedTokenSource.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Returns the <see cref="DownloadResourceResult"/> for a given <paramref name="packageIdentity" /> from the given
        /// <paramref name="sourceRepository" />.
        /// </summary>
        public static async Task<DownloadResourceResult> GetDownloadResourceResultAsync(SourceRepository sourceRepository,
            PackageIdentity packageIdentity,
            Configuration.ISettings settings,
            SourceCacheContext cacheContext,
            Common.ILogger logger,
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

            var downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>(token);

            if (downloadResource == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.DownloadResourceNotFound, sourceRepository.PackageSource.Source));
            }

            token.ThrowIfCancellationRequested();

            DownloadResourceResult result;
            try
            {
                result = await downloadResource.GetDownloadResourceResultAsync(
                   packageIdentity,
                   settings,
                   cacheContext,
                   logger,
                   token);
            }
            catch (OperationCanceledException)
            {
                result = new DownloadResourceResult(DownloadResourceResultStatus.Cancelled);
            }

            if (result == null)
            {
                throw new FatalProtocolException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.DownloadStreamNotAvailable,
                    packageIdentity,
                    sourceRepository.PackageSource.Source));
            }

            if (result.Status == DownloadResourceResultStatus.Cancelled)
            {
                throw new RetriableProtocolException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PackageCancelledFromSource,
                    packageIdentity,
                    sourceRepository.PackageSource.Source));
            }

            if (result.Status == DownloadResourceResultStatus.NotFound)
            {
                throw new FatalProtocolException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PackageNotFoundOnSource,
                    packageIdentity,
                    sourceRepository.PackageSource.Source));
            }

            if (result.PackageReader == null)
            {
                result.PackageStream.Seek(0, SeekOrigin.Begin);
                var packageReader = new PackageArchiveReader(result.PackageStream);
                result.PackageStream.Seek(0, SeekOrigin.Begin);
                result = new DownloadResourceResult(result.PackageStream, packageReader, sourceRepository.PackageSource.Source);
            }
            else
            {
                // bind the source
                result = new DownloadResourceResult(result.PackageStream, result.PackageReader, sourceRepository.PackageSource.Source);
            }

            return result;
        }
    }
}
