// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
            var result = (PackageSearchMetadataRegistration) await resource.GetMetadataAsync(package, Common.NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal("deepequal", result.Identity.Id, StringComparer.OrdinalIgnoreCase);
            Assert.Equal("0.9.0", result.Identity.Version.ToNormalizedString());
            Assert.True(result.Authors.Contains("James Foster"));
            Assert.Equal("An extensible deep comparison library for .NET", result.Description);
            Assert.Null(result.IconUrl);
            Assert.Null(result.LicenseUrl);
            Assert.Equal("http://github.com/jamesfoster/DeepEqual", result.ProjectUrl.ToString());
            Assert.Equal("https://api.nuget.org/v3/catalog0/data/2015.02.03.18.34.51/deepequal.0.9.0.json",
                result.CatalogUri.ToString());
            Assert.Equal(DateTimeOffset.Parse("2013-08-28T09:19:10.013Z"), result.Published);
            Assert.False(result.RequireLicenseAcceptance);
            Assert.Equal(result.Description, result.Summary);
            Assert.Equal(string.Join(", ", "deepequal", "deep", "equal"), result.Tags);
            Assert.Equal("DeepEqual", result.Title);
        }

        [Fact]
        public async Task PackageMetadataResourceV3_UsesReferenceCache()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer);
            responses.Add("https://api.nuget.org/v3/registration0/afine/index.json", JsonData.DuplicatePackageBesidesVersionRegistrationIndex);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageMetadataResource>();

            // Act
            var result = (IEnumerable<PackageSearchMetadataRegistration>) await resource.GetMetadataAsync("afine", true, true, Common.NullLogger.Instance, CancellationToken.None);

            var first = result.ElementAt(0);
            var second = result.ElementAt(1);

            // Assert
            MetadataReferenceCacheTestUtility.AssertPackagesHaveSameReferences(first, second);
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
