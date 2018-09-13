// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
    }
}
