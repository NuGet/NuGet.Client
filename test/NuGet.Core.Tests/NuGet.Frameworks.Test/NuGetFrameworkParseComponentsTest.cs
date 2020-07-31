// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.Test
{
    public class NuGetFrameworkParseComponentsTests
    {
        [Theory]
        [InlineData("net", "4.5", "cf", "CompactFramework")]
        [InlineData("net", "4.5", "CF", "CompactFramework")]
        [InlineData("net", "4.5", "Full", "")]
        [InlineData("net", "4.5", null, "")]
        [InlineData("net", "4.5", "WP71", "WindowsPhone71")]
        [InlineData("net", "4.5", "WP", "WindowsPhone")]
        public void NuGetFramework_ProfileName(string tfm, string tfv, string tfp, string expected)
        {
            string actual = NuGetFramework.ParseComponents(tfm, tfv, tfp, null, null).Profile;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("net45", ".NETFramework", "4.5", null, null, null)]
        [InlineData("net10", ".NETFramework", "1.0", null, null, null)]
        [InlineData("net20", ".NETFramework", "2.0", null, null, null)]
        [InlineData("net40", ".NETFramework", "4.0", null, null, null)]
        [InlineData("net35", ".NETFramework", "3.5", null, null, null)]
        [InlineData("net40-client", ".NETFramework", "4.0", "Client", null, null)]
        [InlineData("net5.0", ".NetCoreApp", "5.0", null, null, null)]
        [InlineData("net5.0", "net", "5.0", null, null, null)]
        [InlineData("net", ".NETFramework", "0.0", null, null, null)]
        [InlineData("net10.1.2.3", ".NETFramework", "10.1.2.3", null, null, null)]
        [InlineData("net10.0", ".NETFramework", "10.0", null, null, null)]
        [InlineData("net45-cf", ".NETFramework", "4.5", "CompactFramework", null, null)]
        [InlineData("uap10.0", "UAP", "10.0", null, null, null)]
        [InlineData("dotnet", ".NETPlatform", "0.0", null, null, null)]
        [InlineData("dotnet", ".NETPlatform", "5.0", null, null, null)]
        [InlineData("netstandard", ".NETStandard", null, null, null, null)]
        [InlineData("netstandard1.0", ".NETStandard", "1.0", null, null, null)]
        [InlineData("netstandard1.0", ".NETStandard", "1.0.0", null, null, null)]
        [InlineData("netstandard0.9", ".NETStandard", "0.9", null, null, null)]
        [InlineData("netstandard1.1", ".NETStandard", "1.1", null, null, null)]
        [InlineData("netstandard1.2", ".NETStandard", "1.2", null, null, null)]
        [InlineData("netstandard1.3", ".NETStandard", "1.3", null, null, null)]
        [InlineData("netstandard1.4", ".NETStandard", "1.4", null, null, null)]
        [InlineData("netstandard1.5", ".NETStandard", "1.5", null, null, null)]
        [InlineData("netcoreapp", ".NETCoreApp", null, null, null, null)]
        [InlineData("netcoreapp1.0", ".NETCoreApp", "1.0", null, null, null)]
        [InlineData("netcoreapp1.5", ".NETCoreApp", "1.5", null, null, null)]
        [InlineData("netcoreapp2.0", ".NetCoreApp", "2.0", null, null, null)]
        [InlineData("netcoreapp3.0", ".NetCoreApp", "3.0", null, null, null)]
        [InlineData("net5.0-android", "net", "5.0", null, "android", null)]
        [InlineData("net5.0-android", "net", "5.0", null, "android", "0.0")]
        [InlineData("net5.0-android", "net", "5.0", null, "android", "")]
        [InlineData("net5.0-ios14.0", "net", "5.0", null, "ios", "14.0")]
        [InlineData("net5.0-macos10.0", "net", "5.0", null, "macos", "10.0")]
        [InlineData("net5.0-watchos1.0", "net", "5.0", null, "watchos", "1.0")]
        [InlineData("net5.0-tvos1.0", "net", "5.0", null, "tvos", "1.0")]
        [InlineData("net5.0-windows10.0", "net", "5.0", null, "windows", "10.0")]
        [InlineData("net5.0-macos10.15.2.3", "net", "5.0", null, "macos", "10.15.2.3")]
        public void NuGetFramework_ParseToShortName(string expected, string tfi, string tfv, string tfp, string tpi, string tpv)
        {
            // Arrange
            var framework = NuGetFramework.ParseComponents(tfi, tfv, tfp, tpi, tpv);

            // Act
            var shortName = framework.GetShortFolderName();

            // Assert
            Assert.Equal(expected, shortName);
        }

        [Theory]
        // Net5.0 ERA
        [InlineData("net", "5.0", null, null, null, "net5.0")]
        [InlineData("net", "10.1.2.3", null, null, null, "net10.1.2.3")]
        [InlineData("netcoreapp", "5.0", null, null, null, "net5.0")]
        [InlineData("net", "5.0", null, "android", null, "net5.0-android")]
        [InlineData("net", "5.0", null, "ios", "14.0", "net5.0-ios14.0")]
        [InlineData("net", "472", null, "ios", "14.0", "net472.0-ios14.0")]

        // Pre-Net5.0 ERA
        [InlineData("net", "4.5", null, null, null, ".NETFramework,Version=v4.5")]
        [InlineData("net", "2.0", null, null, null, ".NETFramework,Version=v2.0")]
        [InlineData("net", "4.0", null, null, null, ".NETFramework,Version=v4.0")]
        [InlineData("net", "3.5", null, null, null, ".NETFramework,Version=v3.5")]
        [InlineData("net", "4.0", "full", null, null, ".NETFramework,Version=v4.0")]
        [InlineData("net", "4.0", "client", null, null, ".NETFramework,Version=v4.0,Profile=Client")]
        [InlineData("net", null, null, null, null, ".NETFramework,Version=v0.0")]
        [InlineData("net", "4.5", "cf", null, null, ".NETFramework,Version=v4.5,Profile=CompactFramework")]
        [InlineData("uap", "10.0", null, null, null, "UAP,Version=v10.0")]
        [InlineData("dotnet", null, null, null, null, ".NETPlatform,Version=v5.0")]
        [InlineData("dotnet", "5", null, null, null, ".NETPlatform,Version=v5.0")]
        [InlineData("dotnet", "5.0", null, null, null, ".NETPlatform,Version=v5.0")]
        [InlineData("dotnet", "1.0", null, null, null, ".NETPlatform,Version=v1.0")]
        [InlineData("dotnet", "10", null, null, null, ".NETPlatform,Version=v10.0")]
        [InlineData("dotnet", "5.1", null, null, null, ".NETPlatform,Version=v5.1")]
        [InlineData("dotnet", "5.2", null, null, null, ".NETPlatform,Version=v5.2")]
        [InlineData("dotnet", "5.3", null, null, null, ".NETPlatform,Version=v5.3")]
        [InlineData("dotnet", "5.4", null, null, null, ".NETPlatform,Version=v5.4")]
        [InlineData("dotnet", "5.5", null, null, null, ".NETPlatform,Version=v5.5")]
        [InlineData("netstandard", "1.0", null, null, null, ".NETStandard,Version=v1.0")]
        [InlineData("netstandard", "1.1", null, null, null, ".NETStandard,Version=v1.1")]
        [InlineData("netstandardapp", null, null, null, null, ".NETStandardApp,Version=v0.0")]
        [InlineData("netstandardapp", "0.0", null, null, null, ".NETStandardApp,Version=v0.0")]
        [InlineData("netstandardapp", "1", null, null, null, ".NETStandardApp,Version=v1.0")]
        [InlineData("netstandardapp", "1.5", null, null, null, ".NETStandardApp,Version=v1.5")]
        [InlineData("netstandardapp", "2", null, null, null, ".NETStandardApp,Version=v2.0")]
        [InlineData("netstandardapp", "2.1", null, null, null, ".NETStandardApp,Version=v2.1")]
        [InlineData("netcoreapp", null, null, null, null, ".NETCoreApp,Version=v0.0")]
        [InlineData("netcoreapp", "0.0", null, null, null, ".NETCoreApp,Version=v0.0")]
        [InlineData("netcoreapp", "1", null, null, null, ".NETCoreApp,Version=v1.0")]
        [InlineData("netcoreapp", "1.5", null, null, null, ".NETCoreApp,Version=v1.5")]
        [InlineData("netcoreapp", "2", null, null, null, ".NETCoreApp,Version=v2.0")]
        [InlineData("netcoreapp", "3", null, null, null, ".NETCoreApp,Version=v3.0")]
        public void NuGetFramework_Basic(string tfi, string tfv, string tfp, string tpi, string tpv, string fullName)
        {
            string output = NuGetFramework.ParseComponents(tfi, tfv, tfp, tpi, tpv).DotNetFrameworkName;

            Assert.Equal(fullName, output);
        }
    }
}
