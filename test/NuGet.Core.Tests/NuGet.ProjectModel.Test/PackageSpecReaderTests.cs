using System;
using System.Linq;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class PackageSpecReaderTests
    {
        [Fact]
        public void PackageSpecReader_PackageMissingVersion()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""type"": ""build""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";

            // Act
            Exception exception = null;

            try
            {
                var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Assert
            Assert.Contains("specify a version range", exception.Message);
        }

        [Fact]
        public void PackageSpecReader_ProjectMissingVersion()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""target"": ""project""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";

            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var range = spec.Dependencies.Single().LibraryRange.VersionRange;

            // Assert
            Assert.Equal(VersionRange.All, range);
        }

        [Fact]
        public void PackageSpecReader_PackageEmptyVersion()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""target"": ""package"",
                                    ""version"": """"
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";

            Exception exception = null;

            try
            {
                var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Assert
            Assert.Contains("specify a version range", exception.Message);
        }

        [Fact]
        public void PackageSpecReader_PackageWhitespaceVersion()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""target"": ""package"",
                                    ""version"": ""   ""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";

            Exception exception = null;

            try
            {
                var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Assert
            Assert.Contains("not a valid version string", exception.Message);
        }

        [Fact]
        public void PackageSpecReader_FrameworkAssemblyEmptyVersion()
        {
            // Arrange
            var json = @"{
                            ""frameworks"": {
                                ""net46"": {
                                    ""frameworkAssemblies"": {
                                       ""packageA"": """"
                                    }
                                }
                            }
                        }";

            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var range = spec.TargetFrameworks.Single().Dependencies.Single().LibraryRange.VersionRange;

            // Assert
            Assert.Equal(VersionRange.All, range);
        }

        [Fact]
        public void PackageSpecReader_ToolVersionValue()
        {
            // Arrange
            var json = @"{
                           ""tools"": {
                             ""packageA"": ""1.2.0-*"",
                             ""packageB"": ""1.3.0-*""
                           }
                         }";

            // Act
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            
            // Assert
            Assert.Equal(2, actual.Tools.Count);

            Assert.Equal("packageA", actual.Tools[0].LibraryRange.Name);
            Assert.Equal(VersionRange.Parse("1.2.0-*"), actual.Tools[0].LibraryRange.VersionRange);
            Assert.Equal(LibraryDependencyTarget.Package, actual.Tools[0].LibraryRange.TypeConstraint);

            Assert.Equal("packageB", actual.Tools[1].LibraryRange.Name);
            Assert.Equal(VersionRange.Parse("1.3.0-*"), actual.Tools[1].LibraryRange.VersionRange);
            Assert.Equal(LibraryDependencyTarget.Package, actual.Tools[1].LibraryRange.TypeConstraint);
        }

        [Fact]
        public void PackageSpecReader_ToolsAreOnlyPackages()
        {
            // Arrange
            var json = @"{
                           ""tools"": {
                             ""packageA"": {
                               ""target"": ""project"",
                               ""version"": ""1.2.0-*""
                             }
                           }
                         }";

            // Act
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            Assert.Equal(1, actual.Tools.Count);
            Assert.Equal("packageA", actual.Tools[0].LibraryRange.Name);
            Assert.Equal(VersionRange.Parse("1.2.0-*"), actual.Tools[0].LibraryRange.VersionRange);
            Assert.Equal(LibraryDependencyTarget.Package, actual.Tools[0].LibraryRange.TypeConstraint);
        }
        
        [Fact]
        public void PackageSpecReader_ToolMissingVersion()
        {
            // Arrange
            var json = @"{
                           ""tools"": {
                             ""packageA"": {
                             }
                           }
                         }";

            // Act & Assert
            var actual = Assert.Throws<FileFormatException>(() => JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json"));
            Assert.Contains("Tools must specify a version range.", actual.Message);
        }

        [Fact]
        public void PackageSpecReader_ToolEmptyVersion()
        {
            // Arrange
            var json = @"{
                           ""tools"": {
                             ""packageA"": {
                               ""version"": """"
                             }
                           }
                         }";

            // Act & Assert
            var actual = Assert.Throws<FileFormatException>(() => JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json"));
            Assert.Contains("not a valid version string", actual.Message);
        }

        [Fact]
        public void PackageSpecReader_ToolWhitespaceVersion()
        {
            // Arrange
            var json = @"{
                           ""tools"": {
                             ""packageA"": {
                               ""version"": "" ""
                             }
                           }
                         }";

            // Act & Assert
            var actual = Assert.Throws<FileFormatException>(() => JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json"));
            Assert.Contains("not a valid version string", actual.Message);
        }
    }
}
