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
        [InlineData(".NETFramework", "v4.5", "cf", "CompactFramework")]
        [InlineData(".NETFramework", "v4.5", "CF", "CompactFramework")]
        [InlineData(".NETFramework", "v4.5", "Full", "")]
        [InlineData(".NETFramework", "v4.5", null, "")]
        [InlineData(".NETFramework", "v4.5", "WP71", "WindowsPhone71")]
        [InlineData(".NETFramework", "v4.5", "WP", "WindowsPhone")]
        public void NuGetFramework_ProfileName(string tfm, string tfv, string tfp, string expected)
        {
            string actual = NuGetFramework.ParseComponents(tfm, tfv, tfp, null, null).Profile;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("net45", ".NETFramework", "v4.5", null, null, null)]
        [InlineData("net10", ".NETFramework", "v1.0", null, null, null)]
        [InlineData("net20", ".NETFramework", "v2.0", null, null, null)]
        [InlineData("net40", ".NETFramework", "v4.0", null, null, null)]
        [InlineData("net35", ".NETFramework", "v3.5", null, null, null)]
        [InlineData("net40-client", ".NETFramework", "v4.0", "Client", null, null)]
        [InlineData("net5.0", ".NETCoreApp", "5.0", null, null, null)]
        [InlineData("net5.0", ".NETCoreApp", "v5.0", null, null, null)]
        [InlineData("net5.0", "net", "5.0", null, null, null)]
        [InlineData("net5.0", "net", "v5.0", null, null, null)]
        [InlineData("net", ".NETFramework", "v0.0", null, null, null)]
        [InlineData("net10.1.2.3", ".NETFramework", "v10.1.2.3", null, null, null)]
        [InlineData("net10.0", ".NETFramework", "v10.0", null, null, null)]
        [InlineData("net45-cf", ".NETFramework", "v4.5", "CompactFramework", null, null)]
        [InlineData("uap10.0", "UAP", "v10.0", null, null, null)]
        [InlineData("dotnet", ".NETPlatform", "v0.0", null, null, null)]
        [InlineData("dotnet", ".NETPlatform", "v5.0", null, null, null)]
        [InlineData("netstandard", ".NETStandard", null, null, null, null)]
        [InlineData("netstandard1.0", ".NETStandard", "v1.0", null, null, null)]
        [InlineData("netstandard1.0", ".NETStandard", "v1.0.0", null, null, null)]
        [InlineData("netstandard0.9", ".NETStandard", "v0.9", null, null, null)]
        [InlineData("netstandard1.1", ".NETStandard", "v1.1", null, null, null)]
        [InlineData("netstandard1.2", ".NETStandard", "v1.2", null, null, null)]
        [InlineData("netstandard1.3", ".NETStandard", "v1.3", null, null, null)]
        [InlineData("netstandard1.4", ".NETStandard", "v1.4", null, null, null)]
        [InlineData("netstandard1.5", ".NETStandard", "v1.5", null, null, null)]
        [InlineData("netcoreapp", ".NETCoreApp", null, null, null, null)]
        [InlineData("netcoreapp1.0", ".NETCoreApp", "v1.0", null, null, null)]
        [InlineData("netcoreapp1.5", ".NETCoreApp", "v1.5", null, null, null)]
        [InlineData("netcoreapp2.0", ".NetCoreApp", "v2.0", null, null, null)]
        [InlineData("netcoreapp3.0", ".NetCoreApp", "v3.0", null, null, null)]
        [InlineData("net5.0-android", ".NETCoreApp", "v5.0", null, "android", null)]
        [InlineData("net5.0-android", ".NETCoreApp", "v5.0", null, "android", "0.0")]
        [InlineData("net5.0-android", ".NETCoreApp", "v5.0", null, "android", "")]
        [InlineData("net5.0-ios14.0", ".NETCoreApp", "v5.0", null, "ios", "14.0")]
        [InlineData("net5.0-macos10.0", ".NETCoreApp", "v5.0", null, "macos", "10.0")]
        [InlineData("net5.0-watchos1.0", ".NETCoreApp", "v5.0", null, "watchos", "1.0")]
        [InlineData("net5.0-tvos1.0", ".NETCoreApp", "v5.0", null, "tvos", "1.0")]
        [InlineData("net5.0-windows10.0", ".NETCoreApp", "v5.0", null, "windows", "10.0")]
        [InlineData("net5.0-macos10.15.2.3", ".NETCoreApp", "v5.0", null, "macos", "10.15.2.3")]
        [InlineData("unsupported", "unsupported", null, null, null, null)]
        // Scenarios where certain properties are ignored.
        [InlineData("netcoreapp3.0", ".NETCoreApp", "v3.0", null, "macos", "10.15.2.3")]
        [InlineData("netcoreapp3.0", ".NETCoreApp", "v3.0", null, "macos", null)]
        [InlineData("netcoreapp3.1", ".NETCoreApp", "v3.1", null, null, "10.15.2.3")]
        [InlineData("netcoreapp3.1-client", ".NETCoreApp", "v3.1", "client", null, "10.15.2.3")]
        [InlineData("netcoreapp3.1-client", ".NETCoreApp", "v3.1", "client", null, null)]
        [InlineData("netcoreapp3.0-client", ".NETCoreApp", "v3.0", "client", "Windows", "7.0")]
        [InlineData("netcoreapp3.0", ".NETCoreApp", "v3.0", null, "Windows", "7.0")]

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
        [InlineData(".NETCoreApp", "v5.0", null, null, null, "net5.0")]
        [InlineData("net", "v10.1.2.3", null, null, null, "net10.1.2.3")]
        [InlineData(".NETCoreApp", "v10.1.2.3", null, null, null, "net10.1.2.3")]
        [InlineData("netcoreapp", "5.0", null, null, null, "net5.0")]
        [InlineData("netcoreapp", "v5.0", null, null, null, "net5.0")]
        [InlineData(".NETCoreApp", "v5.0", null, "android", null, "net5.0-android")]
        [InlineData(".NETCoreApp", "v5.0", null, "ios", "14.0", "net5.0-ios14.0")]
        [InlineData("net", "472", null, "ios", "14.0", "net472.0-ios14.0")]

        // Pre-Net5.0 ERA
        [InlineData(".NETCoreApp", "v3.0", null, "Windows", "7.0", ".NETCoreApp,Version=v3.0")]
        [InlineData(".NETFramework", "v4.5", null, null, null, ".NETFramework,Version=v4.5")]
        [InlineData(".NETFramework", "v2.0", null, null, null, ".NETFramework,Version=v2.0")]
        [InlineData(".NETFramework", "4.0", null, null, null, ".NETFramework,Version=v4.0")]
        [InlineData(".NETFramework", "3.5", null, null, null, ".NETFramework,Version=v3.5")]
        [InlineData(".NETFramework", "4.0", "full", null, null, ".NETFramework,Version=v4.0")]
        [InlineData(".NETFramework", "4.0", "client", null, null, ".NETFramework,Version=v4.0,Profile=Client")]
        [InlineData(".NETFramework", null, null, null, null, ".NETFramework,Version=v0.0")]
        [InlineData(".NETFramework", "4.5", "cf", null, null, ".NETFramework,Version=v4.5,Profile=CompactFramework")]
        [InlineData("uap", "10.0", null, null, null, "UAP,Version=v10.0")]
        [InlineData("dotnet", null, null, null, null, ".NETPlatform,Version=v5.0")]
        [InlineData(".NETPlatform", null, null, null, null, ".NETPlatform,Version=v5.0")]
        [InlineData(".NETPlatform", "5", null, null, null, ".NETPlatform,Version=v5.0")]
        [InlineData(".NETPlatform", "5.0", null, null, null, ".NETPlatform,Version=v5.0")]
        [InlineData(".NETPlatform", "1.0", null, null, null, ".NETPlatform,Version=v1.0")]
        [InlineData(".NETPlatform", "10", null, null, null, ".NETPlatform,Version=v10.0")]
        [InlineData(".NETPlatform", "5.1", null, null, null, ".NETPlatform,Version=v5.1")]
        [InlineData(".NETPlatform", "5.2", null, null, null, ".NETPlatform,Version=v5.2")]
        [InlineData(".NETPlatform", "5.3", null, null, null, ".NETPlatform,Version=v5.3")]
        [InlineData(".NETPlatform", "5.4", null, null, null, ".NETPlatform,Version=v5.4")]
        [InlineData(".NETPlatform", "5.5", null, null, null, ".NETPlatform,Version=v5.5")]
        [InlineData("netstandard", "1.0", null, null, null, ".NETStandard,Version=v1.0")]
        [InlineData(".NETStandard", "1.0", null, null, null, ".NETStandard,Version=v1.0")]
        [InlineData(".NETStandard", "1.1", null, null, null, ".NETStandard,Version=v1.1")]
        [InlineData("netstandardapp", null, null, null, null, ".NETStandardApp,Version=v0.0")]
        [InlineData(".NETStandardApp", null, null, null, null, ".NETStandardApp,Version=v0.0")]
        [InlineData(".NETStandardApp", "0.0", null, null, null, ".NETStandardApp,Version=v0.0")]
        [InlineData(".NETStandardApp", "1", null, null, null, ".NETStandardApp,Version=v1.0")]
        [InlineData(".NETStandardApp", "1.5", null, null, null, ".NETStandardApp,Version=v1.5")]
        [InlineData(".NETStandardApp", "2", null, null, null, ".NETStandardApp,Version=v2.0")]
        [InlineData(".NETStandardApp", "2.1", null, null, null, ".NETStandardApp,Version=v2.1")]
        [InlineData("netcoreapp", null, null, null, null, ".NETCoreApp,Version=v0.0")]
        [InlineData(".NETCoreApp", null, null, null, null, ".NETCoreApp,Version=v0.0")]
        [InlineData(".NETCoreApp", "0.0", null, null, null, ".NETCoreApp,Version=v0.0")]
        [InlineData(".NETCoreApp", "1", null, null, null, ".NETCoreApp,Version=v1.0")]
        [InlineData(".NETCoreApp", "1.5", null, null, null, ".NETCoreApp,Version=v1.5")]
        [InlineData(".NETCoreApp", "2", null, null, null, ".NETCoreApp,Version=v2.0")]
        [InlineData(".NETCoreApp", "3", null, null, null, ".NETCoreApp,Version=v3.0")]
        [InlineData("unsupported", null, null, null, null, "Unsupported,Version=v0.0")]
        // Scenarios where certain properties are ignored.
        [InlineData(".NETCoreApp", "v3.0", null, "macos", "10.15.2.3", ".NETCoreApp,Version=v3.0")]
        [InlineData(".NETCoreApp", "v3.0", null, "macos", null, ".NETCoreApp,Version=v3.0")]
        [InlineData(".NETCoreApp", "v3.1", null, null, "10.15.2.3", ".NETCoreApp,Version=v3.1")]
        [InlineData(".NETCoreApp", "v3.1", "client", null, "10.15.2.3", ".NETCoreApp,Version=v3.1,Profile=Client")]
        [InlineData(".NETCoreApp", "v3.1", "client", null, null, ".NETCoreApp,Version=v3.1,Profile=Client")]
        [InlineData(".NETCoreApp", "v3.0", "client", "Windows", "7.0", ".NETCoreApp,Version=v3.0,Profile=Client")]
        public void NuGetFramework_Basic(string tfi, string tfv, string tfp, string tpi, string tpv, string fullName)
        {
            string output = NuGetFramework.ParseComponents(tfi, tfv, tfp, tpi, tpv).DotNetFrameworkName;

            Assert.Equal(fullName, output);
        }

        [Theory]
        [InlineData(null, "v1.0", null, "android", "v21.0")]
        [InlineData(".NETCoreApp", "vklmnfkjdfn5.0", null, null, null)]
        [InlineData(".NETCoreApp", "v5.0", null, "plat", "badversion")]
        public void NuGetFramework_WithInvalidProperties_Throws(string tfi, string tfv, string tfp, string tpi, string tpv)
        {
            Assert.ThrowsAny<Exception>(() => NuGetFramework.ParseComponents(tfi, tfv, tfp, tpi, tpv));
        }
    }
}
