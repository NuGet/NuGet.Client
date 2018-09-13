// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Core.FuncTest
{
    public class V2FeedParserTests
    {
        [Fact]
        public async Task V2FeedParser_DownloadFromUrl()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3("https://www.nuget.org/api/v2/");

            var httpSource = HttpSource.Create(repo);

            var parser = new V2FeedParser(httpSource, "https://www.nuget.org/api/v2/");

            // Act & Assert
            using (var packagesFolder = TestDirectory.Create())
            using (var cacheContext = new SourceCacheContext())
            {
                var downloadContext = new PackageDownloadContext(cacheContext)
                {
                    ExtractionContext = new PackageExtractionContext(
                    PackageSaveMode.Defaultv3,
                    PackageExtractionBehavior.XmlDocFileSaveMode,
                    NullLogger.Instance,
                    signedPackageVerifier: null,
                    signedPackageVerifierSettings: null)
                };

                using (var downloadResult = await parser.DownloadFromUrl(
                    new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("6.2.0")),
                    new Uri("https://www.nuget.org/api/v2/package/WindowsAzure.Storage/6.2.0"),
                    downloadContext,
                    packagesFolder,
                    NullLogger.Instance,
                    CancellationToken.None))
                {
                    var packageReader = downloadResult.PackageReader;
                    var files = packageReader.GetFiles();

                    Assert.Equal(12, files.Count());
                }
            }
        }
    }
}
