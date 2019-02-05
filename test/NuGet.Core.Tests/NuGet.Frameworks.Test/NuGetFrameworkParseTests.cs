// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.Test
{
    public class NuGetFrameworkParseTests
    {
        [Fact]
        public void NuGetFramework_Mixed()
        {
            string actual = NuGetFramework.Parse(".NETFramework3.5").GetShortFolderName();

            Assert.Equal("net35", actual);
        }

        [Fact]
        public void NuGetFramework_Decimals()
        {
            string actual = NuGetFramework.Parse("Win10.1.2.3").GetShortFolderName();

            Assert.Equal("win10.1.2.3", actual);
        }

        [Theory]
        [InlineData("11")]
        [InlineData("46")]
        [InlineData("30")]
        public void NuGetFramework_NumericUnsupported(string input)
        {
            // These frameworks are deprecated and unsupported
            string actual = NuGetFramework.Parse(input).DotNetFrameworkName;

            Assert.Equal(NuGetFramework.UnsupportedFramework.DotNetFrameworkName, actual);
        }

        [Theory]
        [InlineData("45", "net45")]
        [InlineData("40", "net40")]
        [InlineData("35", "net35")]
        [InlineData("20", "net20")]
        [InlineData("4.5", "net45")]
        [InlineData("4", "net40")]
        [InlineData("4.0", "net40")]
        [InlineData("3.5", "net35")]
        [InlineData("2", "net20")]
        [InlineData("2.0", "net20")]
        public void NuGetFramework_Numeric(string input, string expected)
        {
            string actual = NuGetFramework.Parse(input).GetShortFolderName();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NuGetFramework_SpecialNamesToDotNetFrameworkName()
        {
            Assert.Equal("Any,Version=v0.0", NuGetFramework.AnyFramework.DotNetFrameworkName);
            Assert.Equal("Agnostic,Version=v0.0", NuGetFramework.AgnosticFramework.DotNetFrameworkName);
            Assert.Equal("Unsupported,Version=v0.0", NuGetFramework.UnsupportedFramework.DotNetFrameworkName);
        }

        [Fact]
        public void NuGetFramework_PortableRoundTrip()
        {
            NuGetFramework framework = NuGetFramework.Parse("portable-net45+win8+wp8+wpa81");

            Assert.Equal("portable-net45+win8+wp8+wpa81", framework.GetShortFolderName());
        }

        [Fact]
        public void NuGetFramework_PortableSingleMoniker()
        {
            NuGetFramework framework = NuGetFramework.Parse("portable-net45");

            Assert.Equal("portable-net45", framework.GetShortFolderName());
        }

        [Fact]
        public void NuGetFramework_PortableZeroMoniker()
        {
            NuGetFramework framework = NuGetFramework.Parse("portable");

            Assert.True(framework.IsUnsupported);
        }

        [Fact]
        public void NuGetFramework_PortableNormalizeOptional()
        {
            NuGetFramework framework = NuGetFramework.Parse("portable-net45+win8+wp8+wpa81+monotouch+monoandroid");

            // Optional frameworks are removed by default
            Assert.Equal("portable-net45+win8+wp8+wpa81", framework.GetShortFolderName());
        }

        [Fact]
        public void NuGetFramework_PortableWithOptional()
        {
            NuGetFramework framework = NuGetFramework.Parse("portable-net4%2Bsl5%2Bwp8%2Bwin8%2Bwpa81%2Bmonotouch%2Bmonoandroid");

            Assert.Equal(".NETPortable,Version=v0.0,Profile=Profile328", framework.DotNetFrameworkName);
        }

        [Fact]
        public void NuGetFramework_PortableWithAny()
        {
            NuGetFramework framework = NuGetFramework.Parse("portable-win%2Bnet45%2Bwp8");

            Assert.Equal(".NETPortable,Version=v0.0,Profile=Profile78", framework.DotNetFrameworkName);
        }

        [Fact]
        public void NuGetFramework_IncludeUnknownProfile()
        {
            string actual = NuGetFramework.Parse("net45-custom").DotNetFrameworkName;

            Assert.Equal(".NETFramework,Version=v4.5,Profile=custom", actual);
        }

        [Theory]
        [InlineData(".NETPortable40-Profile1", ".NETPortable,Version=v4.0,Profile=Profile1")]
        [InlineData(".NETPortable-Profile1", ".NETPortable,Version=v0.0,Profile=Profile1")]
        [InlineData(".NETPortable-net45+win8", ".NETPortable,Version=v0.0,Profile=Profile7")]
        public void NuGetFramework_PortableMixed(string input, string expected)
        {
            string actual = NuGetFramework.Parse(input).DotNetFrameworkName;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("foo45", "Unsupported,Version=v0.0")]
        [InlineData("", "Unsupported,Version=v0.0")]
        public void NuGetFramework_ParseUnknown(string input, string expected)
        {
            string actual = NuGetFramework.Parse(input).DotNetFrameworkName;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(".NETPlatform50", ".NETPlatform,Version=v5.0")]
        [InlineData(".NETFramework45", ".NETFramework,Version=v4.5")]
        [InlineData("Portable-net45+win8", ".NETPortable,Version=v0.0,Profile=Profile7")]
        [InlineData("windows8", "Windows,Version=v8.0")]
        [InlineData("windowsphone8", "WindowsPhone,Version=v8.0")]
        public void NuGetFramework_PartialFull(string input, string expected)
        {
            string actual = NuGetFramework.Parse(input).DotNetFrameworkName;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(".NETPlatform,Version=v1.0", ".NETPlatform,Version=v1.0")]
        [InlineData(".NETPlatform,Version=v0.0", ".NETPlatform,Version=v5.0")]
        [InlineData(".NETPlatform,Version=v5.0", ".NETPlatform,Version=v5.0")]
        [InlineData(".NETFramework,Version=v4.5", ".NETFramework,Version=v4.5")]
        [InlineData("NETFramework,Version=v4.5", ".NETFramework,Version=v4.5")]
        [InlineData(".NETPortable,Version=v0.0,Profile=Profile7", ".NETPortable,Version=v0.0,Profile=Profile7")]
        [InlineData("Portable,Version=v0.0,Profile=Profile7", ".NETPortable,Version=v0.0,Profile=Profile7")]
        [InlineData("uap,Version=v10.0.10030.1", "UAP,Version=v10.0.10030.1")]
        public void NuGetFramework_ParseFullName(string input, string expected)
        {
            string actual = NuGetFramework.Parse(input).DotNetFrameworkName;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("portable-net45+win10.0", ".NETPortable,Version=v0.0,Profile=net45+win10.0")]
        [InlineData("portable-net45+win8", ".NETPortable,Version=v0.0,Profile=Profile7")]
        [InlineData("portable-win8+net45", ".NETPortable,Version=v0.0,Profile=Profile7")]
        [InlineData("portable-win8+net45+monoandroid1+monotouch1", ".NETPortable,Version=v0.0,Profile=Profile7")]
        public void NuGetFramework_Portable(string folder, string expected)
        {
            string actual = NuGetFramework.Parse(folder).DotNetFrameworkName;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("net45-cf", "CompactFramework")]
        [InlineData("net45-CF", "CompactFramework")]
        [InlineData("net45-Full", "")]
        [InlineData("net45", "")]
        [InlineData("net45-WP71", "WindowsPhone71")]
        [InlineData("net45-WP", "WindowsPhone")]
        public void NuGetFramework_ProfileName(string folder, string expected)
        {
            string actual = NuGetFramework.Parse(folder).Profile;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("net45", ".NETFramework,Version=v4.5")]
        [InlineData("net20", ".NETFramework,Version=v2.0")]
        [InlineData("net40", ".NETFramework,Version=v4.0")]
        [InlineData("net35", ".NETFramework,Version=v3.5")]
        [InlineData("net40-client", ".NETFramework,Version=v4.0,Profile=Client")]
        [InlineData("net", ".NETFramework,Version=v0.0")]
        [InlineData("net10.1.2.3", ".NETFramework,Version=v10.1.2.3")]
        [InlineData("net45-cf", ".NETFramework,Version=v4.5,Profile=CompactFramework")]
        [InlineData("uap10.0", "UAP,Version=v10.0")]
        [InlineData("dotnet", ".NETPlatform,Version=v0.0")]
        [InlineData("dotnet", ".NETPlatform,Version=v5.0")]
        [InlineData("dotnet1.0", ".NETPlatform,Version=v1.0")]
        [InlineData("dotnet5.1", ".NETPlatform,Version=v5.1")]
        [InlineData("dotnet5.2", ".NETPlatform,Version=v5.2")]
        [InlineData("dotnet5.3", ".NETPlatform,Version=v5.3")]
        [InlineData("dotnet5.4", ".NETPlatform,Version=v5.4")]
        [InlineData("dotnet5.5", ".NETPlatform,Version=v5.5")]
        [InlineData("dotnet6.0", ".NETPlatform,Version=v6.0")]
        [InlineData("dotnet6.0", ".NETPlatform,Version=v6")]
        [InlineData("netstandard", ".NETStandard,Version=v0.0")]
        [InlineData("netstandard1.0", ".NETStandard,Version=v1.0")]
        [InlineData("netstandard1.0", ".NETStandard,Version=v1.0.0")]
        [InlineData("netstandard0.9", ".NETStandard,Version=v0.9")]
        [InlineData("netstandard1.1", ".NETStandard,Version=v1.1")]
        [InlineData("netstandard1.2", ".NETStandard,Version=v1.2")]
        [InlineData("netstandard1.3", ".NETStandard,Version=v1.3")]
        [InlineData("netstandard1.4", ".NETStandard,Version=v1.4")]
        [InlineData("netstandard1.5", ".NETStandard,Version=v1.5")]
        [InlineData("netstandardapp", ".NETStandardApp,Version=v0.0")]
        [InlineData("netstandardapp1.0", ".NETStandardApp,Version=v1.0")]
        [InlineData("netstandardapp1.5", ".NETStandardApp,Version=v1.5")]
        [InlineData("netstandardapp2.0", ".NETStandardApp,Version=v2.0")]
        [InlineData("netstandardapp2.1", ".NETStandardApp,Version=v2.1")]
        [InlineData("netcoreapp", ".NETCoreApp,Version=v0.0")]
        [InlineData("netcoreapp1.0", ".NETCoreApp,Version=v1.0")]
        [InlineData("netcoreapp1.5", ".NETCoreApp,Version=v1.5")]
        [InlineData("netcoreapp2.0", ".NetCoreApp,Version=v2.0")]
        [InlineData("netcoreapp3.0", ".NetCoreApp,Version=v3.0")]
        public void NuGetFramework_ParseToShortName(string expected, string fullName)
        {
            // Arrange
            var framework = NuGetFramework.Parse(fullName);

            // Act
            var shortName = framework.GetShortFolderName();

            // Assert
            Assert.Equal(expected, shortName);
        }

        [Theory]
        [InlineData("net45", ".NETFramework,Version=v4.5")]
        [InlineData("net20", ".NETFramework,Version=v2.0")]
        [InlineData("net40", ".NETFramework,Version=v4.0")]
        [InlineData("net35", ".NETFramework,Version=v3.5")]
        [InlineData("net40-full", ".NETFramework,Version=v4.0")]
        [InlineData("net40-client", ".NETFramework,Version=v4.0,Profile=Client")]
        [InlineData("net", ".NETFramework,Version=v0.0")]
        [InlineData("net10.1.2.3", ".NETFramework,Version=v10.1.2.3")]
        [InlineData("net45-cf", ".NETFramework,Version=v4.5,Profile=CompactFramework")]
        [InlineData("uap10.0", "UAP,Version=v10.0")]
        [InlineData("dotnet", ".NETPlatform,Version=v5.0")]
        [InlineData("dotnet5", ".NETPlatform,Version=v5.0")]
        [InlineData("dotnet50", ".NETPlatform,Version=v5.0")]
        [InlineData("dotnet10", ".NETPlatform,Version=v1.0")]
        [InlineData("dotnet5.1", ".NETPlatform,Version=v5.1")]
        [InlineData("dotnet5.2", ".NETPlatform,Version=v5.2")]
        [InlineData("dotnet5.3", ".NETPlatform,Version=v5.3")]
        [InlineData("dotnet5.4", ".NETPlatform,Version=v5.4")]
        [InlineData("dotnet5.5", ".NETPlatform,Version=v5.5")]
        [InlineData("netstandard1.0", ".NETStandard,Version=v1.0")]
        [InlineData("netstandard1.1", ".NETStandard,Version=v1.1")]
        [InlineData("netstandard1.2", ".NETStandard,Version=v1.2")]
        [InlineData("netstandard1.3", ".NETStandard,Version=v1.3")]
        [InlineData("netstandard1.4", ".NETStandard,Version=v1.4")]
        [InlineData("netstandard1.5", ".NETStandard,Version=v1.5")]
        [InlineData("netstandardapp", ".NETStandardApp,Version=v0.0")]
        [InlineData("netstandardapp0.0", ".NETStandardApp,Version=v0.0")]
        [InlineData("netstandardapp1", ".NETStandardApp,Version=v1.0")]
        [InlineData("netstandardapp1.5", ".NETStandardApp,Version=v1.5")]
        [InlineData("netstandardapp2", ".NETStandardApp,Version=v2.0")]
        [InlineData("netstandardapp2.1", ".NETStandardApp,Version=v2.1")]
        [InlineData("netcoreapp", ".NETCoreApp,Version=v0.0")]
        [InlineData("netcoreapp0.0", ".NETCoreApp,Version=v0.0")]
        [InlineData("netcoreapp1", ".NETCoreApp,Version=v1.0")]
        [InlineData("netcoreapp1.5", ".NETCoreApp,Version=v1.5")]
        [InlineData("netcoreapp2", ".NETCoreApp,Version=v2.0")]
        [InlineData("netcoreapp3", ".NETCoreApp,Version=v3.0")]
        public void NuGetFramework_Basic(string folderName, string fullName)
        {
            string output = NuGetFramework.Parse(folderName).DotNetFrameworkName;

            Assert.Equal(fullName, output);
        }

        [Theory]
        [InlineData("foo")]
        [InlineData("foo45")]
        [InlineData("foo45-client")]
        [InlineData("foo.45")]
        [InlineData("foo4.5.1.2.3")]
        [InlineData("portable-net($3747!4")]
        [InlineData("")]
        public void NuGetFramework_Unsupported(string folderName)
        {
            Assert.Equal("Unsupported,Version=v0.0", NuGetFramework.Parse(folderName).DotNetFrameworkName);
        }

        [Fact]
        public void NuGetFramework_ParsePCLTest()
        {
            var fw = NuGetFramework.Parse("portable-net40%2Bsl5%2Bwp80%2Bwin8%2Bwpa81");

            Assert.Equal("portable-net40+sl5+win8+wp8+wpa81", fw.GetShortFolderName());
        }

        [Theory]
        [InlineData("portable-net45+wp8+win+wpa")]
        [InlineData("portable-net45+win8+wp8+wpa81")]
        [InlineData("portable-net45+wp8+win8+wpa")]
        [InlineData("portable-net45+win8+wp8+wpa81+monotouch+monoandroid")]
        [InlineData(".NETPortable,Version=v0.0,Profile=Profile259")]
        [InlineData("portable-net45+wp8+win+wpa+win8")]
        [InlineData("portable-net45+wp8+win+wpa+netcore+netcore45")]
        [InlineData("portable-net450+net4.5+net45+wp8+wpa+win8+wpa81")]
        [InlineData("portable-win8+net45+wp8+wpa81+win8+win8")]
        [InlineData("portable-net45+wp8+win+wpa+win8+net4.5")]
        public void NuGetFramework_ParsePCLNormalizeTest(string framework)
        {
            Assert.Equal("Profile259", NuGetFramework.Parse(framework).Profile);
        }

        [Theory]
        [InlineData(".NETPortable,Version=v0.0,Profile=win+net-cf", "win+net-cf")]
        [InlineData("portable-win+net-cf", "win+net-cf")]
        [InlineData(".NETPortable,Version=v0.0,Profile=net+win+wpa+wp+sl+net-cf+netmf+MonoAndroid+MonoTouch+Xamarin.iOS", "net+win+wpa+wp+sl+net-cf+netmf+MonoAndroid+MonoTouch+Xamarin.iOS")]
        [InlineData("portable-net+win+wpa+wp+sl+net-cf+netmf+MonoAndroid+MonoTouch+Xamarin.iOS", "net+win+wpa+wp+sl+net-cf+netmf+MonoAndroid+MonoTouch+Xamarin.iOS")]
        public void NuGetFramework_PortableWithInnerPortableProfileFails(string framework, string portableFrameworks)
        {
            var ex = Assert.Throws<ArgumentException>(
                () => NuGetFramework.Parse(framework));
            Assert.Equal(
                $"Invalid portable frameworks '{portableFrameworks}'. A hyphen may not be in any of the portable framework names.",
                ex.Message);
        }

        [Theory]
        [InlineData("dotnet", "dotnet")]
        [InlineData("dotnet", "dotnet50")]
        [InlineData("dotnet", "dotnet5.0")]
        [InlineData("dotnet50", "dotnet")]
        [InlineData("dotnet50", "dotnet50")]
        [InlineData("dotnet50", "dotnet5.0")]
        [InlineData("dotnet5.0", "dotnet")]
        [InlineData("dotnet5.0", "dotnet50")]
        [InlineData("dotnet5.0", "dotnet5.0")]
        [InlineData("dotnet5.1", "dotnet5.1")]
        [InlineData("dotnet5.2", "dotnet5.2")]
        [InlineData("dotnet5.3", "dotnet5.3")]
        [InlineData("dotnet5.4", "dotnet5.4")]
        [InlineData("dotnet5.5", "dotnet5.5")]
        [InlineData("dotnet5.6", "dotnet5.6")]
        [InlineData("dnx451", "dnx451")]
        [InlineData("dnxcore50", "dnxcore50")]
        [InlineData("dnxcore50", "dnxcore")]
        [InlineData("dnxcore", "dnxcore50")]
        [InlineData("dnxcore", "dnxcore")]
        [InlineData("net40", "net4")]
        [InlineData("net40", "net40")]
        [InlineData("net4", "net40")]
        [InlineData("net4", "net4")]
        [InlineData("net45", "net45")]
        [InlineData("net451", "net451")]
        [InlineData("net461", "net461")]
        [InlineData("net462", "net462")]
        [InlineData("win8", "win8")]
        [InlineData("win81", "win81")]
        [InlineData("netstandard", "netstandard")]
        [InlineData("netstandard1.0", "netstandard1.0")]
        [InlineData("netstandard1.0", "netstandard10")]
        [InlineData("netstandard10", "netstandard1.0")]
        [InlineData("netstandard10", "netstandard10")]
        [InlineData("netstandard1.1", "netstandard1.1")]
        [InlineData("netstandard1.1", "netstandard11")]
        [InlineData("netstandard11", "netstandard1.1")]
        [InlineData("netstandard11", "netstandard11")]
        [InlineData("netstandard1.2", "netstandard1.2")]
        [InlineData("netstandard1.2", "netstandard12")]
        [InlineData("netstandard12", "netstandard1.2")]
        [InlineData("netstandard12", "netstandard12")]
        [InlineData("netstandard1.3", "netstandard1.3")]
        [InlineData("netstandard1.3", "netstandard13")]
        [InlineData("netstandard13", "netstandard1.3")]
        [InlineData("netstandard13", "netstandard13")]
        [InlineData("netstandard1.4", "netstandard1.4")]
        [InlineData("netstandard1.4", "netstandard14")]
        [InlineData("netstandard14", "netstandard1.4")]
        [InlineData("netstandard14", "netstandard14")]
        [InlineData("netstandard1.5", "netstandard1.5")]
        [InlineData("netstandard1.5", "netstandard15")]
        [InlineData("netstandard15", "netstandard1.5")]
        [InlineData("netstandard15", "netstandard15")]
        [InlineData("netstandard1.6", "netstandard1.6")]
        [InlineData("netstandard1.6", "netstandard16")]
        [InlineData("netstandard16", "netstandard1.6")]
        [InlineData("netstandard16", "netstandard16")]
        [InlineData("netstandard1.7", "netstandard1.7")]
        [InlineData("netstandard1.7", "netstandard17")]
        [InlineData("netstandard17", "netstandard1.7")]
        [InlineData("netstandard17", "netstandard17")]
        [InlineData("netstandard2.0", "netstandard2.0")]
        [InlineData("netstandard2.0", "netstandard20")]
        [InlineData("netstandard20", "netstandard2.0")]
        [InlineData("netstandard20", "netstandard20")]
        [InlineData("netstandard2.1", "netstandard2.1")]
        [InlineData("netstandard2.1", "netstandard21")]
        [InlineData("netstandard21", "netstandard2.1")]
        [InlineData("netstandard21", "netstandard21")]
        [InlineData("netstandardapp1.5", "netstandardapp1.5")]
        [InlineData("netstandardapp1.5", "netstandardapp15")]
        [InlineData("netstandardapp15", "netstandardapp1.5")]
        [InlineData("netstandardapp15", "netstandardapp15")]
        [InlineData("netcoreapp1.0", "netcoreapp1.0")]
        [InlineData("netcoreapp1.0", "netcoreapp10")]
        [InlineData("netcoreapp10", "netcoreapp1.0")]
        [InlineData("netcoreapp10", "netcoreapp10")]
        [InlineData("netcoreapp2.0", "netcoreapp2.0")]
        [InlineData("netcoreapp2.0", "netcoreapp20")]
        [InlineData("netcoreapp20", "netcoreapp2.0")]
        [InlineData("netcoreapp20", "netcoreapp20")]
        [InlineData("netcoreapp3.0", "netcoreapp3.0")]
        [InlineData("netcoreapp3.0", "netcoreapp30")]
        [InlineData("netcoreapp30", "netcoreapp3.0")]
        [InlineData("netcoreapp30", "netcoreapp30")]
        public void NuGetFramework_TryParseCommonFramework_ParsesCommonFrameworks(string frameworkString1, string frameworkString2)
        {
            var framework1 = NuGetFramework.Parse(frameworkString1);
            var framework2 = NuGetFramework.Parse(frameworkString2);

            // Compare the object references
            Assert.Same(framework1, framework2);
        }
    }
}
