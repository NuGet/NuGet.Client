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
        [InlineData("win")]
        [InlineData("win8")]
        public void WindowsIdentifierCompatibleWithWindowsStoreAppProjects(string identifier)
        {
            // Arrange
            var compat = DefaultCompatibilityProvider.Instance;
            var packageFramework = NuGetFramework.Parse(identifier);

            var projectFramework = NuGetFramework.Parse("netcore45");

            // Act && Assert
            Assert.True(compat.IsCompatible(projectFramework, packageFramework));
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

            var projectFramework = NuGetFramework.Parse("netcore45");
            var compat = DefaultCompatibilityProvider.Instance;

            // Act && Assert
            Assert.False(compat.IsCompatible(projectFramework, packageFramework));
        }

        [Fact]
        public void NetFrameworkCompatibiilityIsCompatibleReturns()
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
