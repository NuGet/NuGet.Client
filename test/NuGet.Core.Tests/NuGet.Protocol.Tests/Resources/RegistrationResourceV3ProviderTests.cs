// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Tests.Providers;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests.Resources
{
    public class RegistrationResourceV3ProviderTests
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task TryCreate_ReportsPackageIdMetadataCapability(
            bool supportsRegistration,
            bool supportsPackageIdMetadata)
        {
            // Arrange
            var packageSource = new PackageSource("https://unit.test/v3/index.json");
            var entries = new List<ServiceIndexEntry>();
            if (supportsRegistration)
            {
                entries.Add(new ServiceIndexEntry(
                    new Uri("https://unit.test/registration/"),
                    "RegistrationsBaseUrl/3.6.0",
                    new NuGetVersion(3, 0, 0)));
            }
            if (supportsPackageIdMetadata)
            {
                entries.Add(new ServiceIndexEntry(
                    entries[0].Uri,
                    "RegistrationsBaseUrl/7.12.0",
                    new NuGetVersion(3, 0, 0)));
            }
            var sourceRepository = new SourceRepository(
                packageSource,
                new INuGetResourceProvider[]
                {
                    MockServiceIndexResourceV3Provider.Create(entries.ToArray()),
                    StaticHttpSource.CreateHttpSource(new Dictionary<string, string>()),
                });

            var sut = new RegistrationResourceV3Provider();

            // Act
            Tuple<bool, INuGetResource?> actual =
                await sut.TryCreate(sourceRepository, CancellationToken.None);

            // Assert
            actual.Item1.Should().Be(supportsRegistration);
            if (!supportsRegistration)
            {
                actual.Item2.Should().BeNull();
                return;
            }

            RegistrationResourceV3 resource = actual.Item2.Should().BeOfType<RegistrationResourceV3>().Subject;
            resource.BaseUri.Should().Be(new Uri("https://unit.test/registration/"));
            resource.SupportsPackageIdMetadata.Should().Be(supportsPackageIdMetadata);
        }

    }
}
