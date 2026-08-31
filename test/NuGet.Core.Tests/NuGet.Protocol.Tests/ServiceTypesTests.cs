// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class ServiceTypesTests
    {
        [Fact]
        public void RepositorySignatures_AllResourcesAreVersioned()
        {
            // Currently RepositorySignatures resources are explicitly versioned ahead of time.
            // If a RepositorySignatures/Versioned resource is ever added the resource caching strategy will need to be revisited.
            var pattern = new Regex(@"^RepositorySignatures/[4-9]\.\d\.\d$", RegexOptions.CultureInvariant);

            foreach (var repositorySignaturesResource in ServiceTypes.RepositorySignatures)
            {
                Assert.True(pattern.IsMatch(repositorySignaturesResource), $"RepositorySignatures resource '{repositorySignaturesResource}' is not versioned with an expected format.  Verify that {nameof(RepositorySignatureResourceProvider)} can handle any new formats.");
            }
        }

        [Fact]
        public void RegistrationsBaseUrls_AreOrderedAndResolveVersion7120()
        {
            string[] expected =
            {
                "RegistrationsBaseUrl/7.12.0",
                "RegistrationsBaseUrl/Versioned",
                "RegistrationsBaseUrl/3.6.0",
                "RegistrationsBaseUrl/3.4.0",
                "RegistrationsBaseUrl/3.0.0-rc",
                "RegistrationsBaseUrl/3.0.0-beta",
                "RegistrationsBaseUrl"
            };
            ServiceTypes.RegistrationsBaseUrl.Should().ContainInOrder(expected);

            var serviceIndex = new ServiceIndexResourceV3(
                JObject.Parse(
                    @"{
                      ""version"": ""3.0.0"",
                      ""resources"": [
                        {
                          ""@id"": ""https://unit.test/registration/"",
                          ""@type"": ""RegistrationsBaseUrl/7.12.0""
                        }
                      ]
                    }"),
                DateTime.UtcNow);

            serviceIndex.GetServiceEntryUri(ServiceTypes.RegistrationsBaseUrl)
                .Should().Be(new Uri("https://unit.test/registration/"));
        }
    }
}
