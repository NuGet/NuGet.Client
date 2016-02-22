using System;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test.Extensibility
{
    public class VsFrameworkParserTests
    {
        [Fact]
        public void VsFrameworkParser_RejectsNullInput()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => target.ParseFrameworkName(null));
        }

        [Fact]
        public void VsFrameworkParser_RejectsInvalidVersion()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => target.ParseFrameworkName("foo,Version=a.b"));
        }

        [Fact]
        public void VsFrameworkParser_RejectsInvalidProfile()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => target.ParseFrameworkName(".NETPortable,Version=1.0,Profile=a-b"));
        }

        [Fact]
        public void VsFrameworkParser_ParsesShortFrameworkName()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act
            var frameworkName = target.ParseFrameworkName("net45");

            // Assert
            Assert.Equal(".NETFramework,Version=v4.5", frameworkName.ToString());
        }

        [Fact]
        public void VsFrameworkParser_ParsesLongFrameworkName()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act
            var frameworkName = target.ParseFrameworkName(".NETFramework,Version=v4.5");

            // Assert
            Assert.Equal(".NETFramework,Version=v4.5", frameworkName.ToString());
        }
    }
}
