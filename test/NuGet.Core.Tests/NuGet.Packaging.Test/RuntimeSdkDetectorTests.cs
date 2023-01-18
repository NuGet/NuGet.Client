// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class RuntimeSdkDetectorTests
    {
        private static readonly Lazy<NuGetVersion> LazyExpectedSdkVersion = new(GetExpectedSdkVersion);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("file.dll")]
        [InlineData("./file.dll")]
        [InlineData("/file.dll")]
        [InlineData("/a/file.dll")]
        [InlineData(@"\\a\b\file.dll")]
        public void TryGetSdkVersion_WhenFileIsNotInVersionedSdkFolder_ReturnsFalse(string filePath)
        {
            bool actualResult = RuntimeSdkDetector.TryGetSdkVersion(filePath, out NuGetVersion version);

            Assert.False(actualResult);
            Assert.Null(version);
        }

        [Theory]
        [InlineData("7.0.200-preview.22628.1")]
        [InlineData("8.0.100")]
        public void TryGetSdkVersion_WhenFileIsInVersionedSdkFolder_ReturnsTrue(string expectedVersion)
        {
            string filePath = $"/home/user/.dotnet/sdk/{expectedVersion}/file.dll";
            bool actualResult = RuntimeSdkDetector.TryGetSdkVersion(filePath, out NuGetVersion version);

            Assert.True(actualResult);
            Assert.NotNull(version);
            Assert.Equal(expectedVersion, version.ToString());
        }

        [Fact]
        public void TryGetSdkVersion_Always_ReturnsValueForNuGetPackagingAssembly()
        {
            bool actualResult = RuntimeSdkDetector.TryGetSdkVersion(out NuGetVersion actualVersion);

            if (LazyExpectedSdkVersion.Value is null)
            {
                Assert.False(actualResult);
                Assert.Null(actualVersion);
            }
            else
            {
                Assert.True(actualResult);
                Assert.NotNull(actualVersion);
                Assert.Equal(LazyExpectedSdkVersion.Value, actualVersion);
            }
        }

        [Fact]
        public void Is8OrGreater_Always_ReturnsValueForNuGetPackagingAssembly()
        {
            bool is8OrGreater = RuntimeSdkDetector.Is8OrGreater;

            if (LazyExpectedSdkVersion.Value is null
                || LazyExpectedSdkVersion.Value.Version < new Version(8, 0, 0, 0))
            {
                Assert.False(is8OrGreater);
            }
            else
            {
                Assert.True(is8OrGreater);
            }
        }

        private static NuGetVersion GetExpectedSdkVersion()
        {
            string directoryName = new FileInfo(typeof(PackageArchiveReader).Assembly.Location).Directory.Name;

            if (NuGetVersion.TryParse(directoryName, out NuGetVersion expectedVersion))
            {
                return expectedVersion;
            }

            return null;
        }
    }
}
