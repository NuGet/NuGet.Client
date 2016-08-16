// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Core.Types
{
    public class PackageDownloadContext
    {
        public PackageDownloadContext(SourceCacheContext sourceCacheContext) : this(
            sourceCacheContext,
            directDownloadDirectory: null)
        {
        }

        public PackageDownloadContext(SourceCacheContext sourceCacheContext, string directDownloadDirectory)
        {
            if (sourceCacheContext == null)
            {
                throw new ArgumentNullException(nameof(sourceCacheContext));
            }

            SourceCacheContext = sourceCacheContext;
            DirectDownload = directDownloadDirectory != null;
            DirectDownloadDirectory = directDownloadDirectory;
        }

        public SourceCacheContext SourceCacheContext { get; }
        public bool DirectDownload { get; }
        public string DirectDownloadDirectory { get; }
    }
}
