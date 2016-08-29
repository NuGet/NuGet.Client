// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGet.Protocol.Core.Types
{
    public class HttpSourceCacheContext
    {
        private HttpSourceCacheContext(string rootTempFolder, TimeSpan maxAge, bool directDownload)
        {
            if (maxAge <= TimeSpan.Zero)
            {
                if (rootTempFolder == null)
                {
                    throw new ArgumentNullException(nameof(rootTempFolder));
                }
            }
            else
            {
                Debug.Assert(
                    rootTempFolder == null,
                    $"{nameof(rootTempFolder)} should be null when {nameof(maxAge)} is not zero.");
            }

            RootTempFolder = rootTempFolder;
            MaxAge = maxAge;
            DirectDownload = directDownload;
        }

        public TimeSpan MaxAge { get; }

        public bool DirectDownload { get; }

        /// <summary>
        /// A suggested root folder to drop temporary files under, it will get cleared by the
        /// disposal of the <see cref="SourceCacheContext"/> that was used to create this instance.
        /// </summary>
        public string RootTempFolder { get; }

        public static HttpSourceCacheContext Create(SourceCacheContext cacheContext, int retryCount)
        {
            if (cacheContext == null)
            {
                throw new ArgumentNullException(nameof(cacheContext));
            }

            if (retryCount == 0 && cacheContext.MaxAgeTimeSpan > TimeSpan.Zero)
            {
                return new HttpSourceCacheContext(
                    rootTempFolder: null,
                    maxAge: cacheContext.MaxAgeTimeSpan,
                    directDownload: cacheContext.DirectDownload);
            }
            else
            {
                return new HttpSourceCacheContext(
                    cacheContext.GeneratedTempFolder,
                    TimeSpan.Zero,
                    cacheContext.DirectDownload);
            }
        }
    }
}
