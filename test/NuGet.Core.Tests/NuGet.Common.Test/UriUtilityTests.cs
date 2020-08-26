// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Common.Test
{
    public class UriUtilityTests
    {
        [Theory]
        [InlineData("file:///test", "test")]
        [InlineData("file://test", "test")]
        [InlineData("https://api.nuget.org/v3/index.json", "https://api.nuget.org/v3/index.json")]
        [InlineData("a/b/c", "a/b/c")]
        [InlineData("", "")]
        [InlineData("ftp://test", "ftp://test")]
        [InlineData("a", "a")]
        [InlineData("..\\a", "..\\a")]
        public void UriUtility_GetLocalPath(string input, string expected)
        {
            // Arrange & Act
            var local = UriUtility.GetLocalPath(input);

            // Assert
            // Trim for xplat
            Assert.Equal(expected, local.TrimStart('\\').TrimStart('/'));
        }

        [Theory]
        [InlineData("test", "test")]
        [InlineData("test/../test2", "test2")]
        [InlineData("../test", "../test")]
        [InlineData("a/b/c", "a/b/c")]
        public void UriUtility_GetAbsolutePath_VerifyRelativePathCombined(string source, string relative)
        {
            using (var root = TestDirectory.Create())
            {
                var expected = Path.GetFullPath(Path.Combine(root, relative));

                var path = UriUtility.GetAbsolutePath(root, source);

                Assert.Equal(expected, path);
            }
        }

        [Fact]
        public void UriUtility_GetAbsolutePath_VerifyUrlPathUnchanged()
        {
            using (var root = TestDirectory.Create())
            {
                var source = "https://api.nuget.org/v3/index.json";

                var path = UriUtility.GetAbsolutePath(root, source);

                Assert.Equal(source, path);
            }
        }

        [Fact]
        public void UriUtility_GetAbsolutePath_VerifyAbsolutePathUnchanged()
        {
            using (var root = TestDirectory.Create())
            using (var root2 = TestDirectory.Create())
            {
                var source = root2;

                var path = UriUtility.GetAbsolutePath(root, source);

                Assert.Equal(source, path);
            }
        }
    }
}
