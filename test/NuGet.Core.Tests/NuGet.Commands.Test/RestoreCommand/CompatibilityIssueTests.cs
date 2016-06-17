using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class CompatibilityIssueTests
    {
        [Fact]
        public void CompatibilityIssue_Equals_Same()
        {
            // Arrange
            var a = CompatibilityIssue.IncompatiblePackage(
                new PackageIdentity("foo", new NuGetVersion("1.0.0")),
                NuGetFramework.Parse("net451"),
                "win10-x64",
                new [] { NuGetFramework.Parse("net40"), NuGetFramework.Parse("net45") });
            var b = CompatibilityIssue.IncompatiblePackage(
                new PackageIdentity("foo", new NuGetVersion("1.0.0")),
                NuGetFramework.Parse("net451"),
                "win10-x64",
                new [] { NuGetFramework.Parse("net45"), NuGetFramework.Parse("net40") });
            
            // Act & Assert
            Assert.Equal(a, b);
        }
        
        [Fact]
        public void CompatibilityIssue_Equals_DifferentAvailableFrameworks()
        {
            // Arrange
            var a = CompatibilityIssue.IncompatiblePackage(
                new PackageIdentity("foo", new NuGetVersion("1.0.0")),
                NuGetFramework.Parse("net451"),
                "win10-x64",
                new [] { NuGetFramework.Parse("net40"), NuGetFramework.Parse("net45") });
            var b = CompatibilityIssue.IncompatiblePackage(
                new PackageIdentity("foo", new NuGetVersion("1.0.0")),
                NuGetFramework.Parse("net451"),
                "win10-x64",
                new [] { NuGetFramework.Parse("net40"), NuGetFramework.Parse("net46") });
            
            // Act & Assert
            Assert.NotEqual(a, b);
        }
        
        [Fact]
        public void CompatibilityIssue_Equals_DifferentPackageIdentity()
        {
            // Arrange
            var a = CompatibilityIssue.IncompatiblePackage(
                new PackageIdentity("foo", new NuGetVersion("1.0.0")),
                NuGetFramework.Parse("net451"),
                "win10-x64",
                new [] { NuGetFramework.Parse("net40"), NuGetFramework.Parse("net45") });
            var b = CompatibilityIssue.IncompatiblePackage(
                new PackageIdentity("foo", new NuGetVersion("2.0.0")),
                NuGetFramework.Parse("net451"),
                "win10-x64",
                new [] { NuGetFramework.Parse("net40"), NuGetFramework.Parse("net45") });
            
            // Act & Assert
            Assert.NotEqual(a, b);
        }
        
        [Fact]
        public void CompatibilityIssue_Equals_DifferentRuntime()
        {
            // Arrange
            var a = CompatibilityIssue.IncompatiblePackage(
                new PackageIdentity("foo", new NuGetVersion("1.0.0")),
                NuGetFramework.Parse("net451"),
                "win10-x64",
                new [] { NuGetFramework.Parse("net40"), NuGetFramework.Parse("net45") });
            var b = CompatibilityIssue.IncompatiblePackage(
                new PackageIdentity("foo", new NuGetVersion("1.0.0")),
                NuGetFramework.Parse("net451"),
                "win10-x86",
                new [] { NuGetFramework.Parse("net40"), NuGetFramework.Parse("net45") });
            
            // Act & Assert
            Assert.NotEqual(a, b);
        }
        
        [Fact]
        public void CompatibilityIssue_Equals_DifferentFramework()
        {
            // Arrange
            var a = CompatibilityIssue.IncompatiblePackage(
                new PackageIdentity("foo", new NuGetVersion("1.0.0")),
                NuGetFramework.Parse("net451"),
                "win10-x64",
                new [] { NuGetFramework.Parse("net40"), NuGetFramework.Parse("net45") });
            var b = CompatibilityIssue.IncompatiblePackage(
                new PackageIdentity("foo", new NuGetVersion("1.0.0")),
                NuGetFramework.Parse("net46"),
                "win10-x64",
                new [] { NuGetFramework.Parse("net40"), NuGetFramework.Parse("net45") });
            
            // Act & Assert
            Assert.NotEqual(a, b);
        }
    }
}
