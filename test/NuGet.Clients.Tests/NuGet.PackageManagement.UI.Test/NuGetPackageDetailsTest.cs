// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class NuGetPackageDetailsTest
    {
        [Theory]
        [InlineData("Newtonsoft.Json", "1.2.3")]
        [InlineData("Moq", "2.3.4")]
        public void NuGetPackageDetails_BothPropertiesNotNull_BothPropertiesNotNull(string name, string version)
        {
            //Arrange
            NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

            //Act
            var packagedetails = new NuGetPackageDetails(name, nugetVersion);

            //Assert
            string actualPackageName = packagedetails.PackageName;
            NuGetVersion actualVersion = packagedetails.VersionNumber;
            Assert.Equal(name, actualPackageName);
            Assert.Equal(nugetVersion, actualVersion);
        }

        [Theory]
        [InlineData(null, "1.2.3")]
        [InlineData(null, "2.3.4")]
        public void NuGetPackageDetails_NameNullVersionNotNull_VersionPropertyNotNull(string name, string version)
        {
            //Arrange
            NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

            //Act
            var packagedetails = new NuGetPackageDetails(name, nugetVersion);

            //Assert
            string actualPackageName = packagedetails.PackageName;
            NuGetVersion actualVersion = packagedetails.VersionNumber;
            Assert.Null(actualPackageName);
            Assert.Equal(nugetVersion, actualVersion);
        }

        [Theory]
        [InlineData("Newtonsoft.Json", null)]
        [InlineData("Moq", null)]
        public void NuGetPackageDetails_NameNotNullVersionNull_NamePropertyNotNull(string name, string version)
        {
            //Arrange
            NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

            //Act
            var packagedetails = new NuGetPackageDetails(name, nugetVersion);

            //Assert
            string actualPackageName = packagedetails.PackageName;
            NuGetVersion actualVersion = packagedetails.VersionNumber;
            Assert.Equal(name, actualPackageName);
            Assert.Null(actualVersion);
        }

        [Theory]
        [InlineData(null, null)]
        public void NuGetPackageDetails_NameNullVersionNull_BothPropertiesNull(string name, string version)
        {
            //Arrange
            NuGetVersion.TryParse(version, out NuGetVersion nugetVersion);

            //Act
            var packagedetails = new NuGetPackageDetails(name, nugetVersion);

            //Assert
            string actualPackageName = packagedetails.PackageName;
            NuGetVersion actualVersion = packagedetails.VersionNumber;
            Assert.Null(actualPackageName);
            Assert.Null(actualVersion);
        }
    }
}
