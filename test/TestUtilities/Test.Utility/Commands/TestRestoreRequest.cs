// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Test;

namespace NuGet.Commands.Test
{
    public class TestRestoreRequest : RestoreRequest
    {
        public TestRestoreRequest(
            PackageSpec project,
            IEnumerable<PackageSource> sources,
            string packagesDirectory,
            ILogger log)
            : this(
                  project,
                  sources,
                  packagesDirectory,
                  new TestSourceCacheContext(),
                  log)
        {
        }

        public TestRestoreRequest(
            PackageSpec project,
            IEnumerable<PackageSource> sources,
            string packagesDirectory,
            SourceCacheContext cacheContext,
            ILogger log)
            : this(
                  project,
                  sources,
                  packagesDirectory,
                  new List<string>(),
                  cacheContext,
                  log)
        {
        }

        public TestRestoreRequest(
            PackageSpec project,
            IEnumerable<PackageSource> sources,
            string packagesDirectory,
            IEnumerable<string> fallbackPackageFolders,
            ILogger log)
            : this(
                  project,
                  sources,
                  packagesDirectory,
                  fallbackPackageFolders,
                  new TestSourceCacheContext(),
                  log)
        {
        }

        public TestRestoreRequest(
            PackageSpec project,
            IEnumerable<PackageSource> sources,
            string packagesDirectory,
            IEnumerable<string> fallbackPackageFolders,
            SourceCacheContext cacheContext,
            ILogger log)
            : this(
                  project,
                  sources.Select(source => Repository.Factory.GetCoreV3(source.Source)),
                  packagesDirectory,
                  fallbackPackageFolders,
                  cacheContext,
                  log)
        {
        }

        public TestRestoreRequest(
            PackageSpec project,
            IEnumerable<SourceRepository> sources,
            string packagesDirectory,
            IEnumerable<string> fallbackPackageFolders,
            ILogger log) : this(
                project,
                sources,
                packagesDirectory,
                fallbackPackageFolders,
                new TestSourceCacheContext(),
                log)
        {
        }

        public TestRestoreRequest(
            PackageSpec project,
            IEnumerable<SourceRepository> sources,
            string packagesDirectory,
            IEnumerable<string> fallbackPackageFolders,
            SourceCacheContext cacheContext,
            ILogger log) : base(
                project,
                RestoreCommandProviders.Create(
                    packagesDirectory,
                    fallbackPackageFolderPaths: fallbackPackageFolders,
                    sources: sources,
                    cacheContext: cacheContext,
                    packageFileCache: new LocalPackageFileCache(),
                    log: log),
                cacheContext,
                log)
        {
        }
    }
}
