// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        [InlineData(new string[0], false, false)]
        [InlineData(new[] { "7.0.0" }, false, false)]
        [InlineData(new[] { "7.12.0" }, false, false)]
        [InlineData(new[] { "3.6.0" }, false, true)]
        [InlineData(new[] { "3.6.0", "7.12.0" }, true, true)]
        public async Task TryCreate_ReportsPackageIdMetadataCapability(
            string[] registrationVersions,
            bool supportsPackageIdMetadata,
            bool supportsRegistration)
        {
            // Arrange
            var packageSource = new PackageSource("https://unit.test/v3/index.json");
            var registrationUri = new Uri("https://unit.test/registration/");
            ServiceIndexEntry[] entries = registrationVersions.Select(version => new ServiceIndexEntry(
                registrationUri, "RegistrationsBaseUrl/" + version, new NuGetVersion(3, 0, 0))).ToArray();
            var sourceRepository = new SourceRepository(
                packageSource,
                new INuGetResourceProvider[]
                {
                    MockServiceIndexResourceV3Provider.Create(entries),
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
            resource.BaseUri.Should().Be(registrationUri);
            resource.SupportsPackageIdMetadata.Should().Be(supportsPackageIdMetadata);
        }
    }
}
