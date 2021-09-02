// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class DeepLinkPackageParserTests
    {
        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json/1.0.0", "Newtonsoft.Json", "1.0.0")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog/1.2.3", "Serilog", "1.2.3")]
        [InlineData("nuget-client://OpenPackageDetails/Moq/1.3.5", "Moq", "1.3.5")]
        public void TryParse_FormattedProperlyTwoProperties_ShouldBeValid(string packageLink, string packageName, string version)
        {
            //Arrange
            var packageVersion = NuGetVersion.Parse(version);
            var package = new NuGetPackageDetails(packageName, packageVersion);

            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out NuGetPackageDetails resultingPackage);

            //Assert
            string expectedPackageName = package.PackageName;
            NuGetVersion expectedPackageVersion = package.VersionNumber;

            string actualPackageName = resultingPackage.PackageName;
            NuGetVersion actualPackageVersion = resultingPackage.VersionNumber;

            Assert.NotNull(resultingPackage);
            Assert.True(result);
            Assert.Equal(expectedPackageName, actualPackageName);
            Assert.Equal(expectedPackageVersion, actualPackageVersion);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json/1.0.0/", "Newtonsoft.Json", "1.0.0")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog/1.2.3/wu/tang/clan", "Serilog", "1.2.3")]
        [InlineData("nuget-client://OpenPackageDetails/Moq/1.3.5/hello", "Moq", "1.3.5")]
        public void TryParse_FormattedProperlyTwoMainPropertiesPlusExtraProperties_ShouldBeValid(string packageLink, string packageName, string version)
        {
            //Arrange
            var packageVersion = NuGetVersion.Parse(version);
            var package = new NuGetPackageDetails(packageName, packageVersion);

            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out NuGetPackageDetails resultingPackage);

            //Assert
            string expectedPackageName = package.PackageName;
            NuGetVersion expectedPackageVersion = package.VersionNumber;

            string actualPackageName = resultingPackage.PackageName;
            NuGetVersion actualPackageVersion = resultingPackage.VersionNumber;

            Assert.NotNull(resultingPackage);
            Assert.True(result);
            Assert.Equal(expectedPackageName, actualPackageName);
            Assert.Equal(expectedPackageVersion, actualPackageVersion);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json", "Newtonsoft.Json")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog", "Serilog")]
        [InlineData("nuget-client://OpenPackageDetails/Moq", "Moq")]
        public void TryParse_FormattedProperlyOneProperty_ShouldBeValid(string packageLink, string packageName)
        {
            //Arrange
            var package = new NuGetPackageDetails(packageName, null);

            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out NuGetPackageDetails resultingPackage);

            //Assert
            string expectedPackageName = package.PackageName;

            string actualPackageName = resultingPackage.PackageName;
            NuGetVersion actualPackageVersion = resultingPackage.VersionNumber;

            Assert.NotNull(resultingPackage);
            Assert.True(result);
            Assert.Equal(expectedPackageName, actualPackageName);
            Assert.Null(actualPackageVersion);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json/helllo", "Newtonsoft.Json")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog/", "Serilog")]
        [InlineData("nuget-client://OpenPackageDetails/Moq/1.27.4.974.3.434", "Moq")]
        public void TryParse_VersionNumbersNotProper_LinkShouldBeValidAndVersionShouldBeNull(string packageLink, string packageName)
        {
            //Arrange
            var package = new NuGetPackageDetails(packageName, null);

            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out NuGetPackageDetails resultingPackage);

            //Assert
            string expectedPackageName = package.PackageName;

            string actualPackageName = resultingPackage.PackageName;
            NuGetVersion actualPackageVersion = resultingPackage.VersionNumber;

            Assert.NotNull(resultingPackage);
            Assert.True(result);
            Assert.Equal(expectedPackageName, actualPackageName);
            Assert.Null(actualPackageVersion);
        }

        [Theory]
        [InlineData("nuget-client:://OpenPackageDetails/Newtonsoft.Json/1.0.0")]
        [InlineData("nuget-client:/a/OpenPackageDetails/Serilog/1.2.3")]
        [InlineData("vsah://OpenPackageDetails/Moq/1.3.5")]
        [InlineData("OpenPackageDetails/Newtonsoft.Json/1.0.0")]
        public void TryParse_BadProtocolName_ShouldBeNull(string packageLink)
        {
            //Arrange
            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out NuGetPackageDetails resultingPackage);

            //Assert
            Assert.Null(resultingPackage);
            Assert.False(result);
        }

        [Theory]
        [InlineData("nuget-client://OpenSesame/Newtonsoft.Json/1.0.0")]
        [InlineData("nuget-client://HEyHOwdidIgetHere/Serilog/1.2.3")]
        [InlineData("nuget-client://OpenPackageDetail/Moq/1.3.5")]
        [InlineData("nuget-client://Newtonsoft.Json/1.0.0")]
        public void TryParse_BadDomainName_ShouldBeNull(string packageLink)
        {
            //Arrange
            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out NuGetPackageDetails resultingPackage);

            //Assert
            Assert.Null(resultingPackage);
            Assert.False(result);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/")]
        [InlineData("nuget-client://OpenPackageDetails")]
        [InlineData(null)]
        public void TryParse_NumberOfPropertiesIsNotTwoOrOne_ShouldBeNull(string packageLink)
        {
            //Arrange
            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out NuGetPackageDetails resultingPackage);

            //Assert
            Assert.Null(resultingPackage);
            Assert.False(result);
        }
    }
}
