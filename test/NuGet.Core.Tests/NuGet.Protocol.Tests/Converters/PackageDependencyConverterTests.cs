// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Packaging;
using NuGet.Versioning;
using NuGet.Protocol.Plugins;
using Xunit;
using System.Collections.Generic;
using System.IO;

namespace NuGet.Protocol.Tests
{
    public class PackageDependencyConverterTests
    {
        private static readonly PackageDependencyGroupConverter _converter = new PackageDependencyGroupConverter();

        [Fact]
        public void CanConvert_ReturnsTrueForPackageDependencyType()
        {
            var canConvert = _converter.CanConvert(typeof(PackageDependencyGroup));

            Assert.True(canConvert);
        }

        [Fact]
        public void ReadJson_ReturnsPackageDependency()
        {
            const string id = "PackageA";
            var version = new VersionRange(new NuGetVersion(1, 4, 1));
            var expectedPackageDependency = new Packaging.Core.PackageDependency(id, version);
            IEnumerable<Packaging.Core.PackageDependency> packages = new List<Packaging.Core.PackageDependency>() { expectedPackageDependency };

            using var stringReader = new StringReader(PackageRegistrationDependencyGroupsJson);
            using var jsonReader = new JsonTextReader(stringReader);

            jsonReader.Read();

            PackageDependencyGroup actualPackageDependencies = (PackageDependencyGroup)_converter.ReadJson(
                jsonReader,
                typeof(PackageDependencyGroup),
                existingValue: null,
                serializer: JsonSerializationUtilities.Serializer);

            Assert.Equal(packages, actualPackageDependencies.Packages);
        }

        [Fact]
        public void ReadJson_ReturnsPackageDependencyWithNoRange()
        {
            const string id = "PackageA";
            var version = VersionRange.All;
            var expectedPackageDependency = new Packaging.Core.PackageDependency(id, version);

            IEnumerable<Packaging.Core.PackageDependency> packages = new List<Packaging.Core.PackageDependency>() { expectedPackageDependency };
            using var stringReader = new StringReader(PackageRegistrationDependencyGroupsNoRangeJson);
            using var jsonReader = new JsonTextReader(stringReader);

            jsonReader.Read();

            PackageDependencyGroup actualPackageDependencies = (PackageDependencyGroup)_converter.ReadJson(
                jsonReader,
                typeof(PackageDependencyGroup),
                existingValue: null,
                serializer: JsonSerializationUtilities.Serializer);
            Assert.Equal(packages, actualPackageDependencies.Packages);
        }

        private const string PackageRegistrationDependencyGroupsJson = @"{
    ""dependencies"": [
            {
             ""@id"": ""https://api.nuget.org/v3/catalog0/data/2015.02.01.06.24.00/PackageA.1.6.0.json#dependencygroup/jquery"",
             //  comment to test that they are ignored
             ""@type"": ""PackageDependency"",
             ""id"": ""PackageA"",
             ""range"": ""[1.4.1, )"",
             ""registration"": ""https://api.nuget.org/v3/registration0/PackageA/index.json""
            }
           ]
        }";

        private const string PackageRegistrationDependencyGroupsNoRangeJson = @"{""dependencies"": [
                {
                ""@id"": ""https://api.nuget.org/v3/catalog0/data/2015.02.01.06.24.00/PackageA.1.6.0.json#dependencygroup/jquery"",
                ""@type"": ""PackageDependency"",
                ""id"": ""PackageA"",
                ""registration"": ""https://api.nuget.org/v3/registration0/PackageA/index.json""
                }
            ]
        }";
    }
}
