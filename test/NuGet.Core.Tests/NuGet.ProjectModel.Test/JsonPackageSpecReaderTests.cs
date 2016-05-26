using System;
using System.Linq;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class JsonPackageSpecReaderTests
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
                var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);
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
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);
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
                var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);
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
                var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);
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
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);
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
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);
            
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
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);

            // Assert
            Assert.Equal(1, actual.Tools.Count);
            Assert.Equal("packageA", actual.Tools[0].LibraryRange.Name);
            Assert.Equal(VersionRange.Parse("1.2.0-*"), actual.Tools[0].LibraryRange.VersionRange);
            Assert.Equal(LibraryDependencyTarget.Package, actual.Tools[0].LibraryRange.TypeConstraint);
            Assert.Equal(0, actual.Tools[0].Imports.Count);
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
            var actual = Assert.Throws<FileFormatException>(() => JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty));
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
            var actual = Assert.Throws<FileFormatException>(() => JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty));
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
            var actual = Assert.Throws<FileFormatException>(() => JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty));
            Assert.Contains("not a valid version string", actual.Message);
        }

        [Fact]
        public void PackageSpecReader_ToolInvalidImports()
        {
            // Arrange
            var json = @"{
                           ""tools"": {
                             ""packageA"": {
                               ""imports"": ""a"",
                               ""version"": ""1.2.0-*""
                             }
                           }
                         }";

            // Act & Assert
            var actual = Assert.Throws<FileFormatException>(() => JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty));
            Assert.Contains("Imports contains an invalid framework: 'a' in 'project.json'.", actual.Message);
        }

        [Fact]
        public void PackageSpecReader_ToolCommaSeparatedImports()
        {
            // Arrange
            var json = @"{
                           ""tools"": {
                             ""packageA"": {
                               ""imports"": ""net45, dnxcore50"",
                               ""version"": ""1.2.0-*""
                             }
                           }
                         }";

            // Act
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);
            
            // Assert
            Assert.Equal(1, actual.Tools.Count);
            Assert.Equal("packageA", actual.Tools[0].LibraryRange.Name);
            Assert.Equal(VersionRange.Parse("1.2.0-*"), actual.Tools[0].LibraryRange.VersionRange);
            Assert.Equal(LibraryDependencyTarget.Package, actual.Tools[0].LibraryRange.TypeConstraint);
            Assert.Equal(1, actual.Tools[0].Imports.Count());
            
            // comma-separated frameworks are not supported
            Assert.Equal("net45,Version=v0.0", actual.Tools[0].Imports.First().ToString());
        }

        [Fact]
        public void PackageSpecReader_ToolSingleImport()
        {
            // Arrange
            var json = @"{
                           ""tools"": {
                             ""packageA"": {
                               ""imports"": ""net45"",
                               ""version"": ""1.2.0-*""
                             }
                           }
                         }";

            // Act
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);
            
            // Assert
            Assert.Equal(1, actual.Tools.Count);
            Assert.Equal("packageA", actual.Tools[0].LibraryRange.Name);
            Assert.Equal(VersionRange.Parse("1.2.0-*"), actual.Tools[0].LibraryRange.VersionRange);
            Assert.Equal(LibraryDependencyTarget.Package, actual.Tools[0].LibraryRange.TypeConstraint);
            Assert.Equal(1, actual.Tools[0].Imports.Count());
            
            // comma-separated frameworks are not supported
            Assert.Equal(".NETFramework,Version=v4.5", actual.Tools[0].Imports[0].ToString());
        }

        [Fact]
        public void PackageSpecReader_ToolArrayImports()
        {
            // Arrange
            var json = @"{
                           ""tools"": {
                             ""packageA"": {
                               ""imports"": [""net45"", ""dnxcore50""],
                               ""version"": ""1.2.0-*""
                             }
                           }
                         }";

            // Act
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);
            
            // Assert
            Assert.Equal(1, actual.Tools.Count);
            Assert.Equal("packageA", actual.Tools[0].LibraryRange.Name);
            Assert.Equal(VersionRange.Parse("1.2.0-*"), actual.Tools[0].LibraryRange.VersionRange);
            Assert.Equal(LibraryDependencyTarget.Package, actual.Tools[0].LibraryRange.TypeConstraint);
            Assert.Equal(2, actual.Tools[0].Imports.Count());
            Assert.Equal(".NETFramework,Version=v4.5", actual.Tools[0].Imports[0].ToString());
            Assert.Equal("DNXCore,Version=v5.0", actual.Tools[0].Imports[1].ToString());
        }

        [Fact]
        public void PackageSpecReader_SetsPlatformDependencyFlagsCorrectly()
        {
            // Arrange
            var json = @"{
                           ""dependencies"": {
                             ""redist"": {
                               ""version"": ""1.0.0"",
                               ""type"": ""platform""
                             }
                           }
                         }";

            // Act
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);

            // Assert
            var dep = actual.Dependencies.FirstOrDefault(d => d.Name.Equals("redist"));
            Assert.NotNull(dep);
            Assert.Equal(LibraryDependencyTypeKeyword.Platform.CreateType(), dep.Type);

            var expected = LibraryIncludeFlags.Build |
                LibraryIncludeFlags.Compile |
                LibraryIncludeFlags.Analyzers;
            Assert.Equal(expected, dep.IncludeType);
        }

        [Fact]
        public void PackageSpecReader_ExplicitExcludesAddToTypePlatform()
        {
            // Arrange
            var json = @"{
                           ""dependencies"": {
                             ""redist"": {
                               ""version"": ""1.0.0"",
                               ""type"": ""platform"",
                               ""exclude"": ""analyzers""
                             }
                           }
                         }";

            // Act
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);

            // Assert
            var dep = actual.Dependencies.FirstOrDefault(d => d.Name.Equals("redist"));
            Assert.NotNull(dep);
            Assert.Equal(LibraryDependencyTypeKeyword.Platform.CreateType(), dep.Type);

            var expected = LibraryIncludeFlags.Build |
                LibraryIncludeFlags.Compile;
            Assert.Equal(expected, dep.IncludeType);
        }

        [Fact]
        public void PackageSpecReader_ExplicitIncludesOverrideTypePlatform()
        {
            // Arrange
            var json = @"{
                           ""dependencies"": {
                             ""redist"": {
                               ""version"": ""1.0.0"",
                               ""type"": ""platform"",
                               ""include"": ""analyzers""
                             }
                           }
                         }";

            // Act
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);

            // Assert
            var dep = actual.Dependencies.FirstOrDefault(d => d.Name.Equals("redist"));
            Assert.NotNull(dep);
            Assert.Equal(LibraryDependencyTypeKeyword.Platform.CreateType(), dep.Type);

            var expected = LibraryIncludeFlags.Analyzers;
            Assert.Equal(expected, dep.IncludeType);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData(@"{
                        ""packOptions"": {}
                      }")]
        [InlineData(@"{
                        ""packOptions"": {
                          ""foo"": [1, 2]
                        }
                      }")]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": null
                        }
                      }")]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": []
                        }
                      }")]
        public void PackageSpecReader_PackOptions_Default(string json)
        {
            // Arrange & Act
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);

            // Assert
            Assert.NotNull(actual.PackOptions);
            Assert.NotNull(actual.PackOptions.PackageType);
            Assert.Empty(actual.PackOptions.PackageType);
        }

        [Theory]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": ""foo""
                        }
                      }", new[] { "foo" })]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": ""foo, bar""
                        }
                      }", new[] { "foo, bar" })]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": [ ""foo"" ]
                        }
                      }", new[] { "foo" })]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": [ ""foo, bar"" ]
                        }
                      }", new[] { "foo, bar" })]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": [ ""foo"", ""bar"" ]
                        }
                      }", new[] { "foo", "bar" })]
        public void PackageSpecReader_PackOptions_ValidPackageType(string json, string[] expectedNames)
        {
            // Arrange
            var expected = expectedNames
                .Select(n => new PackageType(n, PackageType.EmptyVersion))
                .ToArray();

            // Act
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty);

            // Assert
            Assert.NotNull(actual.PackOptions);
            Assert.NotNull(actual.PackOptions.PackageType);
            Assert.Equal(expected, actual.PackOptions.PackageType.ToArray());
        }

        [Theory]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": 1
                        }
                      }")]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": false
                        }
                      }")]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": 1.0
                        }
                      }")]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": {}
                        }
                      }")]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": {
                            ""name"": ""foo""
                          }
                        }
                      }")]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": [
                            { ""name"": ""foo"" },
                            { ""name"": ""bar"" }
                          ]
                        }
                      }")]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": [
                            ""foo"",
                            null
                          ]
                        }
                      }")]
        [InlineData(@"{
                        ""packOptions"": {
                          ""packageType"": [
                            ""foo"",
                            true
                          ]
                        }
                      }")]
        public void PackageSpecReader_PackOptions_InvalidPackageType(string json)
        {
            // Arrange & Act & Assert
            var actual = Assert.Throws<FileFormatException>(
                () => JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json", string.Empty));

            Assert.Contains("The pack options package type must be a string or array of strings in 'project.json'.", actual.Message);
        }
    }
}
