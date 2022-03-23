// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class MsBuildToolsetTest
    {
        [Fact]
        public void WhenNullIsPassedForVersionParameterThenMsBuildVersionIsFetchedFromPath_Success()
        {
            //Arrange
            var msbuildPath = Util.GetMsbuildPathOnWindows();
            if (RuntimeEnvironmentHelper.IsMono && RuntimeEnvironmentHelper.IsMacOSX)
            {
                msbuildPath = @"/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/msbuild/15.0/bin/";
            }

            //Act
            var toolset = new MsBuildToolset(version: null, path: msbuildPath);

            //Assert
            Assert.Equal(msbuildPath, toolset.Path);
            Assert.False(toolset.ParsedVersion.CompareTo(new Version(15, 5)) < 0);
        }
    }
}
