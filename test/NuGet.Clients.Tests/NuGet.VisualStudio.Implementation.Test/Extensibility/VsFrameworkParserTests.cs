using System;
using System.Runtime.Versioning;
using NuGet.Frameworks;
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

        [Fact]
        public void TryParse_NullInput_ThrowsArgumentNullException()
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act
            var exception = Assert.Throws<ArgumentNullException>(() => target.TryParse(input: null, out _));

            // Assert
            Assert.Equal("input", exception.ParamName);
        }

        [Theory]
        [InlineData("net472")]
        [InlineData(".NETFramework,Version=4.7.2")]
        [InlineData("netstandard2.0")]
        [InlineData(".NETStandard,Version=2.0")]
        [InlineData("netcoreapp3.1")]
        [InlineData(".NETCoreApp,Version=3.1")]
        [InlineData("net5.0")]
        [InlineData(".NETCoreApp,Version=5.0")]
        [InlineData("net5.0-android10.0")]
        [InlineData("portable-net45+win8")]
        [InlineData(".NETPortable,Version=v0.0,Profile=Profile7")]
        public void TryParse_ValidInput_Succeeds(string input)
        {
            // Arrange
            var target = new VsFrameworkParser();
            IVsNuGetFramework actual;
            var expected = NuGetFramework.Parse(input);

            // Act
            var result = target.TryParse(input, out actual);

            // Assert
            Assert.True(result, "Return value was not true");
            Assert.Equal(expected.Framework, actual.TargetFrameworkIdentifier);
            Assert.Equal("v" + expected.Version.ToString(), actual.TargetFrameworkVersion);
            Assert.Equal(expected.Profile, actual.TargetFrameworkProfile);
            Assert.Equal(expected.Platform, actual.TargetPlatformIdentifier);
            Assert.Equal(expected.PlatformVersion.ToString(), actual.TargetPlatformVersion);
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("any")]
        [InlineData("unsupported")]
        public void TryParse_InvalidInput_ReturnsFalse(string input)
        {
            // Arrange
            var target = new VsFrameworkParser();

            // Act
            var actual = target.TryParse(input, out IVsNuGetFramework parsed);

            // Assert
            Assert.False(actual, $"Expected false, but got true for {input}. TFI {parsed.TargetFrameworkIdentifier}, TFV {parsed.TargetFrameworkVersion}, Profile {parsed.TargetFrameworkProfile}, TPI {parsed.TargetPlatformIdentifier}, TPV {parsed.TargetPlatformVersion}");
        }
    }
}
