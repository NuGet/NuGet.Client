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
    }
}
