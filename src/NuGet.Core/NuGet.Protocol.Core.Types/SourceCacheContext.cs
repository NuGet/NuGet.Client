// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Cache control settings for the V3 disk cache.
    /// </summary>
    public class SourceCacheContext : IDisposable
    {
        /// <summary>
        /// Path of temp folder if requested by GeneratedTempFolder
        /// </summary>
        private string _generatedTempFolder = null;

        /// <summary>
        /// Default amount of time to cache version lists.
        /// </summary>
        private static readonly TimeSpan DefaultMaxAge = TimeSpan.FromMinutes(30);

        /// <summary>
        /// If set, the global disk cache will not be written to or read from. Instead, a temporary directory will be
        /// used.
        /// </summary>
        public bool NoCache { get; set; }

        /// <summary>
        /// If set, the global disk cache will not be written to.
        /// </summary>
        public bool DirectDownload { get; set; }

        /// <summary>
        /// Package version lists or packages from the server older than this date will be fetched from the server.
        /// </summary>
        /// <remarks>This will be ignored if <see cref="NoCache"/> is true.</remarks>
        /// <remarks>If the value is null the default expiration will be used.</remarks>
        public DateTimeOffset? MaxAge { get; set; }

        /// <summary>
        /// Package version lists from the server older than this time span
        /// will be fetched from the server.
        /// </summary>
        public TimeSpan MaxAgeTimeSpan
        {
            get
            {
                return GetCacheTime(MaxAge, DefaultMaxAge);
            }
        }

        private TimeSpan GetCacheTime(DateTimeOffset? maxAge, TimeSpan defaultTime)
        {
            var timeSpan = TimeSpan.Zero;

            if (!NoCache)
            {
                // Default
                timeSpan = defaultTime;

                // If the max age is set use that instead of the default
                if (maxAge.HasValue)
                {
                    var difference = DateTimeOffset.UtcNow.Subtract(maxAge.Value);

                    Debug.Assert(difference >= TimeSpan.Zero, "Invalid cache time");

                    if (difference >= TimeSpan.Zero)
                    {
                        timeSpan = difference;
                    }
                }
            }

            return timeSpan;
        }

        public string GeneratedTempFolder
        {
            get
            {
                if (_generatedTempFolder == null)
                {
                    var newTempFolder = Path.Combine(
                        Path.GetTempPath(),
                        "NuGet",
                        "TempCache",
                        Guid.NewGuid().ToString());

                    Interlocked.CompareExchange(ref _generatedTempFolder, newTempFolder, null);
                }

                return _generatedTempFolder;
            }
        }

        public bool IgnoreFailedSources { get; set; }

        public void Dispose()
        {
            var currentTempFolder = Interlocked.CompareExchange(ref _generatedTempFolder, null, null);

            if (currentTempFolder != null)
            {
                try
                {
                    Directory.Delete(_generatedTempFolder, recursive: true);
                }
                catch
                {
                    // Ignore failures when cleaning up.
                }
            }
        }
    }
}
