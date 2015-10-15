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
        [InlineData("net40", ".NETFramework,Version=v4.0")]
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
        [InlineData("portable-net45+wp8+win+wpa+win8")]
        [InlineData("portable-net45+wp8+win+wpa+win8+net4.5")]
        public void NuGetFramework_ParsePCLNormalizeTest(string framework)
        {
            Assert.Equal("Profile259", NuGetFramework.Parse(framework).Profile);
        }
    }
}
