// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Core.Types
{
    public class HttpSourceCacheContext
    {
        private HttpSourceCacheContext(string rootTempFolder, TimeSpan maxAge, bool directDownload)
        {
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

            if (retryCount == 0)
            {
                return new HttpSourceCacheContext(
                    cacheContext.GeneratedTempFolder,
                    cacheContext.MaxAgeTimeSpan,
                    cacheContext.DirectDownload);
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
