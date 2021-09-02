// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.PackageManagement.UI;
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
        public void URILinks_FormattedProperlyTwoProperties_ShouldBeValid(string packageLink, string packageName, string version)
        {
            //Arrange
            var packageVersion = NuGetVersion.Parse(version);
            var package = new NuGetPackageDetails(packageName, packageVersion);

            //Act
            NuGetPackageDetails resultingPackage = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            string expectedPackageName = package.PackageName;
            NuGetVersion expectedPackageVersion = package.VersionNumber;

            string actualPackageName = resultingPackage.PackageName;
            NuGetVersion actualPackageVersion = resultingPackage.VersionNumber;

            Assert.NotNull(resultingPackage);
            Assert.Equal(expectedPackageName, actualPackageName);
            Assert.Equal(expectedPackageVersion, actualPackageVersion);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json/1.0.0/", "Newtonsoft.Json", "1.0.0")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog/1.2.3/wu/tang/clan", "Serilog", "1.2.3")]
        [InlineData("nuget-client://OpenPackageDetails/Moq/1.3.5/hello", "Moq", "1.3.5")]
        public void URILinks_FormattedProperlyTwoMainPropertiesPlusExtraProperties_ShouldBeValid(string packageLink, string packageName, string version)
        {
            //Arrange
            var packageVersion = NuGetVersion.Parse(version);
            var package = new NuGetPackageDetails(packageName, packageVersion);

            //Act
            NuGetPackageDetails resultingPackage = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            string expectedPackageName = package.PackageName;
            NuGetVersion expectedPackageVersion = package.VersionNumber;

            string actualPackageName = resultingPackage.PackageName;
            NuGetVersion actualPackageVersion = resultingPackage.VersionNumber;

            Assert.NotNull(resultingPackage);
            Assert.Equal(expectedPackageName, actualPackageName);
            Assert.Equal(expectedPackageVersion, actualPackageVersion);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json", "Newtonsoft.Json")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog", "Serilog")]
        [InlineData("nuget-client://OpenPackageDetails/Moq", "Moq")]
        public void URILinks_FormattedProperlyOneProperty_ShouldBeValid(string packageLink, string packageName)
        {
            //Arrange
            var package = new NuGetPackageDetails(packageName, null);

            //Act
            NuGetPackageDetails resultingPackage = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            string expectedPackageName = package.PackageName;

            string actualPackageName = resultingPackage.PackageName;
            NuGetVersion actualPackageVersion = resultingPackage.VersionNumber;

            Assert.NotNull(resultingPackage);
            Assert.Equal(expectedPackageName, actualPackageName);
            Assert.Null(actualPackageVersion);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json/helllo", "Newtonsoft.Json")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog/", "Serilog")]
        [InlineData("nuget-client://OpenPackageDetails/Moq/1.27.4.974.3.434", "Moq")]
        public void URILinks_VersionNumbersNotProper_LinkShouldBeValidAndVersionShouldBeNull(string packageLink, string packageName)
        {
            //Arrange
            var package = new NuGetPackageDetails(packageName, null);

            //Act
            NuGetPackageDetails resultingPackage = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            string expectedPackageName = package.PackageName;

            string actualPackageName = resultingPackage.PackageName;
            NuGetVersion actualPackageVersion = resultingPackage.VersionNumber;

            Assert.NotNull(resultingPackage);
            Assert.Equal(expectedPackageName, actualPackageName);
            Assert.Null(actualPackageVersion);
        }

        [Theory]
        [InlineData("nuget-client:://OpenPackageDetails/Newtonsoft.Json/1.0.0")]
        [InlineData("nuget-client:/a/OpenPackageDetails/Serilog/1.2.3")]
        [InlineData("vsah://OpenPackageDetails/Moq/1.3.5")]
        [InlineData("OpenPackageDetails/Newtonsoft.Json/1.0.0")]
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
        [InlineData("nuget-client://Newtonsoft.Json/1.0.0")]
        public void URILinks_BadDomainName_ShouldBeNull(string packageLink)
        {
            //Arrange

            //Act
            NuGetPackageDetails result = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/")]
        [InlineData("nuget-client://OpenPackageDetails")]
        [InlineData(null)]
        public void URILinks_NumberOfPropertiesIsNotTwoOrOne_ShouldBeNull(string packageLink)
        {
            //Arrange

            //Act
            NuGetPackageDetails result = DeepLinkURIParser.GetNuGetPackageDetails(packageLink);

            //Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("nuget-clients://OpenPackageDetails/Newtonsoft.Json/1.0.0")]
        [InlineData("nuget-client://OpenPackageDetail/Serilog/1.2.3")]
        [InlineData("nugt://OpenPackageDetails/Moq/1.3.5")]
        [InlineData(null)]
        public void TryParseURILinks_BadProtocolAndDomain_ShouldBeNull(string packageLink)
        {
            //Arrange
            string packageName;
            NuGetVersion nugetPackageVersion;
            string version = null;
            NuGetVersion.TryParse(version, out nugetPackageVersion);

            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out packageName, out nugetPackageVersion);

            //Assert
            Assert.False(result);
            Assert.Null(packageName);
            Assert.Null(nugetPackageVersion);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/")]
        [InlineData("nuget-client://OpenPackageDetails")]
        public void TryParseURILinks_NoPackageName_ShouldBeNull(string packageLink)
        {
            //Arrange
            string packageName;
            NuGetVersion nugetPackageVersion;
            string version = null;
            NuGetVersion.TryParse(version, out nugetPackageVersion);

            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out packageName, out nugetPackageVersion);

            //Assert
            Assert.False(result);
            Assert.Null(packageName);
            Assert.Null(nugetPackageVersion);
        }
        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json/1.0.0", "Newtonsoft.Json", "1.0.0")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog/1.2.3", "Serilog", "1.2.3")]
        [InlineData("nuget-client://OpenPackageDetails/Moq/1.3.5", "Moq", "1.3.5")]
        public void TryParseURILinks_ValidNameValidVersion_NotNull(string packageLink, string name, string version)
        {
            //Arrange
            string actualName;
            NuGetVersion actualVersion;

            NuGetVersion expectedNugetVersion;
            NuGetVersion.TryParse(version, out expectedNugetVersion);

            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out actualName, out actualVersion);

            //Assert
            Assert.True(result);
            Assert.Equal(name, actualName);
            Assert.Equal(expectedNugetVersion, actualVersion);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json", "Newtonsoft.Json")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog", "Serilog")]
        [InlineData("nuget-client://OpenPackageDetails/Moq", "Moq")]
        public void TryParseURILinks_NoVersion_NotNull(string packageLink, string name)
        {
            //Arrange
            string actualName;
            NuGetVersion actualVersion;

            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out actualName, out actualVersion);

            //Assert
            Assert.True(result);
            Assert.Equal(name, actualName);
            Assert.Null(actualVersion);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json/1.2.23.23.2.4.34.3", "Newtonsoft.Json")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog/whatsupmate", "Serilog")]
        [InlineData("nuget-client://OpenPackageDetails/Moq/", "Moq")]
        public void TryParseURILinks_ImproperVersion_NotNull(string packageLink, string name)
        {
            //Arrange
            string actualName;
            NuGetVersion actualVersion;

            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out actualName, out actualVersion);

            //Assert
            Assert.True(result);
            Assert.Equal(name, actualName);
            Assert.Null(actualVersion);
        }

        [Theory]
        [InlineData("nuget-client://OpenPackageDetails/Newtonsoft.Json/1.0.0/", "Newtonsoft.Json", "1.0.0")]
        [InlineData("nuget-client://OpenPackageDetails/Serilog/1.2.3/wu/tang/clan", "Serilog", "1.2.3")]
        [InlineData("nuget-client://OpenPackageDetails/Moq/1.3.5/hello", "Moq", "1.3.5")]
        public void TryParseURILinks_FormattedProperlyTwoMainPropertiesPlusExtraProperties_NotNull(string packageLink, string name, string version)
        {
            //Arrange
            string actualName;
            NuGetVersion actualVersion;

            NuGetVersion expectedNugetVersion;
            NuGetVersion.TryParse(version, out expectedNugetVersion);

            //Act
            bool result = DeepLinkURIParser.TryParse(packageLink, out actualName, out actualVersion);

            //Assert
            Assert.True(result);
            Assert.Equal(name, actualName);
            Assert.Equal(expectedNugetVersion, actualVersion);
        }
    }
}
