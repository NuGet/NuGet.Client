// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
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
        [Fact]
        public async Task TryCreate_When_RegistrationsBaseUrl_Version_Is_Unuspported_Returns_False()
        {
            //Arrange
            var rawServiceIndex = @"{
              ""version"": ""3.0.0"",
              ""resources"": [
                {
                  ""@id"": ""https://api.nuget.org/v5/registrations-gz-semver3"",
                  ""@type"": ""RegistrationsBaseUrl/7.0.0"",
                  ""comment"": ""Fancy new semver 3 url that this client doesn't support""
                }]}";

            var serviceIndexJObject = JObject.Parse(rawServiceIndex);
            var serviceIndexResource = new ServiceIndexResourceV3(serviceIndexJObject, DateTime.Now);
            var sourceRepositoryMock = Mock.Of<SourceRepository>(mock => mock.GetResourceAsync<ServiceIndexResourceV3>(It.IsAny<CancellationToken>()) == Task.FromResult(serviceIndexResource));

            var sut = new RegistrationResourceV3Provider();

            //Act
            var actual = await sut.TryCreate(sourceRepositoryMock, CancellationToken.None);

            //Assert
            actual.Item1.Should().BeFalse();
            actual.Item2.Should().BeNull();
        }

        [Theory]
        [InlineData(new string[0], false, false)]
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
            ServiceIndexEntry[] entries = registrationVersions.Select(version =>
            {
                string serviceType = "RegistrationsBaseUrl/" + version;
                var clientVersion = new NuGetVersion(3, 0, 0);
                return new ServiceIndexEntry(registrationUri, serviceType, clientVersion);
            }).ToArray();
            var responses = new Dictionary<string, string>();
            INuGetResourceProvider[] providers =
            [
                MockServiceIndexResourceV3Provider.Create(entries),
                StaticHttpSource.CreateHttpSource(responses),
            ];
            var sourceRepository = new SourceRepository(packageSource, providers);

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
