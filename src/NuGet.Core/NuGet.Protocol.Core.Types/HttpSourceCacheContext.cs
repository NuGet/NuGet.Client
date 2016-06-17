// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Protocol.Core.Types
{
    public class HttpSourceCacheContext
    {
        private readonly string _rootTempFolder;
        private bool tempFolderCreated;

        /// <summary>
        /// Default context, caches for 30 minutes
        /// </summary>
        public HttpSourceCacheContext()
            : this(new SourceCacheContext())
        {
        }

        public HttpSourceCacheContext(SourceCacheContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            MaxAge = context.ListMaxAgeTimeSpan;
            _rootTempFolder = context.GeneratedTempFolder;
        }

        public HttpSourceCacheContext(SourceCacheContext context, TimeSpan overrideMaxAge)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            MaxAge = overrideMaxAge;
            _rootTempFolder = context.GeneratedTempFolder;
        }

        public TimeSpan MaxAge { get; }

        /// <summary>
        /// A suggested root folder to drop temporary files under, it will get cleared by the
        /// code that constructs a RestoreRequest.
        /// </summary>
        public string RootTempFolder
        {
            get
            {
                if (!tempFolderCreated)
                {
                    Directory.CreateDirectory(_rootTempFolder);
                    tempFolderCreated = true;
                }

                return _rootTempFolder;
            }
        }

        public static HttpSourceCacheContext CreateCacheContext(SourceCacheContext cacheContext, int retryCount)
        {
            if (retryCount == 0)
            {
                return new HttpSourceCacheContext(cacheContext);
            }
            else
            {
                return new HttpSourceCacheContext(cacheContext, TimeSpan.Zero);
            }
        }
    }
}
