using System;
using System.Runtime.Versioning;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test.Extensibility
{
    public class VsFrameworkParserTests
    {
        [Fact]
        public void VsFrameworkParser_ParseFrameworkName_RejectsNullInput()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => target.ParseFrameworkName(null));
        }

        [Fact]
        public void VsFrameworkParser_ParseFrameworkName_RejectsInvalidVersion()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => target.ParseFrameworkName("foo,Version=a.b"));
        }

        [Fact]
        public void VsFrameworkParser_ParseFrameworkName_RejectsInvalidProfile()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => target.ParseFrameworkName(".NETPortable,Version=1.0,Profile=a-b"));
        }

        [Fact]
        public void VsFrameworkParser_ParseFrameworkName_ParsesShortFrameworkName()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act
            var frameworkName = target.ParseFrameworkName("net45");

            // Assert
            Assert.Equal(".NETFramework,Version=v4.5", frameworkName.ToString());
        }

        [Fact]
        public void VsFrameworkParser_ParseFrameworkName_ParsesLongFrameworkName()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act
            var frameworkName = target.ParseFrameworkName(".NETFramework,Version=v4.5");

            // Assert
            Assert.Equal(".NETFramework,Version=v4.5", frameworkName.ToString());
        }

        [Fact]
        public void VsFrameworkParser_GetShortFrameworkName_Success()
        {
            // Arrange
            var target = new VsFrameworkParser();
            var frameworkName = new FrameworkName(".NETStandard", Version.Parse("1.3"));

            // Act
            var actual = target.GetShortFrameworkName(frameworkName);

            // Assert
            Assert.Equal("netstandard1.3", actual);
        }

        [Fact]
        public void VsFrameworkParser_GetShortFrameworkName_RejectsNull()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => target.GetShortFrameworkName(null));
        }

        [Fact]
        public void VsFrameworkParser_GetShortFrameworkName_RejectsInvalidFrameworkIdentifier()
        {
            // Arrange
            var target = new VsFrameworkParser();
            var frameworkName = new FrameworkName("!?", Version.Parse("4.5"));

            // Act & Assert
            Assert.Throws<ArgumentException>(() => target.GetShortFrameworkName(frameworkName));
        }

        [Fact]
        public void VsFrameworkParser_GetShortFrameworkName_RejectsInvalidPortable()
        {
            // Arrange
            var target = new VsFrameworkParser();
            var frameworkName = new FrameworkName(".NETPortable", Version.Parse("4.5"), "net45+portable-net451");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => target.GetShortFrameworkName(frameworkName));
        }
    }
}
