// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Commands;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class MSBuildProjectFactoryTests
    {
        [PlatformTheory(Platform.Linux)]
        [InlineData("D:\\temp\\project1\\source.cs",                            "D:\\temp\\project1",                           "src\\project1\\source.cs")]
        public void MSBuildProjectFactory_GetTargetPathForSourceFiles(string sourcePath, string projectDirectory, string expectedResult)
        {
            var result = MSBuildProjectFactory.GetTargetPathForSourceFile(sourcePath, projectDirectory);
            Assert.Equal(expectedResult, result);
        }
    }
}
