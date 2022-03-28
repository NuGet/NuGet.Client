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
            MsBuildToolset msbuildToolset;

            if (RuntimeEnvironmentHelper.IsMono && RuntimeEnvironmentHelper.IsMacOSX)
            {
                msbuildToolset = MsBuildUtility.GetMsBuildFromMonoPaths(userVersion: null);
            }
            else
            {
                var msbuildPath = Util.GetMsbuildPathOnWindows();
                msbuildToolset = new MsBuildToolset(version: null, path: msbuildPath);
                Assert.Equal(msbuildPath, msbuildToolset.Path);
            }

            Assert.True(msbuildToolset.ParsedVersion.CompareTo(new Version()) > 0);
        }
    }
}
