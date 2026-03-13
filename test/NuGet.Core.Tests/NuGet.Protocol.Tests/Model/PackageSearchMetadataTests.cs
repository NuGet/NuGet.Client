// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class PackageSearchMetadataTests
    {
        [Fact]
        public void CacheStrings_DeduplicatesStrings()
        {
            // Arrange
            var cache = new MetadataReferenceCache();
            var metadata1 = new JObject { ["authors"] = "Microsoft", ["description"] = "desc" }.FromJToken<PackageSearchMetadata>();
            var metadata2 = new JObject { ["authors"] = "Microsoft", ["description"] = "other" }.FromJToken<PackageSearchMetadata>();

            // Act
            metadata1.CacheStrings(cache);
            metadata2.CacheStrings(cache);

            // Assert
            Assert.Same(metadata1.Authors, metadata2.Authors);
        }

        [Fact]
        public void Verify_AllStringProperties_AccountedInCacheStrings()
        {
            var stringProperties = typeof(PackageSearchMetadata)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.PropertyType == typeof(string))
                .ToArray();

            stringProperties.Should().HaveCount(10,
                $"the number of string properties changed in PackageSearchMetadata " +
                $"[{string.Join(", ", stringProperties.Select(p => p.Name))}]. " +
                "Please make sure this change is accounted for in the CacheStrings method");
        }
    }
}
