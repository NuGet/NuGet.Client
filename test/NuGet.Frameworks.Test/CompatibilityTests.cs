using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Test
{
    public class CompatibilityTests
    {
        [Theory]
        [InlineData("dnx46", "dnx451")]
        [InlineData("dnx452", "dnx451")]
        [InlineData("dnx452", "dnx")]
        [InlineData("dnxcore", "core50")]
        [InlineData("dnxcore", "core")]
        [InlineData("net46", "core50")]
        [InlineData("dnx46", "core50")]
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

        [Theory]
        [InlineData("net45", "dnx451")]
        [InlineData("net45", "net46")]
        [InlineData("core50", "net4")]
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

            // asp does not support dnx
            Assert.False(compat.IsCompatible(framework2, framework1));
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
        [InlineData("net45")]
        [InlineData("netcore45")]
        [InlineData("win8")]
        [InlineData("native")]
        [InlineData("dnx451")]
        public void Compatibility_CoreCompatNeg(string framework)
        {
            var framework1 = NuGetFramework.Parse(framework);
            var framework2 = NuGetFramework.Parse("core50");

            var compat = DefaultCompatibilityProvider.Instance;

            Assert.False(compat.IsCompatible(framework1, framework2));
        }

        [Theory]
        [InlineData("net46")]
        [InlineData("dnx46")]
        [InlineData("dnxcore50")]
        [InlineData("dnxcore")]
        public void Compatibility_CoreCompat(string framework)
        {
            var framework1 = NuGetFramework.Parse(framework);
            var framework2 = NuGetFramework.Parse("core50");

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a one way mapping
            Assert.True(!compat.IsCompatible(framework2, framework1));
        }

        [Fact]
        public void Compatibility_InferredCore()
        {
            // dnxcore50 -> coreclr -> native
            var framework1 = NuGetFramework.Parse("dnxcore50");
            var framework2 = NuGetFramework.Parse("native");

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
        [InlineData("win", "nfcore")]
        [InlineData("win81", "nfcore")]
        [InlineData("win8", "nfcore")]
        [InlineData("win", "nfcore45")]
        [InlineData("win", "nfcore4")]
        [InlineData("win81", "nfcore4")]
        public void Compatibility_Inferred(string fw1, string fw2)
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
        [InlineData("win8", "win")]
        [InlineData("wpa", "wpa81")]
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
        [InlineData("net45", "native")]
        [InlineData("net", "native")]
        [InlineData("dnx46", "native")]
        [InlineData("dnx452", "native")]
        [InlineData("dnx451", "net45")]
        [InlineData("dnx451", "nfcore45")]
        [InlineData("dnx451", "native")]
        [InlineData("dnxcore50", "native")]
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
