// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
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
            string msbuildPath;

            //Arrange
            if (RuntimeEnvironmentHelper.IsMono && RuntimeEnvironmentHelper.IsMacOSX)
            {
                msbuildToolset = MsBuildUtility.GetMsBuildFromMonoPaths(userVersion: null);
                msbuildPath = msbuildToolset.Path;
            }
            else
            {
                msbuildPath = Util.GetMsbuildPathOnWindows();
            }

            //Act
            msbuildToolset = new MsBuildToolset(version: null, path: msbuildPath);

            //Assert
            string errorMessage = msbuildToolset.Path + " " + File.Exists(Path.Combine(msbuildPath, "msbuild.exe").ToString());
            Assert.Equal(msbuildPath, msbuildToolset.Path);
            Assert.True(msbuildToolset.ParsedVersion.CompareTo(new Version(0, 0)) > 0, errorMessage);
        }
    }
}
