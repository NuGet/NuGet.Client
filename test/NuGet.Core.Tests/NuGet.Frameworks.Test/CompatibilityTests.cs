// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.Test
{
    public class CompatibilityTests
    {
        [Theory]
        // dotnet
        [InlineData("dotnet", "dotnet", true)]
        [InlineData("dotnet5.1", "dotnet", true)]

        // dnxcore50 -> dotnet
        [InlineData("dnxcore50", "dotnet5.5", true)]
        [InlineData("dnxcore50", "dotnet5.4", true)]
        [InlineData("dnxcore50", "dotnet5.3", true)]
        [InlineData("dnxcore50", "dotnet5.2", true)]
        [InlineData("dnxcore50", "dotnet5.1", true)]

        // net -> dotnet
        [InlineData("net461", "dotnet5.5", true)]
        [InlineData("net461", "dotnet5.4", true)]
        [InlineData("net461", "dotnet5.3", true)]
        [InlineData("net461", "dotnet5.2", true)]
        [InlineData("net461", "dotnet5.1", true)]
        [InlineData("net461", "dotnet", true)]

        [InlineData("net46", "dotnet5.5", false)]
        [InlineData("net46", "dotnet5.4", true)]
        [InlineData("net46", "dotnet5.3", true)]
        [InlineData("net46", "dotnet5.2", true)]
        [InlineData("net46", "dotnet5.1", true)]
        [InlineData("net46", "dotnet", true)]

        [InlineData("net452", "dotnet5.5", false)]
        [InlineData("net452", "dotnet5.4", false)]
        [InlineData("net452", "dotnet5.3", true)]
        [InlineData("net452", "dotnet5.2", true)]
        [InlineData("net452", "dotnet5.1", true)]
        [InlineData("net452", "dotnet", true)]

        [InlineData("net451", "dotnet5.5", false)]
        [InlineData("net451", "dotnet5.4", false)]
        [InlineData("net451", "dotnet5.3", true)]
        [InlineData("net451", "dotnet5.2", true)]
        [InlineData("net451", "dotnet5.1", true)]
        [InlineData("net451", "dotnet", true)]

        [InlineData("net45", "dotnet5.5", false)]
        [InlineData("net45", "dotnet5.4", false)]
        [InlineData("net45", "dotnet5.3", false)]
        [InlineData("net45", "dotnet5.2", true)]
        [InlineData("net45", "dotnet5.1", true)]
        [InlineData("net45", "dotnet", true)]

        // dnx -> dotnet
        [InlineData("dnx461", "dotnet5.5", true)]
        [InlineData("dnx461", "dotnet5.4", true)]
        [InlineData("dnx461", "dotnet5.3", true)]
        [InlineData("dnx461", "dotnet5.2", true)]
        [InlineData("dnx461", "dotnet5.1", true)]
        [InlineData("dnx461", "dotnet", true)]

        [InlineData("dnx46", "dotnet5.5", false)]
        [InlineData("dnx46", "dotnet5.4", true)]
        [InlineData("dnx46", "dotnet5.3", true)]
        [InlineData("dnx46", "dotnet5.2", true)]
        [InlineData("dnx46", "dotnet5.1", true)]
        [InlineData("dnx46", "dotnet", true)]

        [InlineData("dnx452", "dotnet5.5", false)]
        [InlineData("dnx452", "dotnet5.4", false)]
        [InlineData("dnx452", "dotnet5.3", true)]
        [InlineData("dnx452", "dotnet5.2", true)]
        [InlineData("dnx452", "dotnet5.1", true)]
        [InlineData("dnx452", "dotnet", true)]

        [InlineData("dnx451", "dotnet5.5", false)]
        [InlineData("dnx451", "dotnet5.4", false)]
        [InlineData("dnx451", "dotnet5.3", true)]
        [InlineData("dnx451", "dotnet5.2", true)]
        [InlineData("dnx451", "dotnet5.1", true)]
        [InlineData("dnx451", "dotnet", true)]

        // dnx45 doesn't really work, but it's here for completeness :)
        [InlineData("dnx45", "dotnet5.5", false)]
        [InlineData("dnx45", "dotnet5.4", false)]
        [InlineData("dnx45", "dotnet5.3", false)]
        [InlineData("dnx45", "dotnet5.2", true)]
        [InlineData("dnx45", "dotnet5.1", true)]
        [InlineData("dnx45", "dotnet", true)]

        // uap10 -> netcore50 -> win81 -> wpa81 -> dotnet
        [InlineData("uap10.0", "netcore50", true)]
        [InlineData("uap10.0", "win81", true)]
        [InlineData("uap10.0", "wpa81", true)]
        [InlineData("uap10.0", "dotnet5.5", false)]
        [InlineData("uap10.0", "dotnet5.4", true)]
        [InlineData("uap10.0", "dotnet5.3", true)]
        [InlineData("uap10.0", "dotnet5.2", true)]
        [InlineData("uap10.0", "dotnet5.1", true)]
        [InlineData("netcore50", "win81", true)]
        [InlineData("netcore50", "wpa81", false)]
        [InlineData("netcore50", "dotnet5.5", false)]
        [InlineData("netcore50", "dotnet5.4", true)]
        [InlineData("netcore50", "dotnet5.3", true)]
        [InlineData("netcore50", "dotnet5.2", true)]
        [InlineData("netcore50", "dotnet5.1", true)]

        // wpa81/win81 -> dotnet
        [InlineData("wpa81", "dotnet5.5", false)]
        [InlineData("wpa81", "dotnet5.4", false)]
        [InlineData("wpa81", "dotnet5.3", true)]
        [InlineData("wpa81", "dotnet5.2", true)]
        [InlineData("wpa81", "dotnet5.1", true)]
        [InlineData("win81", "dotnet5.5", false)]
        [InlineData("win81", "dotnet5.4", false)]
        [InlineData("win81", "dotnet5.3", true)]
        [InlineData("win81", "dotnet5.2", true)]
        [InlineData("win81", "dotnet5.1", true)]

        // wp8/wp81 -> dotnet
        [InlineData("wp81", "dotnet5.5", false)]
        [InlineData("wp81", "dotnet5.4", false)]
        [InlineData("wp81", "dotnet5.3", false)]
        [InlineData("wp81", "dotnet5.2", false)]
        [InlineData("wp81", "dotnet5.1", true)]
        [InlineData("wp8", "dotnet5.5", false)]
        [InlineData("wp8", "dotnet5.4", false)]
        [InlineData("wp8", "dotnet5.3", false)]
        [InlineData("wp8", "dotnet5.2", false)]
        [InlineData("wp8", "dotnet5.1", true)]
        [InlineData("sl8-windowsphone", "dotnet5.5", false)]
        [InlineData("sl8-windowsphone", "dotnet5.4", false)]
        [InlineData("sl8-windowsphone", "dotnet5.3", false)]
        [InlineData("sl8-windowsphone", "dotnet5.2", false)]
        [InlineData("sl8-windowsphone", "dotnet5.1", true)]
        [InlineData("sl7-windowsphone", "dotnet5.5", false)]
        [InlineData("sl7-windowsphone", "dotnet5.4", false)]
        [InlineData("sl7-windowsphone", "dotnet5.3", false)]
        [InlineData("sl7-windowsphone", "dotnet5.2", false)]
        [InlineData("sl7-windowsphone", "dotnet5.1", false)]

        // win8 -> dotnet
        [InlineData("win8", "dotnet5.4", false)]
        [InlineData("win8", "dotnet5.3", false)]
        [InlineData("win8", "dotnet5.2", true)]
        [InlineData("win8", "dotnet5.1", true)]

        // Older things don't support dotnet at all
        [InlineData("sl4", "dotnet", false)]
        [InlineData("sl3", "dotnet", false)]
        [InlineData("sl2", "dotnet", false)]
        [InlineData("net40", "dotnet", false)]
        [InlineData("net35", "dotnet", false)]
        [InlineData("net20", "dotnet", false)]
        [InlineData("net20", "dotnet", false)]

        // dotnet doesn't support the things that support it
        [InlineData("dotnet5.1", "net45", false)]
        [InlineData("dotnet5.2", "net45", false)]
        [InlineData("dotnet5.2", "net451", false)]
        [InlineData("dotnet5.2", "net452", false)]
        [InlineData("dotnet5.1", "net46", false)]
        [InlineData("dotnet5.2", "net46", false)]
        [InlineData("dotnet5.3", "net46", false)]
        [InlineData("dotnet5.1", "net461", false)]
        [InlineData("dotnet5.2", "net461", false)]
        [InlineData("dotnet5.3", "net461", false)]
        [InlineData("dotnet5.4", "net461", false)]
        [InlineData("dotnet5.1", "dnxcore50", false)]
        [InlineData("dotnet5.2", "dnxcore50", false)]
        [InlineData("dotnet5.3", "dnxcore50", false)]
        [InlineData("dotnet5.4", "dnxcore50", false)]

        // Old-world Portable doesn't support dotnet and vice-versa
        [InlineData("dotnet", "portable-net40+sl5+win8", false)]
        [InlineData("portable-net40+sl5+win8", "dotnet", false)]
        [InlineData("portable-net45+win8", "dotnet", false)]
        [InlineData("portable-net451+win81", "dotnet", false)]
        [InlineData("portable-net451+win8+core50", "dotnet", false)]
        [InlineData("portable-net451+win8+dnxcore50", "dotnet", false)]
        [InlineData("portable-net451+win8+aspnetcore50", "dotnet", false)]
        public void Compatibility_FrameworksAreCompatible(string project, string package, bool compatible)
        {
            // Arrange
            var framework1 = NuGetFramework.Parse(project);
            var framework2 = NuGetFramework.Parse(package);

            var compat = DefaultCompatibilityProvider.Instance;

            // Act & Assert
            Assert.Equal(compatible, compat.IsCompatible(framework1, framework2));
        }

        [Fact]
        public void Compatibility_UAPWinNonTPM()
        {
            NuGetFramework framework = NuGetFramework.Parse("UAP10.0");
            NuGetFramework windows = NuGetFramework.Parse("win");

            var compat = DefaultCompatibilityProvider.Instance;

            Assert.True(compat.IsCompatible(framework, windows));
        }

        [Theory]
        [InlineData("dnxcore50", "UAP10.0")]
        [InlineData("dotnet50", "UAP10.0")]
        [InlineData("dotnet", "UAP10.0")]
        [InlineData("dotnet", "UAP")]
        [InlineData("native", "UAP")]
        [InlineData("net46", "UAP")]
        public void Compatibility_PlatformOneWayNeg(string fw1, string fw2)
        {
            var framework1 = NuGetFramework.Parse(fw1);
            var framework2 = NuGetFramework.Parse(fw2);

            var compat = DefaultCompatibilityProvider.Instance;

            Assert.False(compat.IsCompatible(framework1, framework2));
        }

        [Theory]
        [InlineData("UAP10.0", "netcore50")]
        [InlineData("UAP10.0", "netcore45")]
        [InlineData("UAP10.0", "winrt45")]
        [InlineData("UAP10.0", "dotnet")]
        [InlineData("UAP10.0", "dotnet50")]
        [InlineData("UAP10.0", "Win81")]
        [InlineData("UAP10.0", "Win8")]
        [InlineData("UAP10.0", "Win")]
        [InlineData("UAP10.0", "WPA81")]
        [InlineData("UAP10.0", "WPA")]
        [InlineData("UAP", "Win81")]
        [InlineData("UAP", "Win8")]
        [InlineData("UAP", "Win")]
        [InlineData("UAP", "WPA81")]
        [InlineData("UAP", "WPA")]
        [InlineData("UAP11.0", "Win81")]
        [InlineData("UAP11.0", "Win8")]
        [InlineData("UAP11.0", "Win")]
        [InlineData("UAP11.0", "WPA81")]
        [InlineData("UAP11.0", "WPA")]
        public void Compatibility_PlatformOneWay(string fw1, string fw2)
        {
            var framework1 = NuGetFramework.Parse(fw1);
            var framework2 = NuGetFramework.Parse(fw2);

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a one way mapping
            Assert.True(!compat.IsCompatible(framework2, framework1));
        }

        [Theory]
        [InlineData("dnx46", "dnx451")]
        [InlineData("dnx452", "dnx451")]
        [InlineData("dnx452", "dnx")]
        [InlineData("dnxcore", "dotnet50")]
        [InlineData("dnxcore", "dotnet")]
        [InlineData("net46", "dotnet")]
        [InlineData("dnx46", "dotnet")]
        [InlineData("aspnet50", "net40")]
        [InlineData("netcore50", "netcore45")]
        [InlineData("netcore50", "dotnet")]
        [InlineData("uap10.0", "portable-net45+win8")]
        [InlineData("uap10.0", "portable-net45+win8+wpa81")]
        [InlineData("uap10.0", "portable-net45+wpa81")]
        [InlineData("uap10.0", "portable-net45+sl5+dotnet")]
        [InlineData("uap10.0", "portable-net45+sl5+netcore50")]
        [InlineData("uap10.0", "portable-net45+sl5+uap")]
        [InlineData("netcore50", "netcore451")]
        [InlineData("netcore50", "win81")]
        [InlineData("netcore50", "win8")]
        [InlineData("netcore50", "win")]
        [InlineData("win81", "netcore")]
        [InlineData("netcore451", "win8")]
        public void Compatibility_SimpleOneWay(string fw1, string fw2)
        {
            var framework1 = NuGetFramework.Parse(fw1);
            var framework2 = NuGetFramework.Parse(fw2);

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a one way mapping
            Assert.True(!compat.IsCompatible(framework2, framework1));
        }

        [InlineData("netcore451", "win81")]
        [InlineData("netcore45", "win8")]
        [InlineData("netcore", "win")]
        [InlineData("win8", "win")]
        [InlineData("win8", "netcore")]
        [InlineData("win8", "netcore45")]
        [InlineData("wpa", "wpa81")]
        [InlineData("uap", "uap10.0")]
        public void Compatibility_SimpleTwoWay(string fw1, string fw2)
        {
            var framework1 = NuGetFramework.Parse(fw1);
            var framework2 = NuGetFramework.Parse(fw2);

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a two way mapping
            Assert.True(compat.IsCompatible(framework2, framework1));
        }

        [Theory]
        [InlineData("net45", "dnx451")]
        [InlineData("net45", "net46")]
        [InlineData("dotnet", "net4")]
        [InlineData("win81", "netcore50")]
        [InlineData("wpa81", "netcore50")]
        [InlineData("uap10.0", "portable-net45+sl5+wp8")]
        [InlineData("win8", "netcore451")]
        [InlineData("net40", "dotnet")]
        public void Compatibility_SimpleNonCompat(string fw1, string fw2)
        {
            var framework1 = NuGetFramework.Parse(fw1);
            var framework2 = NuGetFramework.Parse(fw2);

            var compat = DefaultCompatibilityProvider.Instance;

            Assert.False(compat.IsCompatible(framework1, framework2));
        }

        [Fact]
        public void Compatibility_DnxNoCompat()
        {
            var framework1 = NuGetFramework.Parse("dnx451");
            var framework2 = NuGetFramework.Parse("dnxcore50");

            var compat = DefaultCompatibilityProvider.Instance;

            Assert.False(compat.IsCompatible(framework1, framework2));

            Assert.False(compat.IsCompatible(framework2, framework1));
        }

        [Fact]
        public void Compatibility_DnxAsp()
        {
            var framework1 = NuGetFramework.Parse("dnxcore50");
            var framework2 = NuGetFramework.Parse("aspnetcore50");

            var compat = DefaultCompatibilityProvider.Instance;

            // dnx supports asp
            Assert.True(compat.IsCompatible(framework1, framework2));

            Assert.True(compat.IsCompatible(framework2, framework1));
        }

        [Theory]
        [InlineData("net45", "net45-full")]
        [InlineData("net40-full", "net40-full")]
        public void Compatibility_ProfileAlias(string fw1, string fw2)
        {
            var framework1 = NuGetFramework.Parse(fw1);
            var framework2 = NuGetFramework.Parse(fw2);

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a two way mapping
            Assert.True(compat.IsCompatible(framework2, framework1));
        }

        [Theory]
        [InlineData("net45", "net45-client")]
        [InlineData("net45", "net40-client")]
        public void Compatibility_Profiles(string fw1, string fw2)
        {
            var framework1 = NuGetFramework.Parse(fw1);
            var framework2 = NuGetFramework.Parse(fw2);

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));
        }

        [Theory]
        [InlineData("net40-client")]
        [InlineData("net40")]
        [InlineData("native")]
        [InlineData("sl5")]
        [InlineData("wp7")]
        [InlineData("wp4")]
        [InlineData("sl")]
        [InlineData("netmf")]
        [InlineData("net35")]
        [InlineData("net403")]
        [InlineData("portable-net45+win8")]
        [InlineData("net45-cf")]
        [InlineData("sl5")]
        [InlineData("sl")]
        [InlineData("netmf")]
        [InlineData("wp7")]
        [InlineData("net40")]
        public void Compatibility_ProjectCannotInstallDotNetLibraries(string framework)
        {
            // Arrange
            var framework1 = NuGetFramework.Parse(framework);
            var framework2 = NuGetFramework.Parse("dotnet");
            var compat = DefaultCompatibilityProvider.Instance;

            // Act & Assert
            Assert.False(compat.IsCompatible(framework1, framework2));
        }

        [Theory]
        [InlineData("net45")]
        [InlineData("net45-client")]
        [InlineData("net451")]
        [InlineData("net50")]
        [InlineData("net46")]
        [InlineData("dnx46")]
        [InlineData("dnx50")]
        [InlineData("dnxcore50")]
        [InlineData("dnxcore")]
        [InlineData("netcore50")]
        [InlineData("netcore60")]
        [InlineData("uap")]
        [InlineData("uap11.0")]
        [InlineData("wpa")]
        [InlineData("wpa81")]
        [InlineData("netcore")]
        [InlineData("win")]
        [InlineData("win8")]
        [InlineData("win81")]
        [InlineData("monotouch")]
        [InlineData("monotouch10")]
        [InlineData("monoandroid")]
        [InlineData("monoandroid40")]
        [InlineData("monomac")]
        [InlineData("xamarinios")]
        [InlineData("xamarinmac")]
        [InlineData("xamarinpsthree")]
        [InlineData("xamarinpsfour")]
        [InlineData("xamarinpsvita")]
        [InlineData("xamarintvos")]
        [InlineData("xamarinxboxthreesixty")]
        [InlineData("xamarinwatchos")]
        [InlineData("xamarinxboxone")]
        public void Compatibility_ProjectCanInstallDotNetLibraries(string framework)
        {
            // Arrange
            var framework1 = NuGetFramework.Parse(framework);
            var framework2 = NuGetFramework.Parse("dotnet");

            var compat = DefaultCompatibilityProvider.Instance;

            // Act & Assert

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a one way mapping
            Assert.True(!compat.IsCompatible(framework2, framework1));
        }

        [Theory]
        [InlineData("dotnet")]
        [InlineData("dotnet50")]
        public void Compatibility_DotNetProjectCompat(string framework)
        {
            // Arrange
            var framework1 = NuGetFramework.Parse(framework);
            var project = NuGetFramework.Parse("dotnet");

            var compat = DefaultCompatibilityProvider.Instance;

            // Act & Assert
            Assert.True(compat.IsCompatible(project, framework1));
        }

        [Theory]
        [InlineData("native")]
        [InlineData("wpa81")]
        [InlineData("UAP10.0")]
        [InlineData("win8")]
        [InlineData("net50")]
        [InlineData("net46")]
        [InlineData("dnx46")]
        [InlineData("dnx50")]
        [InlineData("dnxcore50")]
        [InlineData("dnxcore")]
        [InlineData("netcore50")]
        [InlineData("netcore60")]
        [InlineData("sl6")]
        public void Compatibility_DotNetProjectCompatNeg(string framework)
        {
            // Arrange
            var framework1 = NuGetFramework.Parse(framework);
            var project = NuGetFramework.Parse("dotnet");

            var compat = DefaultCompatibilityProvider.Instance;

            // Act & Assert
            Assert.False(compat.IsCompatible(project, framework1));
        }

        [Fact]
        public void Compatibility_InferredDotNet()
        {
            // dnxcore50 -> coreclr -> native
            var framework1 = NuGetFramework.Parse("dnxcore50");
            var framework2 = NuGetFramework.Parse("dotnet");

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a one way mapping
            Assert.True(!compat.IsCompatible(framework2, framework1));
        }

        [Fact]
        public void Compatibility_InferredIndirect()
        {
            // win9 -> win8 -> netcore45, win8 -> netcore45
            var framework1 = NuGetFramework.Parse("win9");
            var framework2 = NuGetFramework.Parse("netcore45");

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a one way mapping
            Assert.True(!compat.IsCompatible(framework2, framework1));
        }

        [Theory]
        [InlineData("win8", "win")]
        [InlineData("wpa", "wpa81")]
        [InlineData("netcore45", "win8")]
        [InlineData("netcore451", "win81")]
        public void Compatibility_EqualMappings(string fw1, string fw2)
        {
            var framework1 = NuGetFramework.Parse(fw1);
            var framework2 = NuGetFramework.Parse(fw2);

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a two way mapping
            Assert.True(compat.IsCompatible(framework2, framework1));
        }

        [Theory]
        [InlineData("dnx451", "net4")]
        [InlineData("dnx451", "net451")]
        [InlineData("dnx451", "net45")]
        public void Compatibility_OneWayMappings(string fw1, string fw2)
        {
            var framework1 = NuGetFramework.Parse(fw1);
            var framework2 = NuGetFramework.Parse(fw2);

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a one way mapping
            Assert.True(!compat.IsCompatible(framework2, framework1));
        }

        [Fact]
        public void Compatibility_Basic()
        {
            var net45 = NuGetFramework.Parse("net45");
            var net40 = NuGetFramework.Parse("net40");

            var compat = DefaultCompatibilityProvider.Instance;

            Assert.True(compat.IsCompatible(net45, net40));
        }

        [Fact]
        public void IsCompatibleReturnsFalseForSlAndWindowsPhoneFrameworks()
        {
            // Arrange
            var sl3 = NuGetFramework.Parse("sl3");
            var wp7 = NuGetFramework.Parse("sl3-wp");
            var compat = DefaultCompatibilityProvider.Instance;

            // Act
            bool wp7CompatibleWithSl = compat.IsCompatible(sl3, wp7);
            bool slCompatibleWithWp7 = compat.IsCompatible(wp7, sl3);

            // Assert
            Assert.False(slCompatibleWithWp7);
            Assert.False(wp7CompatibleWithSl);
        }

        [Fact]
        public void IsCompatibleWindowsPhoneVersions()
        {
            // Arrange
            var wp7 = NuGetFramework.Parse("sl3-wp");
            var wp7Mango = NuGetFramework.Parse("sl4-wp71");
            var wp8 = NuGetFramework.Parse("wp8");
            var wp81 = NuGetFramework.Parse("wp81");
            var wpa81 = NuGetFramework.Parse("wpa81");
            var compat = DefaultCompatibilityProvider.Instance;

            // Act
            bool wp7MangoCompatibleWithwp7 = compat.IsCompatible(wp7, wp7Mango);
            bool wp7CompatibleWithwp7Mango = compat.IsCompatible(wp7Mango, wp7);

            bool wp7CompatibleWithwp8 = compat.IsCompatible(wp8, wp7);
            bool wp7MangoCompatibleWithwp8 = compat.IsCompatible(wp8, wp7Mango);

            bool wp8CompatibleWithwp7 = compat.IsCompatible(wp7, wp8);
            bool wp8CompatbielWithwp7Mango = compat.IsCompatible(wp7Mango, wp8);

            bool wp81CompatibleWithwp8 = compat.IsCompatible(wp81, wp8);

            bool wpa81CompatibleWithwp81 = compat.IsCompatible(wpa81, wp81);

            // Assert
            Assert.False(wp7MangoCompatibleWithwp7);
            Assert.True(wp7CompatibleWithwp7Mango);

            Assert.True(wp7CompatibleWithwp8);
            Assert.True(wp7MangoCompatibleWithwp8);

            Assert.False(wp8CompatibleWithwp7);
            Assert.False(wp8CompatbielWithwp7Mango);

            Assert.True(wp81CompatibleWithwp8);

            Assert.False(wpa81CompatibleWithwp81);
        }

        [Theory]
        [InlineData("wp")]
        [InlineData("wp7")]
        [InlineData("wp70")]
        [InlineData("wp")]
        [InlineData("wp7")]
        [InlineData("wp70")]
        [InlineData("sl3-wp")]
        public void WindowsPhone7IdentifierCompatibleWithAllWPProjects(string wp7Identifier)
        {
            var compat = DefaultCompatibilityProvider.Instance;

            // Arrange
            var wp7Package = NuGetFramework.Parse(wp7Identifier);

            var wp7Project = NuGetFramework.Parse("sl3-wp");
            var mangoProject = NuGetFramework.Parse("sl4-wp71");
            var apolloProject = NuGetFramework.Parse("wp8");

            // Act & Assert
            Assert.True(compat.IsCompatible(wp7Project, wp7Package));
            Assert.True(compat.IsCompatible(mangoProject, wp7Package));
            Assert.True(compat.IsCompatible(apolloProject, wp7Package));
        }

        [Theory]
        [InlineData("wp71")]
        [InlineData("sl4-wp71")]
        public void WindowsPhoneMangoIdentifierCompatibleWithAllWPProjects(string mangoIdentifier)
        {
            // Arrange
            var mangoPackage = NuGetFramework.Parse(mangoIdentifier);
            var compat = DefaultCompatibilityProvider.Instance;

            var wp7Project = NuGetFramework.Parse("sl3-wp");
            var mangoProject = NuGetFramework.Parse("sl4-wp71");
            var apolloProject = NuGetFramework.Parse("wp8");

            // Act & Assert
            Assert.False(compat.IsCompatible(wp7Project, mangoPackage));
            Assert.True(compat.IsCompatible(mangoProject, mangoPackage));
            Assert.True(compat.IsCompatible(apolloProject, mangoPackage));
        }

        [Theory]
        [InlineData("wp8")]
        [InlineData("wp80")]
        [InlineData("windowsphone8")]
        [InlineData("windowsphone80")]
        public void WindowsPhoneApolloIdentifierCompatibleWithAllWPProjects(string apolloIdentifier)
        {
            // Arrange
            var compat = DefaultCompatibilityProvider.Instance;
            var apolloPackage = NuGetFramework.Parse(apolloIdentifier);

            var wp7Project = NuGetFramework.Parse("sl3-wp");
            var mangoProject = NuGetFramework.Parse("sl4-wp71");
            var apolloProject = NuGetFramework.Parse("wp8");

            // Act & Assert
            Assert.False(compat.IsCompatible(wp7Project, apolloPackage));
            Assert.False(compat.IsCompatible(mangoProject, apolloPackage));
            Assert.True(compat.IsCompatible(apolloProject, apolloPackage));
        }

        [Theory]
        [InlineData("win9")]
        [InlineData("win9")]
        [InlineData("win10")]
        [InlineData("win81")]
        [InlineData("win45")]
        [InlineData("win1")]
        public void WindowsIdentifierWithUnsupportedVersionNotCompatibleWithWindowsStoreAppProjects(string identifier)
        {
            // Arrange
            var packageFramework = NuGetFramework.Parse(identifier);

            var projectFramework = NuGetFramework.Parse("nfcore45");
            var compat = DefaultCompatibilityProvider.Instance;

            // Act && Assert
            Assert.False(compat.IsCompatible(projectFramework, packageFramework));
        }

        [Fact]
        public void NetFrameworkCompatibilityIsCompatibleReturns()
        {
            // Arrange
            var net40 = NuGetFramework.Parse("net40");
            var net40Client = NuGetFramework.Parse("net40-client");
            var compat = DefaultCompatibilityProvider.Instance;

            // Act
            bool netClientCompatibleWithNet = compat.IsCompatible(net40, net40Client);
            bool netCompatibleWithClient = compat.IsCompatible(net40Client, net40);

            // Assert
            Assert.True(netClientCompatibleWithNet);
            Assert.True(netCompatibleWithClient);
        }

        [Fact]
        public void LowerFrameworkVersionsAreNotCompatibleWithHigherFrameworkVersionsWithSameFrameworkName()
        {
            // Arrange
            var net40 = NuGetFramework.Parse("net40");
            var net20 = NuGetFramework.Parse("net20");
            var compat = DefaultCompatibilityProvider.Instance;

            // Act
            bool net40CompatibleWithNet20 = compat.IsCompatible(net20, net40);
            bool net20CompatibleWithNet40 = compat.IsCompatible(net40, net20);

            // Assert
            Assert.False(net40CompatibleWithNet20);
            Assert.True(net20CompatibleWithNet40);
        }
    }
}
