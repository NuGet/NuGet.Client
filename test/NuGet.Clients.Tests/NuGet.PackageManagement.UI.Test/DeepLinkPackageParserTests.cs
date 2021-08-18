// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.PackageManagement.UI;
using Xunit;

namespace NuGet.Tools.Test
{
    public class DeepLinkPackageParserTests
    {
        private readonly List<string> _fullyFunctionalLinks = new List<string>
            {
                "vsph://OpenPackageDetails/Newtsoft.Json/1.0.0.0",
                "vsph://OpenPackageDetails/Serilog/1.2.3",
                "vsph://OpenPackageDetails/Moq/1.3.5",
            };

        private readonly List<string> _linksWithBadProtocols = new List<string>
            {
                "vsph:://OpenPackageDetails/Newtsoft.Json/1.0.0.0",
                "vsph:/a/OpenPackageDetails/Serilog/1.2.3",
                "vsah://OpenPackageDetails/Moq/1.3.5",
            };

        [Theory]
        [InlineData("vsph://OpenPackageDetails/Newtsoft.Json/1.0.0.0", "Newtsoft.Json", "1.0.0")]
        [InlineData("vsph://OpenPackageDetails/Serilog/1.2.3", "Serilog", "1.2.3")]
        [InlineData("vsph://OpenPackageDetails/Moq/1.3.5", "Moq", "1.3.5")]
        public void TestFullyFuntionalURILinks(string packageLink, string packageName, string packageVersion)
        {
            //Arrange
            var package = new NuGetPackageDetails(packageName, packageVersion);

            //Act
            var resultingPackage = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            string testPackageName = package.PackageName;
            string testPackageVersion = package.VersionNumber;

            string resultPackageName = resultingPackage.PackageName;
            string resultPackageVersion = resultingPackage.VersionNumber;

            Assert.Equal(testPackageName, resultPackageName);
            Assert.Equal(testPackageVersion, resultPackageVersion);
        }

        [Theory]
        [InlineData("vsph:://OpenPackageDetails/Newtsoft.Json/1.0.0.0")]
        [InlineData("vsph:/a/OpenPackageDetails/Serilog/1.2.3")]
        [InlineData("vsah://OpenPackageDetails/Moq/1.3.5")]
        public void TestURILinksWithBadProtocols(string packageLink)
        {
            //Arrange

            //Act
            var result = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("vsph://OpenSesame/Newtsoft.Json/1.0.0.0")]
        [InlineData("vsph://HEyHOwdidIgetHere/Serilog/1.2.3")]
        [InlineData("vsph://OpenPackageDetail/Moq/1.3.5")]
        public void TestURILinksWithBadDomains(string packageLink)
        {
            //Arrange

            //Act
            var result = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("vsph://OpenPackageDetails/Newtsoft.Json/1.0.0.0/wazzap")]
        [InlineData("vsph://OpenPackageDetails/Serilog/1.2.3/Im/messing/up/your/code")]
        [InlineData("vsph://OpenPackageDetails/Moq")]
        public void TestURILinksWithInappropriateNumberOfProperties(string packageLink)
        {
            //Arrange

            //Act
            var result = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            Assert.Null(result);
        }

        [Fact]
        public void TestNullLink()
        {
            //Arrange
            string packageLink = null;

            //Act
            var result = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            Assert.Null(result);
        }
    }
}
