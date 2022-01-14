// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test.PackageExtraction
{
    public class StreamExtensionsTests
    {
        [PlatformFact(Platform.Windows)]
        public void WriteFilesWithMMapOnWindows()
        {
            var environmentVariableReader = new Mock<IEnvironmentVariableReader>();
            var uut = new NuGet.Packaging.StreamExtensions.Testable(environmentVariableReader.Object);
            Assert.True(uut.IsMMapEnabled);
        }

        [PlatformFact(SkipPlatform = Platform.Windows)]
        public void WriteFilesWithFileStreamOnNonWindows()
        {
            var environmentVariableReader = new Mock<IEnvironmentVariableReader>();
            var uut = new NuGet.Packaging.StreamExtensions.Testable(environmentVariableReader.Object);
            Assert.False(uut.IsMMapEnabled);
        }

        [Theory]
        [InlineData("0", false)]
        [InlineData("1", true)]
        public void EnvironmentVariableIsRespected(string env, bool expected)
        {
            var environmentVariableReader = new Mock<IEnvironmentVariableReader>();
            environmentVariableReader.Setup(x => x.GetEnvironmentVariable("NUGET_PACKAGE_EXTRACTION_USE_MMAP"))
                .Returns(env);
            var uut = new NuGet.Packaging.StreamExtensions.Testable(environmentVariableReader.Object);
            Assert.Equal(uut.IsMMapEnabled, expected);
        }
    }
}
