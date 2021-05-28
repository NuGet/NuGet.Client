// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Configuration;
using NuGet.Packaging.Signing;

namespace NuGet.Protocol.Core.Types
{
    public class PackageDownloadContext
    {
        public PackageDownloadContext(SourceCacheContext sourceCacheContext) : this(
            sourceCacheContext,
            directDownloadDirectory: null,
            directDownload: false)
        {
        }

        public PackageDownloadContext(
            SourceCacheContext sourceCacheContext,
            string directDownloadDirectory,
            bool directDownload)
        {
            if (sourceCacheContext == null)
            {
                throw new ArgumentNullException(nameof(sourceCacheContext));
            }

            if (directDownloadDirectory == null && (directDownload || sourceCacheContext.NoCache))
            {
                // If NoCache is specified on the source cache context, it's possible that we will perform a direct
                // download (even if the PackageDownloadContext.DirectDownload property is false).
                throw new ArgumentNullException(nameof(directDownloadDirectory));
            }

            SourceCacheContext = sourceCacheContext;
            DirectDownload = directDownload;
            DirectDownloadDirectory = directDownloadDirectory;
            PackageNamespacesConfiguration = new Lazy<PackageNamespacesConfiguration>(() => GetPackageNamespacesConfiguration());
        }

        public PackageDownloadContext(
            SourceCacheContext sourceCacheContext,
            string directDownloadDirectory,
            bool directDownload,
            ISettings settings)
            : this(sourceCacheContext, directDownloadDirectory, directDownload)
        {
            Settings = settings;
        }

        public SourceCacheContext SourceCacheContext { get; }
        public bool DirectDownload { get; }
        public string DirectDownloadDirectory { get; }

        public Guid ParentId { get; set; }

        public ClientPolicyContext ClientPolicyContext { get; set; }
        public ISettings Settings { get; }
        public Lazy<PackageNamespacesConfiguration> PackageNamespacesConfiguration { get; }

        // We need to calculation of Settings to PackageNamespacesConfiguration here other wisewe there would be
        // duplicate calculation for each packageId.
        private PackageNamespacesConfiguration GetPackageNamespacesConfiguration()
        {
            return Configuration.PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(Settings);
        }
    }
}
