// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Commands;
using NuGet.Frameworks;
using Test.Utility;
using Xunit;

namespace NuGet.Common.Test
{
    public class PackageSpecificWarningPropertiesTests
    {

        [Fact]
        public void PackageSpecificWanringProperties_DefaultValue()
        {

            // Arrange
            var properties = new PackageSpecificWarningProperties();

            // Assert
            Assert.False(properties.Contains(NuGetLogCode.NU1500, "test_libraryId"));
        }

        [Fact]
        public void PackageSpecificWanringProperties_AddsValue()
        {

            // Arrange
            var code = NuGetLogCode.NU1500;
            var libraryId = "test_libraryId";
            var targetFramework = NuGetFramework.Parse("net45");
            var properties = new PackageSpecificWarningProperties();
            properties.Add(code, libraryId, targetFramework);

            // Assert
            Assert.True(properties.Contains(code, libraryId, targetFramework));
            Assert.False(properties.Contains(code, libraryId, NuGetFramework.Parse("random_target_graph")));
            Assert.False(properties.Contains(code, libraryId));
        }

        [Fact]
        public void PackageSpecificWanringProperties_AddsRangeValue()
        {
            // Arrange
            var codes = new List<NuGetLogCode> { NuGetLogCode.NU1500, NuGetLogCode.NU1601, NuGetLogCode.NU1701 };
            var libraryId = "test_libraryId";
            var targetFramework = NuGetFramework.Parse("net45");
            var properties = new PackageSpecificWarningProperties();
            properties.AddRange(codes, libraryId, targetFramework);

            // Assert
            foreach (var code in codes)
            {
                Assert.True(properties.Contains(code, libraryId, targetFramework));
                Assert.False(properties.Contains(code, libraryId, NuGetFramework.Parse("random_target_graph")));
                Assert.False(properties.Contains(code, libraryId));
            }
        }

        [Fact]
        public void PackageSpecificWanringProperties_AddsValueWithGlobalTFM()
        {

            // Arrange
            var code = NuGetLogCode.NU1500;
            var libraryId = "test_libraryId";
            var properties = new PackageSpecificWarningProperties();
            properties.Add(code, libraryId);

            // Assert
            Assert.False(properties.Contains(code, libraryId, NuGetFramework.Parse("random_target_graph")));
            Assert.True(properties.Contains(code, libraryId));
        }

        [Fact]
        public void PackageSpecificWanringProperties_AddsRangeValueWithGlobalTFM()
        {

            // Arrange
            var codes = new List<NuGetLogCode> { NuGetLogCode.NU1500, NuGetLogCode.NU1601, NuGetLogCode.NU1701 };
            var libraryId = "test_libraryId";
            var properties = new PackageSpecificWarningProperties();
            properties.AddRange(codes, libraryId);

            // Assert
            foreach (var code in codes)
            {
                Assert.False(properties.Contains(code, libraryId, NuGetFramework.Parse("random_target_graph")));
                Assert.True(properties.Contains(code, libraryId));
            }
        }
    }
}
