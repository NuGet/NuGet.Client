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
            Assert.Equal(7, actual.Length);
        }
    }
}
