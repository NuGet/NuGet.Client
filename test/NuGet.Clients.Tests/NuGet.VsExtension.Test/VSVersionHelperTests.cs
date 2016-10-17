// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using EnvDTE;
using Moq;
using NuGet.PackageManagement.VisualStudio;
using Xunit;

namespace NuGet.VsExtension.Test
{
    internal class VSVersionHelperTests
    {
        private readonly Mock<DTE> _mockDte = new Mock<DTE>();

        [Theory]
        [InlineData("14.0.247200.00")]
        [InlineData("14.0")]
        public void TestCorrectVSVersion_dev14(string versionString)
        {
            // Arrange
            _mockDte.Setup(x => x.Version).Returns(versionString);

            // Act
            var isDev14 = VSVersionHelper.IsDev14(_mockDte.Object);

            // Assert
            Assert.True(isDev14);
        }

        [Theory]
        [InlineData("15.0.247200.00")]
        [InlineData("15.0")]
        [InlineData("12.0")]
        [InlineData("13.0")]
        public void TestCorrectVSVersion_dev15(string versionString)
        {
            // Arrange
            _mockDte.Setup(x => x.Version).Returns(versionString);

            // Act
            var isDev14 = VSVersionHelper.IsDev14(_mockDte.Object);

            // Assert
            Assert.False(isDev14);
        }
    }
}