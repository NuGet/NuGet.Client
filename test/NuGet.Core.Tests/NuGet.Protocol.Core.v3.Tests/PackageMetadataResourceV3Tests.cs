// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class PackageMetadataResourceV3Tests
    {
        [Fact]
        public async Task PackageMetadataResourceV3_GetMetadataAsync()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer);
            responses.Add("https://api.nuget.org/v3/registration0/deepequal/index.json", JsonData.DeepEqualRegistationIndex);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageMetadataResource>();

            var package = new PackageIdentity("deepequal", NuGetVersion.Parse("0.9.0"));

            // Act
            var result = await resource.GetMetadataAsync(package, Common.NullLogger.Instance, CancellationToken.None);
            
            // Assert
            Assert.Equal("deepequal", result.Identity.Id, StringComparer.OrdinalIgnoreCase);
            Assert.Equal("0.9.0", result.Identity.Version.ToNormalizedString());
        }

        [Fact]
        public async Task PackageMetadataResourceV3_GetMetadataAsync_NotFound()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer);
            responses.Add("https://api.nuget.org/v3/registration0/deepequal/index.json", JsonData.DeepEqualRegistationIndex);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageMetadataResource>();

            var package = new PackageIdentity("deepequal", NuGetVersion.Parse("0.0.0"));

            // Act
            var result = await resource.GetMetadataAsync(package, Common.NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }
    }
}
