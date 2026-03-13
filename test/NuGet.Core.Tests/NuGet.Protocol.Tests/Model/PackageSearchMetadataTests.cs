// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Reflection;
using System.Text;
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
            var authors = new StringBuilder().Append("Microsoft").ToString();
            var json = new JObject
            {
                ["authors"] = authors,
                ["description"] = "desc",
                ["summary"] = "sum",
            };
            var metadata = json.FromJToken<PackageSearchMetadata>();

            // Pre-condition: deserialized string is equal but not same reference
            Assert.Equal(authors, metadata.Authors);

            // Act
            metadata.CacheStrings(cache);

            // Assert — after caching, the same content resolves to the same reference
            var cachedAuthors = cache.GetString(new StringBuilder().Append("Microsoft").ToString());
            Assert.Same(cachedAuthors, metadata.Authors);
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
