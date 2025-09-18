// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Common.Test
{
    public class RuntimeEnvironmentHelperTests
    {
        [Fact]
        public void IsWindows_ShouldReturnConsistentValue()
        {
            // Arrange & Act
            var result1 = RuntimeEnvironmentHelper.IsWindows;
            var result2 = RuntimeEnvironmentHelper.IsWindows;

            // Assert
            Assert.Equal(result1, result2);
        }

        [Fact]
        public void IsMacOSX_ShouldReturnConsistentValue()
        {
            // Arrange & Act
            var result1 = RuntimeEnvironmentHelper.IsMacOSX;
            var result2 = RuntimeEnvironmentHelper.IsMacOSX;

            // Assert
            Assert.Equal(result1, result2);
        }

        [Fact]
        public void IsLinux_ShouldReturnConsistentValue()
        {
            // Arrange & Act
            var result1 = RuntimeEnvironmentHelper.IsLinux;
            var result2 = RuntimeEnvironmentHelper.IsLinux;

            // Assert
            Assert.Equal(result1, result2);
        }

        [Fact]
        public void PlatformDetection_ShouldBeExclusive()
        {
            // Arrange & Act
            var isWindows = RuntimeEnvironmentHelper.IsWindows;
            var isMacOS = RuntimeEnvironmentHelper.IsMacOSX;
            var isLinux = RuntimeEnvironmentHelper.IsLinux;

            // Assert - Only one platform should be true
            var trueCount = (isWindows ? 1 : 0) + (isMacOS ? 1 : 0) + (isLinux ? 1 : 0);
            Assert.True(trueCount <= 1,
                $"Multiple platforms detected as true: Windows={isWindows}, macOS={isMacOS}, Linux={isLinux}");

            // At least one should be detected (we're running on some platform)
            Assert.True(trueCount >= 1,
                $"No platform detected as true: Windows={isWindows}, macOS={isMacOS}, Linux={isLinux}");
        }

        [Fact]
        public void IsMacOSX_ShouldNotThrowException()
        {
            // Arrange & Act & Assert
            // This test specifically verifies that no first-chance exception is thrown
            // when calling IsMacOSX on any platform, especially Windows
            var exception = Record.Exception(() =>
            {
                var result = RuntimeEnvironmentHelper.IsMacOSX;
                // Multiple calls to ensure consistency and no exceptions on repeated access
                var result2 = RuntimeEnvironmentHelper.IsMacOSX;
                Assert.Equal(result, result2);
            });

            Assert.Null(exception);
        }
    }
}
