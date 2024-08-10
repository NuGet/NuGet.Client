// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
using NuGet.ContentModel;
using Xunit;

namespace NuGet.Packaging.Test.ContentModelTests
{
    public class ContentItemTests
    {
        [Fact]
        public void Add_WithPropertiesNotInitialized__WithAssembly_SetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem.Add(ContentItem.ManagedAssembly, value);
            // Assert
            contentItem._assembly.Should().Be(value);
        }

        [Fact]
        public void Add_WithPropertiesNotInitialized__WithTfm_SetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem.Add(ContentItem.TargetFrameworkMoniker, value);
            // Assert
            contentItem._tfm.Should().Be(value);
        }

        [Fact]
        public void Add_WithPropertiesNotInitialized__WithRuntimeIdentifier_SetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem.Add(ContentItem.RuntimeIdentifier, value);
            // Assert
            contentItem._rid.Should().Be(value);
        }

        [Fact]
        public void Add_WithPropertiesNotInitialized__WithAny_SetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem.Add(ContentItem.AnyValue, value);
            // Assert
            contentItem._any.Should().Be(value);
        }

        [Fact]
        public void Add_WithPropertiesNotInitialized__WithLocale_SetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem.Add(ContentItem.Locale, value);
            // Assert
            contentItem._locale.Should().Be(value);
        }

        [Fact]
        public void Add_WithPropertiesNotInitialized__WithMsbuild_SetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem.Add(ContentItem.MSBuild, value);
            // Assert
            contentItem._msbuild.Should().Be(value);
        }

        [Fact]
        public void Add_WithPropertiesNotInitialized__WithSatelliteAssembly_SetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem.Add(ContentItem.SatelliteAssembly, value);
            // Assert
            contentItem._satelliteAssembly.Should().Be(value);
        }

        [Fact]
        public void Add_WithPropertiesNotInitialized__WithCodeLanguage_SetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem.Add(ContentItem.CodeLanguage, value);
            // Assert
            contentItem._codeLanguage.Should().Be(value);
        }

        [Fact]
        public void Add_WithPropertiesNotInitialized__WithRelated_SetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem.Add(ContentItem.Related, value);
            // Assert
            contentItem._related.Should().Be(value);
        }

        [Fact]
        public void Add_WithPropertiesNotInitialized__WithTfmRaw_SetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem.Add(ContentItem.TfmRaw, value);
            // Assert
            contentItem._tfmRaw.Should().Be(value);
        }


        [Fact]
        public void TryGetValue_WithPropertiesNotInitialized__WithAssembly_GetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem._assembly = value;
            // Assert
            contentItem.TryGetValue(ContentItem.ManagedAssembly, out object result);
            result.Should().Be(value);
        }

        [Fact]
        public void TryGetValue_WithPropertiesNotInitialized__WithTfm_GetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem._tfm = value;
            // Assert
            contentItem.TryGetValue(ContentItem.TargetFrameworkMoniker, out object result);
            result.Should().Be(value);
        }

        [Fact]
        public void TryGetValue_WithPropertiesNotInitialized__WithRuntimeIdentifier_GetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem._rid = value;
            // Assert
            contentItem.TryGetValue(ContentItem.RuntimeIdentifier, out object result);
            result.Should().Be(value);
        }

        [Fact]
        public void TryGetValue_WithPropertiesNotInitialized__WithAny_GetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem._any = value;
            // Assert
            contentItem.TryGetValue(ContentItem.AnyValue, out object result);
            result.Should().Be(value);
        }

        [Fact]
        public void TryGetValue_WithPropertiesNotInitialized__WithLocale_GetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem._locale = value;
            // Assert
            contentItem.TryGetValue(ContentItem.Locale, out object result);
            result.Should().Be(value);
        }

        [Fact]
        public void TryGetValue_WithPropertiesNotInitialized__WithMsbuild_GetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem._msbuild = value;
            // Assert
            contentItem.TryGetValue(ContentItem.MSBuild, out object result);
            result.Should().Be(value);
        }

        [Fact]
        public void TryGetValue_WithPropertiesNotInitialized__WithSatelliteAssembly_GetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem._satelliteAssembly = value;
            // Assert
            contentItem.TryGetValue(ContentItem.SatelliteAssembly, out object result);
            result.Should().Be(value);
        }

        [Fact]
        public void TryGetValue_WithPropertiesNotInitialized__WithCodeLanguage_GetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem._codeLanguage = value;
            // Assert
            contentItem.TryGetValue(ContentItem.CodeLanguage, out object result);
            result.Should().Be(value);
        }

        [Fact]
        public void TryGetValue_WithPropertiesNotInitialized__WithRelated_GetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem._related = value;
            // Assert
            contentItem.TryGetValue(ContentItem.Related, out object result);
            result.Should().Be(value);
        }

        [Fact]
        public void TryGetValue_WithPropertiesNotInitialized__WithTfmRaw_GetsCorrectValue()
        {
            var contentItem = new ContentItem();
            var value = "value";
            // Act
            contentItem._tfmRaw = value;
            // Assert
            contentItem.TryGetValue(ContentItem.TfmRaw, out object result);
            result.Should().Be(value);
        }
    }
}
