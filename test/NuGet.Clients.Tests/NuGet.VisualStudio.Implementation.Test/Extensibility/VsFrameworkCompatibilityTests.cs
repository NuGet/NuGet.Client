using System;
using System.Linq;
using System.Runtime.Versioning;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test.Extensibility
{
    public class VsFrameworkCompatibilityTests
    {
        [Fact]
        public void VsFrameworkCompatibility_GetNearestRejectsNullTargetFramework()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();
            FrameworkName targetFramework = null;
            var frameworks = new[]
            {
                new FrameworkName(".NETFramework,Version=v4.5.1"),
                new FrameworkName(".NETFramework,Version=v4.5.2"),
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => target.GetNearest(targetFramework, frameworks));
        }

        [Fact]
        public void VsFrameworkCompatibility_GetNearestRejectsNullFrameworks()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();
            var targetFramework = new FrameworkName(".NETFramework,Version=v4.5");
            FrameworkName[] frameworks = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => target.GetNearest(targetFramework, frameworks));
        }

        [Fact]
        public void VsFrameworkCompatibility_GetNearestRejectsNullFallbackFrameworks()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();
            var targetFramework = new FrameworkName(".NETFramework,Version=v4.5");
            FrameworkName[] fallbackTargetFrameworks = null;
            var frameworks = new[]
            {
                new FrameworkName(".NETFramework,Version=v4.5.1"),
                new FrameworkName(".NETFramework,Version=v4.5.2"),
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => target.GetNearest(targetFramework, fallbackTargetFrameworks, frameworks));
        }

        [Fact]
        public void VsFrameworkCompatibility_GetNearestWithNoneCompatible()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();
            var targetFramework = new FrameworkName(".NETFramework,Version=v4.5");
            var frameworks = new[]
            {
                new FrameworkName(".NETFramework,Version=v4.5.1"),
                new FrameworkName(".NETFramework,Version=v4.5.2"),
            };

            // Act
            var actual = target.GetNearest(targetFramework, frameworks);

            // Assert
            Assert.Null(actual);
        }

        [Fact]
        public void VsFrameworkCompatibility_GetNearestWithMultipleCompatible()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();
            var targetFramework = new FrameworkName(".NETFramework,Version=v4.5.1");
            var frameworks = new[]
            {
                new FrameworkName(".NETFramework,Version=v3.5"),
                new FrameworkName(".NETFramework,Version=v4.0"),
                new FrameworkName(".NETFramework,Version=v4.5"),
                new FrameworkName(".NETFramework,Version=v4.5.2"),
            };

            // Act
            var actual = target.GetNearest(targetFramework, frameworks);

            // Assert
            Assert.Equal(".NETFramework,Version=v4.5", actual.ToString());
        }

        [Fact]
        public void VsFrameworkCompatibility_GetNearestWithCompatibleFallback()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();
            var targetFramework = new FrameworkName(".NETFramework,Version=v4.5");
            var fallbackTargetFrameworks = new[]
            {
                new FrameworkName(".NETFramework,Version=v4.5.2")
            };
            var frameworks = new[]
            {
                new FrameworkName(".NETFramework,Version=v4.5.1"),
                new FrameworkName(".NETFramework,Version=v4.6.1"),
            };

            // Act
            var actual = target.GetNearest(targetFramework, fallbackTargetFrameworks, frameworks);

            // Assert
            Assert.Equal(".NETFramework,Version=v4.5.1", actual.ToString());
        }

        [Fact]
        public void VsFrameworkCompatibility_GetNearestWithIncompatibleFallback()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();
            var targetFramework = new FrameworkName(".NETFramework,Version=v4.5");
            var fallbackTargetFrameworks = new[]
            {
                new FrameworkName(".NETFramework,Version=v4.5.2")
            };
            var frameworks = new[]
            {
                new FrameworkName(".NETFramework,Version=v4.6"),
                new FrameworkName(".NETFramework,Version=v4.6.1"),
            };

            // Act
            var actual = target.GetNearest(targetFramework, fallbackTargetFrameworks, frameworks);

            // Assert
            Assert.Null(actual);
        }

        [Fact]
        public void VsFrameworkCompatibility_GetNearestWithWithEmptyFallbackList()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();
            var targetFramework = new FrameworkName(".NETFramework,Version=v4.5.1");
            var fallbackTargetFrameworks = new FrameworkName[0];
            var frameworks = new[]
            {
                new FrameworkName(".NETFramework,Version=v3.5"),
                new FrameworkName(".NETFramework,Version=v4.0"),
                new FrameworkName(".NETFramework,Version=v4.5"),
                new FrameworkName(".NETFramework,Version=v4.5.2"),
            };

            // Act
            var actual = target.GetNearest(targetFramework, frameworks);

            // Assert
            Assert.Equal(".NETFramework,Version=v4.5", actual.ToString());
        }

        [Fact]
        public void VsFrameworkCompatibility_GetNetStandardVersions()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();

            // Act
            var actual = target.GetNetStandardFrameworks().ToArray();

            // Assert
            Assert.Equal(".NETStandard,Version=v1.0", actual[0].ToString());
            Assert.Equal(".NETStandard,Version=v1.1", actual[1].ToString());
            Assert.Equal(".NETStandard,Version=v1.2", actual[2].ToString());
            Assert.Equal(".NETStandard,Version=v1.3", actual[3].ToString());
            Assert.Equal(".NETStandard,Version=v1.4", actual[4].ToString());
            Assert.Equal(".NETStandard,Version=v1.5", actual[5].ToString());
            Assert.Equal(".NETStandard,Version=v1.6", actual[6].ToString());
            Assert.Equal(".NETStandard,Version=v1.7", actual[7].ToString());
            Assert.Equal(".NETStandard,Version=v2.0", actual[8].ToString());
            Assert.Equal(".NETStandard,Version=v2.1", actual[9].ToString());
            Assert.Equal(10, actual.Length);
        }

        [Fact]
        public void IVsFrameworkCompatibility3_GetNearest_WithCompatibleFramework_Succeeds()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();
            var net5 = new VsNuGetFramework(".NETCoreApp,Version=v5.0", targetPlatformMoniker: null, targetPlatformMinVersion: null);
            var net472 = new VsNuGetFramework(".NETFramework,Version=v4.7.2", targetPlatformMoniker: null, targetPlatformMinVersion: null);
            var netcoreapp31 = new VsNuGetFramework(".NETCoreApp,Version=v3.1", targetPlatformMoniker: null, targetPlatformMinVersion: null);
            var frameworks = new IVsNuGetFramework[] { net472, netcoreapp31 };

            // Act
            var actual = target.GetNearest(net5, frameworks);

            // Assert
            Assert.Equal(netcoreapp31, actual);
        }

        [Fact]
        public void IVsFrameworkCompatibility3_GetNearest_WithFallbackTfm_Succeeds()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();

            var net5 = new VsNuGetFramework(".NETCoreApp,Version=v5.0", targetPlatformMoniker: null, targetPlatformMinVersion: null);
            var net472 = new VsNuGetFramework(".NETFramework,Version=v4.7.2", targetPlatformMoniker: null, targetPlatformMinVersion: null);
            var fallbackFrameworks = new IVsNuGetFramework[] { net472 };
            var frameworks = new IVsNuGetFramework[] { net472 };

            // Act
            var actual = target.GetNearest(net5, fallbackFrameworks, frameworks);

            // Assert
            Assert.Equal(net472, actual);
        }

        [Fact]
        public void IVsFrameworkCompatibility3_GetNearest_NoCompatibleFramework_ReturnsNull()
        {
            // Arrange
            var target = new VsFrameworkCompatibility();

            var net5 = new VsNuGetFramework(".NETCoreApp,Version=v5.0", targetPlatformMoniker: null, targetPlatformMinVersion: null);
            var net472 = new VsNuGetFramework(".NETFramework,Version=v4.7.2", targetPlatformMoniker: null, targetPlatformMinVersion: null);
            var frameworks = new IVsNuGetFramework[] { net472 };

            // Act
            var actual = target.GetNearest(net5, frameworks);

            // Assert
            Assert.Null(actual);
        }
    }
}
