// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Protocol.Converters;
using NuGet.Versioning;
using NuGet.Protocol.Plugins;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class PackageDependencyConverterTests
    {
        private static readonly PackageDependencyConverter _converter = new PackageDependencyConverter();

        [Fact]
        public void CanConvert_ReturnsTrueForPackageDependencyType()
        {
            var canConvert = _converter.CanConvert(typeof(Packaging.Core.PackageDependency));

            Assert.True(canConvert);
        }

        [Fact]
        public void ReadJson_ReturnsPackageDependency()
        {
            const string id = "PackageA";
            var version = new VersionRange(new NuGetVersion(1, 4, 1));
            var expectedPackageDependency = new Packaging.Core.PackageDependency(id, version);

            var actualPackageDependency = _converter.ReadJson(
                new JsonTextReader(new System.IO.StringReader(PackageRegistrationDependencyGroupsJson)),
                typeof(Packaging.Core.PackageDependency),
                existingValue: null,
                serializer: JsonSerializationUtilities.Serializer);
            Assert.Equal(expectedPackageDependency, actualPackageDependency);
        }

        [Theory]
        [InlineData(PackageRegistrationDependencyGroupsNoRangeJson)]
        [InlineData(PackageRegistrationDependencyGroupsEmptyRangeJson)]
        public void ReadJson_ReturnsPackageDependencyWithNoRange(string json)
        {
            const string id = "PackageA";
            var version = VersionRange.All;
            var expectedPackageDependency = new Packaging.Core.PackageDependency(id, version);

            var actualPackageDependency = _converter.ReadJson(
                new JsonTextReader(new System.IO.StringReader(json)),
                typeof(Packaging.Core.PackageDependency),
                existingValue: null,
                serializer: JsonSerializationUtilities.Serializer);
            Assert.Equal(expectedPackageDependency, actualPackageDependency);
        }

        private const string PackageRegistrationDependencyGroupsJson = @"{""dependencyGroups"": [
              {
                ""@id"": ""https://api.nuget.org/v3/catalog0/data/2015.02.01.06.24.00/PackageA.1.6.0.json#dependencygroup"",
                ""@type"": ""PackageDependencyGroup"",
                ""dependencies"": [
                  {
                    ""@id"": ""https://api.nuget.org/v3/catalog0/data/2015.02.01.06.24.00/PackageA.1.6.0.json#dependencygroup/jquery"",
                    ""@type"": ""PackageDependency"",
                    ""id"": ""PackageA"",
                    ""range"": ""[1.4.1, )"",
                    ""registration"": ""https://api.nuget.org/v3/registration0/PackageA/index.json""
                  }
                ]
              }
            ],
        }";

        private const string PackageRegistrationDependencyGroupsNoRangeJson = @"{""dependencyGroups"": [
              {
                ""@id"": ""https://api.nuget.org/v3/catalog0/data/2015.02.01.06.24.00/PackageA.1.6.0.json#dependencygroup"",
                ""@type"": ""PackageDependencyGroup"",
                ""dependencies"": [
                  {
                    ""@id"": ""https://api.nuget.org/v3/catalog0/data/2015.02.01.06.24.00/PackageA.1.6.0.json#dependencygroup/jquery"",
                    ""@type"": ""PackageDependency"",
                    ""id"": ""PackageA"",
                    ""registration"": ""https://api.nuget.org/v3/registration0/PackageA/index.json""
                  }
                ]
              }
            ],
        }";

        private const string PackageRegistrationDependencyGroupsEmptyRangeJson = @"{""dependencyGroups"": [
              {
                ""@id"": ""https://api.nuget.org/v3/catalog0/data/2015.02.01.06.24.00/PackageA.1.6.0.json#dependencygroup"",
                ""@type"": ""PackageDependencyGroup"",
                ""dependencies"": [
                  {
                    ""@id"": ""https://api.nuget.org/v3/catalog0/data/2015.02.01.06.24.00/PackageA.1.6.0.json#dependencygroup/jquery"",
                    ""@type"": ""PackageDependency"",
                    ""id"": ""PackageA"",
                    ""range"": """",
                    ""registration"": ""https://api.nuget.org/v3/registration0/PackageA/index.json""
                  }
                ]
              }
            ],
        }";
    }
}
