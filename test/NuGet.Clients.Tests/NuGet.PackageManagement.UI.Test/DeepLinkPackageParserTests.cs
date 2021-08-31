// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.PackageManagement.UI;
using Xunit;

namespace NuGet.Tools.Test
{
    public class DeepLinkPackageParserTests
    {
        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json/1.0.0", "Newtonsoft.Json", "1.0.0")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog/1.2.3", "Serilog", "1.2.3")]
        [InlineData("nuget-client://OpenPackageDetails/Moq/1.3.5", "Moq", "1.3.5")]
        public void URILinks_FormattedProperlyTwoProperties_ShouldBeValid(string packageLink, string packageName, string packageVersion)
        {
            //Arrange
            var package = new NuGetPackageDetails(packageName, packageVersion);

            //Act
            NuGetPackageDetails resultingPackage = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            string testPackageName = package.PackageName;
            string testPackageVersion = package.VersionNumber;

            string resultPackageName = resultingPackage.PackageName;
            string resultPackageVersion = resultingPackage.VersionNumber;

            Assert.NotNull(resultingPackage);
            Assert.Equal(testPackageName, resultPackageName);
            Assert.Equal(testPackageVersion, resultPackageVersion);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json", "Newtonsoft.Json")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog", "Serilog")]
        [InlineData("nuget-client://OpenPackageDetails/Moq", "Moq")]
        public void URILinks_FormattedProperlyOneProperty_ShouldBeValid(string packageLink, string packageName)
        {
            //Arrange
            var package = new NuGetPackageDetails(packageName);

            //Act
            NuGetPackageDetails resultingPackage = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            string testPackageName = package.PackageName;

            string resultPackageName = resultingPackage.PackageName;
            string resultPackageVersion = resultingPackage.VersionNumber;

            Assert.NotNull(resultingPackage);
            Assert.Equal(testPackageName, resultPackageName);
            Assert.Null(resultPackageVersion);
        }

        [Theory]
        [InlineData("nuget-client:://OpenPackageDetails/Newtonsoft.Json/1.0.0")]
        [InlineData("nuget-client:/a/OpenPackageDetails/Serilog/1.2.3")]
        [InlineData("vsah://OpenPackageDetails/Moq/1.3.5")]
        public void URILinks_BadProtocolName_ShouldBeNull(string packageLink)
        {
            //Arrange

            //Act
            NuGetPackageDetails result = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("nuget-client://OpenSesame/Newtonsoft.Json/1.0.0")]
        [InlineData("nuget-client://HEyHOwdidIgetHere/Serilog/1.2.3")]
        [InlineData("nuget-client://OpenPackageDetail/Moq/1.3.5")]
        public void URILinks_BadDomainName_ShouldBeNull(string packageLink)
        {
            //Arrange

            //Act
            NuGetPackageDetails result = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtsoft.Json/1.0.0/wazzap")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog/1.2.3/Im/messing/up/your/code")]
        [InlineData(null)]
        public void URILinks_NumberOfPropertiesIsNotTwoOrOne_ShouldBeNull(string packageLink)
        {
            //Arrange

            //Act
            NuGetPackageDetails result = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            Assert.Null(result);
        }
    }
}
