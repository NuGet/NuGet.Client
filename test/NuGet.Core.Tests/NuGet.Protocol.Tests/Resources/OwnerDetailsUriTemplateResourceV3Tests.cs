// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using FluentAssertions;
using NuGet.Protocol.Resources;
using Xunit;

namespace NuGet.Protocol.Tests.Resources
{
    public class OwnerDetailsUriTemplateResourceV3Tests
    {
        private readonly Uri _template = new Uri("https://nuget.test/profiles/{owner}?_src=template");

        [Fact]
        public void CreateOrNull_WhenNullTemplate_CreatesNullResource()
        {
            // Arrange
            Uri? template = null;

            // Act
            var target = OwnerDetailsUriTemplateResourceV3.CreateOrNull(template);

            // Assert
            target.Should().BeNull();
        }

        [Fact]
        public void CreateOrNull_WhenEmptyTemplate_CreatesNullResource()
        {
            // Arrange
            var template = new Uri(string.Empty);

            // Act
            var target = OwnerDetailsUriTemplateResourceV3.CreateOrNull(template);

            // Assert
            target.Should().BeNull();
        }

        [Fact]
        public void CreateOrNull_WhenTemplateNotAbsoluteUri_CreatesNullResource()
        {
            // Arrange
            var template = new Uri("/owner/profile");

            // Act
            var target = OwnerDetailsUriTemplateResourceV3.CreateOrNull(template);

            // Assert
            target.Should().BeNull();
        }

        [Fact]
        public void CreateOrNull_WhenTemplateNotHttps_CreatesNullResource()
        {
            // Arrange
            var template = new Uri("http://nuget.test/profiles/{owner}?_src=template");

            // Act
            var target = OwnerDetailsUriTemplateResourceV3.CreateOrNull(template);

            // Assert
            target.Should().BeNull();
        }

        [Fact]
        public void CreateOrNull_WhenValidTemplateHttps_CreatesResource()
        {
            // Arrange
            var template = new Uri("https://nuget.test/profiles/{owner}?_src=template");

            // Act
            var target = OwnerDetailsUriTemplateResourceV3.CreateOrNull(template);

            // Assert
            target.Should().NotBeNull();
        }

        [Fact]
        public void GetUri_WithSpacesInOwnerParameter_CreatesValidOwnerUriWithEncoding()
        {
            // Arrange
            string owner = "Microsoft Microsoft Microsoft";
            string formattedOwner = "Microsoft%20Microsoft%20Microsoft";

            var target = OwnerDetailsUriTemplateResourceV3.CreateOrNull(_template);

            // Act
            Uri ownerUri = target!.GetUri(owner);

            // Assert
            ownerUri.Should().NotBeNull();
            ownerUri.IsAbsoluteUri.Should().BeTrue();
            ownerUri.AbsoluteUri.Should().Be($"https://nuget.test/profiles/{formattedOwner}?_src=template");
        }

        [Theory]
        [InlineData("microsoft")]
        [InlineData("MiCroSoFT")]
        public void GetUri_WithValidOwnerParameter_CreatesValidOwnerUriWithSameCasing(string owner)
        {
            // Arrange
            var target = OwnerDetailsUriTemplateResourceV3.CreateOrNull(_template);

            // Act
            Uri ownerUri = target!.GetUri(owner);

            // Assert
            ownerUri.Should().NotBeNull();
            ownerUri.IsAbsoluteUri.Should().BeTrue();
            ownerUri.AbsoluteUri.Should().Be($"https://nuget.test/profiles/{owner}?_src=template");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetUri_WithInvalidOwnerParameter_ReturnsOriginalTemplate(string owner)
        {
            // Arrange
            var target = OwnerDetailsUriTemplateResourceV3.CreateOrNull(_template);
            string templateWithoutOwner = "https://nuget.test/profiles/?_src=template";

            // Act
            Uri ownerUri = target!.GetUri(owner);

            // Assert
            ownerUri.Should().NotBeNull();
            ownerUri.IsAbsoluteUri.Should().BeTrue();
            ownerUri.AbsoluteUri.Should().Be(templateWithoutOwner);
        }
    }
}
