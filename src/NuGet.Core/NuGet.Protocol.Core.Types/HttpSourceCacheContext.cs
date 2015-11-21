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

        public HttpSourceCacheContext(SourceCacheContext context)
        {
            if (context == null)
            {
                throw new InvalidOperationException(nameof(context));
            }

            MaxAge = context.ListMaxAgeTimeSpan;
            _rootTempFolder = context.GeneratedTempFolder;
        }

        public HttpSourceCacheContext(SourceCacheContext context, TimeSpan overrideMaxAge)
        {
            if (context == null)
            {
                throw new InvalidOperationException(nameof(context));
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
    }
}
