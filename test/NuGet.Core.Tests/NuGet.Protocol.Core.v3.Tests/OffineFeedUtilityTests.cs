// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Tests
{
//Negative tests here won't run well on *nix because bad test data used will trigger new exceptions
//TODO: we can revisit to catch them if there is value.

#if !IS_CORECLR
    public class OffineFeedUtilityTests
    {
        [Theory]
        [InlineData("c:\\foo|<>|bar")]
        [InlineData("c:\\foo|<>|bar.nupkg")]
        public void OfflineFeedUtility_ThrowIfInvalid_Throws_PathInvalid(string path)
        {
            // Act & Assert
            var expectedMessage = string.Format("'{0}' is not a valid path.", path);

            var exception
                = Assert.Throws<ArgumentException>(() => OfflineFeedUtility.ThrowIfInvalid(path));

            Assert.Equal(expectedMessage, exception.Message);
        }

        [Theory]
        [InlineData("http://foonugetbar.org")]
        [InlineData("http://foonugetbar.org/A.nupkg")]
        public void OfflineFeedUtility_ThrowIfInvalid_Throws_Path_Invalid_NotFileNotUnc(string path)
        {
            // Act & Assert
            var expectedMessage = string.Format("'{0}' should be a local path or a UNC share path.", path);

            var exception
                = Assert.Throws<ArgumentException>(() => OfflineFeedUtility.ThrowIfInvalid(path));

            Assert.Equal(expectedMessage, exception.Message);
        }

        [Theory]
        [InlineData("foo\\bar")]
        [InlineData("c:\\foo\\bar")]
        [InlineData("\\foouncshare\\bar")]
        public void OfflineFeedUtility_ThrowIfInvalid_DoesNotThrow(string path)
        {
            // Act & Assert that the following call does not throw
            OfflineFeedUtility.ThrowIfInvalid(path);
        }

        [Theory]
        [InlineData("c:\\foobardoesnotexist", true)]
        [InlineData("foobardoesnotexist\\A.nupkg", false)]
        public void OfflineFeedUtility_ThrowIfInvalidOrNotFound_Throws(string path, bool isDirectory)
        {
            // Act & Assert
            var exception
                = Assert.Throws<ArgumentException>(()
                    => OfflineFeedUtility.ThrowIfInvalidOrNotFound(
                        path,
                        isDirectory,
                        "some exception message"));
        }
    }
#endif
}
