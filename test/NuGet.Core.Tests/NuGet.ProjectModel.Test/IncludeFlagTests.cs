using System.Linq;
using NuGet.LibraryModel;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class IncludeFlagTests
    {
        [Fact]
        public void IncludeFlag_UnknownFlagsParse()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""include"": ""futureFlag"",
                                    ""exclude"": ""futureFlag2"",
                                    ""suppressParent"": ""futureFlag""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            var futureFlag = LibraryIncludeType.Parse(new string[] { "futureFlag" });

            // Assert
            Assert.True(dependency.IncludeType.Equals(futureFlag));
            Assert.True(dependency.SuppressParent.Equals(futureFlag));
        }

        [Fact]
        public void IncludeFlag_SuppressParentFlags()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""suppressParent"": ""build,contentFiles,runtime,native""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.SuppressParent.Equals(LibraryIncludeType.Parse(new string[] { "build", "runtime", "contentFiles", "native" })));
        }

        [Fact]
        public void IncludeFlag_SuppressParentAll()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""suppressParent"": ""all""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.SuppressParent.Equals(LibraryIncludeType.All));
        }

        [Fact]
        public void IncludeFlag_SuppressParentDefault()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.SuppressParent.Equals(LibraryIncludeType.DefaultSuppress));
        }

        [Fact]
        public void IncludeFlag_EmptyExcludeInclude()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""exclude"": """",
                                    ""include"": """"
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.IncludeType.Equals(LibraryIncludeType.None));
        }

        [Fact]
        public void IncludeFlag_EmptyExclude()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""exclude"": """"
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.IncludeType.Equals(LibraryIncludeType.All));
        }

        [Fact]
        public void IncludeFlag_EmptyInclude()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""include"": """"
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.IncludeType.Equals(LibraryIncludeType.None));
        }

        [Fact]
        public void IncludeFlag_ExcludeOnlyContent()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""exclude"": ""contentFiles""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.IncludeType.Equals(LibraryIncludeType.Parse(new string[] { "build", "runtime", "compile", "native" })));
        }

        [Fact]
        public void IncludeFlag_IncludeOnlyContent()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""include"": ""contentFiles""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.IncludeType.Equals(LibraryIncludeType.Parse(new string[] { "contentFiles" })));
        }

        [Fact]
        public void IncludeFlag_IncludeBuildExcludeBuild()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""include"": ""build"", 
                                    ""exclude"": ""build""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.IncludeType.Equals(LibraryIncludeType.None));
        }

        [Fact]
        public void IncludeFlag_IncludeBuildExcludeAll()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""include"": ""build,compile"", 
                                    ""exclude"": ""all""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.IncludeType.Equals(LibraryIncludeType.None));
        }

        [Fact]
        public void IncludeFlag_IncludeNoneExcludeAll()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""include"": ""none"", 
                                    ""exclude"": ""all""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.IncludeType.Equals(LibraryIncludeType.None));
        }

        [Fact]
        public void IncludeFlag_IncludeAllExcludeAll()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0"",
                                    ""include"": ""all"", 
                                    ""exclude"": ""all""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.IncludeType.Equals(LibraryIncludeType.None));
        }

        [Fact]
        public void IncludeFlag_DefaultValues()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""1.0.0""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            }
                        }";


            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");
            var dependency = spec.Dependencies.Single();

            // Assert
            Assert.True(dependency.IncludeType.Equals(LibraryIncludeType.All));
        }
    }
}
