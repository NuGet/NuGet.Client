// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Test.Utility;
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
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

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
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

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
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

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
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

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
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

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
                () => JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json"));

            Assert.Contains("The pack options package type must be a string or array of strings in 'project.json'.", actual.Message);
        }

        [Fact]
        public void PackageSpecReader_PackOptions_Files1()
        {
            // Arrange & Act
            var json = @"{
                        ""packOptions"": {
                          ""files"": {
                            ""include"": ""file1"",
                            ""exclude"": ""file2"",
                            ""includeFiles"": ""file3"",
                            ""excludeFiles"": ""file4"",
                            ""mappings"": {
                              ""dest/path"": ""./src/path""
                            }
                          }
                        }
                      }";
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            Assert.NotNull(actual.PackOptions);
            Assert.Equal(1, actual.PackOptions.IncludeExcludeFiles.Include.Count);
            Assert.Equal(1, actual.PackOptions.IncludeExcludeFiles.Exclude.Count);
            Assert.Equal(1, actual.PackOptions.IncludeExcludeFiles.IncludeFiles.Count);
            Assert.Equal(1, actual.PackOptions.IncludeExcludeFiles.ExcludeFiles.Count);
            Assert.Equal("file1", actual.PackOptions.IncludeExcludeFiles.Include.First());
            Assert.Equal("file2", actual.PackOptions.IncludeExcludeFiles.Exclude.First());
            Assert.Equal("file3", actual.PackOptions.IncludeExcludeFiles.IncludeFiles.First());
            Assert.Equal("file4", actual.PackOptions.IncludeExcludeFiles.ExcludeFiles.First());
            Assert.NotNull(actual.PackOptions.Mappings);
            Assert.Equal(1, actual.PackOptions.Mappings.Count());
            Assert.Equal("dest/path", actual.PackOptions.Mappings.First().Key);
            Assert.Equal(1, actual.PackOptions.Mappings.First().Value.Include.Count());
            Assert.Null(actual.PackOptions.Mappings.First().Value.Exclude);
            Assert.Null(actual.PackOptions.Mappings.First().Value.IncludeFiles);
            Assert.Null(actual.PackOptions.Mappings.First().Value.ExcludeFiles);
            Assert.Equal("./src/path", actual.PackOptions.Mappings.First().Value.Include.First());
        }

        [Fact]
        public void PackageSpecReader_PackOptions_Files2()
        {
            // Arrange & Act
            var json = @"{
                        ""packOptions"": {
                          ""files"": {
                            ""include"": [""file1a"", ""file1b""],
                            ""exclude"": [""file2a"", ""file2b""],
                            ""includeFiles"": [""file3a"", ""file3b""],
                            ""excludeFiles"": [""file4a"", ""file4b""],
                            ""mappings"": {
                              ""dest/path1"": [""./src/path1"", ""./src/path2""],
                              ""dest/path2"": {
                                ""includeFiles"": [""map1a"", ""map1b""],
                              },
                            }
                          }
                        }
                      }";
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            Assert.NotNull(actual.PackOptions);
            Assert.Equal(2, actual.PackOptions.IncludeExcludeFiles.Include.Count);
            Assert.Equal(2, actual.PackOptions.IncludeExcludeFiles.Exclude.Count);
            Assert.Equal(2, actual.PackOptions.IncludeExcludeFiles.IncludeFiles.Count);
            Assert.Equal(2, actual.PackOptions.IncludeExcludeFiles.ExcludeFiles.Count);
            Assert.Equal("file1a", actual.PackOptions.IncludeExcludeFiles.Include.First());
            Assert.Equal("file2a", actual.PackOptions.IncludeExcludeFiles.Exclude.First());
            Assert.Equal("file3a", actual.PackOptions.IncludeExcludeFiles.IncludeFiles.First());
            Assert.Equal("file4a", actual.PackOptions.IncludeExcludeFiles.ExcludeFiles.First());
            Assert.Equal("file1b", actual.PackOptions.IncludeExcludeFiles.Include.Last());
            Assert.Equal("file2b", actual.PackOptions.IncludeExcludeFiles.Exclude.Last());
            Assert.Equal("file3b", actual.PackOptions.IncludeExcludeFiles.IncludeFiles.Last());
            Assert.Equal("file4b", actual.PackOptions.IncludeExcludeFiles.ExcludeFiles.Last());
            Assert.NotNull(actual.PackOptions.Mappings);
            Assert.Equal(2, actual.PackOptions.Mappings.Count());
            Assert.Equal("dest/path1", actual.PackOptions.Mappings.First().Key);
            Assert.Equal("dest/path2", actual.PackOptions.Mappings.Last().Key);
            Assert.Equal(2, actual.PackOptions.Mappings.First().Value.Include.Count());
            Assert.Null(actual.PackOptions.Mappings.First().Value.Exclude);
            Assert.Null(actual.PackOptions.Mappings.First().Value.IncludeFiles);
            Assert.Null(actual.PackOptions.Mappings.First().Value.ExcludeFiles);
            Assert.Equal("./src/path1", actual.PackOptions.Mappings.First().Value.Include.First());
            Assert.Equal("./src/path2", actual.PackOptions.Mappings.First().Value.Include.Last());
            Assert.Null(actual.PackOptions.Mappings.Last().Value.Include);
            Assert.Null(actual.PackOptions.Mappings.Last().Value.Exclude);
            Assert.Null(actual.PackOptions.Mappings.Last().Value.ExcludeFiles);
            Assert.Equal("map1a", actual.PackOptions.Mappings.Last().Value.IncludeFiles.First());
            Assert.Equal("map1b", actual.PackOptions.Mappings.Last().Value.IncludeFiles.Last());
        }

        [Theory]
        [InlineData("{}", null, true)]
        [InlineData(@"{
                        ""buildOptions"": {}
                      }", null, false)]
        [InlineData(@"{
                        ""buildOptions"": {
                          ""outputName"": ""dllName""
                        }
                      }", "dllName", false)]
        [InlineData(@"{
                        ""buildOptions"": {
                          ""outputName"": ""dllName2"",
                          ""emitEntryPoint"": true
                        }
                      }", "dllName2", false)]
        [InlineData(@"{
                        ""buildOptions"": {
                          ""outputName"": null
                        }
                      }", null, false)]
        public void PackageSpecReader_BuildOptions(string json, string expectedValue, bool nullBuildOptions)
        {
            // Arrange & Act
            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            if (nullBuildOptions)
            {
                Assert.Null(actual.BuildOptions);
            }
            else
            {
                Assert.NotNull(actual.BuildOptions);
                Assert.Equal(expectedValue, actual.BuildOptions.OutputName);
            }
        }

        [Fact]
        public void PackageSpecReader_ReadsWithRestoreSettings()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""target"": ""package"",
                                    ""version"": ""1.0.0""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            },
                            ""restoreSettings"": {
                            ""hideWarningsAndErrors"": true
                            },
                        }";

            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            Assert.NotNull(actual);
            Assert.NotNull(actual.RestoreSettings);
            Assert.True(actual.RestoreSettings.HideWarningsAndErrors);
        }

        [Fact]
        public void PackageSpecReader_ReadsWithoutRestoreSettings()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""target"": ""package"",
                                    ""version"": ""1.0.0""
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            },
                        }";

            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            Assert.NotNull(actual);
            Assert.NotNull(actual.RestoreSettings);
            Assert.False(actual.RestoreSettings.HideWarningsAndErrors);
        }

        [Fact]
        public void PackageSpecReader_ReadsDependencyWithMultipleNoWarn()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""target"": ""package"",
                                    ""version"": ""1.0.0"",
                                    ""noWarn"": [
                                        ""NU1500"",
                                        ""NU1107""
                                      ]
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            },
                        }";

            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            var dep = actual.Dependencies.FirstOrDefault(d => d.Name.Equals("packageA"));
            Assert.NotNull(dep);
            Assert.NotNull(dep.NoWarn);
            Assert.Equal(dep.NoWarn.Count, 2);
            Assert.True(dep.NoWarn.Contains(NuGetLogCode.NU1500));
            Assert.True(dep.NoWarn.Contains(NuGetLogCode.NU1107));
        }

        [Fact]
        public void PackageSpecReader_ReadsDependencyWithSingleNoWarn()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""target"": ""package"",
                                    ""version"": ""1.0.0"",
                                    ""noWarn"": [
                                        ""NU1500""
                                      ]
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            },
                        }";

            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            var dep = actual.Dependencies.FirstOrDefault(d => d.Name.Equals("packageA"));
            Assert.NotNull(dep);
            Assert.NotNull(dep.NoWarn);
            Assert.Equal(dep.NoWarn.Count, 1);
            Assert.True(dep.NoWarn.Contains(NuGetLogCode.NU1500));
        }

        [Fact]
        public void PackageSpecReader_ReadsDependencyWithSingleEmptyNoWarn()
        {
            // Arrange
            var json = @"{
                          ""dependencies"": {
                                ""packageA"": {
                                    ""target"": ""package"",
                                    ""version"": ""1.0.0"",
                                    ""noWarn"": [
                                      ]
                                }
                            },
                            ""frameworks"": {
                                ""net46"": {}
                            },
                        }";

            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            var dep = actual.Dependencies.FirstOrDefault(d => d.Name.Equals("packageA"));
            Assert.NotNull(dep);
            Assert.NotNull(dep.NoWarn);
            Assert.Equal(dep.NoWarn.Count, 0);
        }

        [Fact]
        public void PackageSpecReader_ReadsRestoreMetadataWithWarningProperties()
        {
            // Arrange
            var json = @"{  
                            ""restore"": {
    ""projectUniqueName"": ""projectUniqueName"",
    ""projectName"": ""projectName"",
    ""projectPath"": ""projectPath"",
    ""projectJsonPath"": ""projectJsonPath"",
    ""packagesPath"": ""packagesPath"",
    ""outputPath"": ""outputPath"",
    ""projectStyle"": ""PackageReference"",
    ""crossTargeting"": true,
    ""configFilePaths"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""fallbackFolders"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""originalTargetFrameworks"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""sources"": {
      ""source"": {}
    },
    ""frameworks"": {
      ""frameworkidentifier123-frameworkprofile"": {
        ""projectReferences"": {}
      }
    },
    ""warningProperties"": {
      ""allWarningsAsErrors"": true,
      ""noWarn"": [
        ""NU1601"",
      ],
      ""warnAsError"": [
        ""NU1500"",
        ""NU1501""
      ]
    }
  }
}";

            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            var metadata = actual.RestoreMetadata;
            var warningProperties = actual.RestoreMetadata.ProjectWideWarningProperties;

            Assert.NotNull(metadata);
            Assert.NotNull(warningProperties);
            Assert.True(warningProperties.AllWarningsAsErrors);
            Assert.Equal(1, warningProperties.NoWarn.Count);
            Assert.True(warningProperties.NoWarn.Contains(NuGetLogCode.NU1601));
            Assert.Equal(2, warningProperties.WarningsAsErrors.Count);
            Assert.True(warningProperties.WarningsAsErrors.Contains(NuGetLogCode.NU1500));
            Assert.True(warningProperties.WarningsAsErrors.Contains(NuGetLogCode.NU1501));
        }

        [Fact]
        public void PackageSpecReader_ReadsRestoreMetadataWithWarningPropertiesAndNo_NoWarn()
        {
            // Arrange
            var json = @"{  
                            ""restore"": {
    ""projectUniqueName"": ""projectUniqueName"",
    ""projectName"": ""projectName"",
    ""projectPath"": ""projectPath"",
    ""projectJsonPath"": ""projectJsonPath"",
    ""packagesPath"": ""packagesPath"",
    ""outputPath"": ""outputPath"",
    ""projectStyle"": ""PackageReference"",
    ""crossTargeting"": true,
    ""configFilePaths"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""fallbackFolders"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""originalTargetFrameworks"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""sources"": {
      ""source"": {}
    },
    ""frameworks"": {
      ""frameworkidentifier123-frameworkprofile"": {
        ""projectReferences"": {}
      }
    },
    ""warningProperties"": {
      ""allWarningsAsErrors"": true,
      ""warnAsError"": [
        ""NU1500"",
        ""NU1501""
      ]
    }
  }
}";

            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            var metadata = actual.RestoreMetadata;
            var warningProperties = actual.RestoreMetadata.ProjectWideWarningProperties;

            Assert.NotNull(metadata);
            Assert.NotNull(warningProperties);
            Assert.True(warningProperties.AllWarningsAsErrors);
            Assert.Equal(0, warningProperties.NoWarn.Count);
            Assert.Equal(2, warningProperties.WarningsAsErrors.Count);
            Assert.True(warningProperties.WarningsAsErrors.Contains(NuGetLogCode.NU1500));
            Assert.True(warningProperties.WarningsAsErrors.Contains(NuGetLogCode.NU1501));
        }

        [Fact]
        public void PackageSpecReader_ReadsRestoreMetadataWithWarningPropertiesAndNo_WarnAsError()
        {
            // Arrange
            var json = @"{  
                            ""restore"": {
    ""projectUniqueName"": ""projectUniqueName"",
    ""projectName"": ""projectName"",
    ""projectPath"": ""projectPath"",
    ""projectJsonPath"": ""projectJsonPath"",
    ""packagesPath"": ""packagesPath"",
    ""outputPath"": ""outputPath"",
    ""projectStyle"": ""PackageReference"",
    ""crossTargeting"": true,
    ""configFilePaths"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""fallbackFolders"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""originalTargetFrameworks"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""sources"": {
      ""source"": {}
    },
    ""frameworks"": {
      ""frameworkidentifier123-frameworkprofile"": {
        ""projectReferences"": {}
      }
    },
    ""warningProperties"": {
      ""allWarningsAsErrors"": true,
      ""noWarn"": [
        ""NU1601"",
      ]
    }
  }
}";

            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            var metadata = actual.RestoreMetadata;
            var warningProperties = actual.RestoreMetadata.ProjectWideWarningProperties;

            Assert.NotNull(metadata);
            Assert.NotNull(warningProperties);
            Assert.True(warningProperties.AllWarningsAsErrors);
            Assert.Equal(1, warningProperties.NoWarn.Count);
            Assert.True(warningProperties.NoWarn.Contains(NuGetLogCode.NU1601));
            Assert.Equal(0, warningProperties.WarningsAsErrors.Count);
        }

        [Fact]
        public void PackageSpecReader_ReadsRestoreMetadataWithWarningPropertiesAndNo_AllWarningsAsErrors()
        {
            // Arrange
            var json = @"{  
                            ""restore"": {
    ""projectUniqueName"": ""projectUniqueName"",
    ""projectName"": ""projectName"",
    ""projectPath"": ""projectPath"",
    ""projectJsonPath"": ""projectJsonPath"",
    ""packagesPath"": ""packagesPath"",
    ""outputPath"": ""outputPath"",
    ""projectStyle"": ""PackageReference"",
    ""crossTargeting"": true,
    ""configFilePaths"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""fallbackFolders"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""originalTargetFrameworks"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""sources"": {
      ""source"": {}
    },
    ""frameworks"": {
      ""frameworkidentifier123-frameworkprofile"": {
        ""projectReferences"": {}
      }
    },
    ""warningProperties"": {
      ""noWarn"": [
        ""NU1601"",
      ],
      ""warnAsError"": [
        ""NU1500"",
        ""NU1501""
      ]
    }
  }
}";

            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            var metadata = actual.RestoreMetadata;
            var warningProperties = actual.RestoreMetadata.ProjectWideWarningProperties;

            Assert.NotNull(metadata);
            Assert.NotNull(warningProperties);
            Assert.False(warningProperties.AllWarningsAsErrors);
            Assert.Equal(1, warningProperties.NoWarn.Count);
            Assert.True(warningProperties.NoWarn.Contains(NuGetLogCode.NU1601));
            Assert.Equal(2, warningProperties.WarningsAsErrors.Count);
            Assert.True(warningProperties.WarningsAsErrors.Contains(NuGetLogCode.NU1500));
            Assert.True(warningProperties.WarningsAsErrors.Contains(NuGetLogCode.NU1501));
        }

        [Fact]
        public void PackageSpecReader_ReadsRestoreMetadataWithEmptyWarningPropertiesAnd()
        {
            // Arrange
            var json = @"{  
                            ""restore"": {
    ""projectUniqueName"": ""projectUniqueName"",
    ""projectName"": ""projectName"",
    ""projectPath"": ""projectPath"",
    ""projectJsonPath"": ""projectJsonPath"",
    ""packagesPath"": ""packagesPath"",
    ""outputPath"": ""outputPath"",
    ""projectStyle"": ""PackageReference"",
    ""crossTargeting"": true,
    ""configFilePaths"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""fallbackFolders"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""originalTargetFrameworks"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""sources"": {
      ""source"": {}
    },
    ""frameworks"": {
      ""frameworkidentifier123-frameworkprofile"": {
        ""projectReferences"": {}
      }
    },
    ""warningProperties"": {
    }
  }
}";

            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            var metadata = actual.RestoreMetadata;
            var warningProperties = actual.RestoreMetadata.ProjectWideWarningProperties;

            Assert.NotNull(metadata);
            Assert.NotNull(warningProperties);
            Assert.False(warningProperties.AllWarningsAsErrors);
            Assert.Equal(0, warningProperties.NoWarn.Count);
            Assert.Equal(0, warningProperties.WarningsAsErrors.Count);
        }

        [Fact]
        public void PackageSpecReader_ReadsRestoreMetadataWithNoWarningProperties()
        {
            // Arrange
            var json = @"{  
                            ""restore"": {
    ""projectUniqueName"": ""projectUniqueName"",
    ""projectName"": ""projectName"",
    ""projectPath"": ""projectPath"",
    ""projectJsonPath"": ""projectJsonPath"",
    ""packagesPath"": ""packagesPath"",
    ""outputPath"": ""outputPath"",
    ""projectStyle"": ""PackageReference"",
    ""crossTargeting"": true,
    ""configFilePaths"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""fallbackFolders"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""originalTargetFrameworks"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""sources"": {
      ""source"": {}
    },
    ""frameworks"": {
      ""frameworkidentifier123-frameworkprofile"": {
        ""projectReferences"": {}
      }
    }
  }
}";

            var actual = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.json");

            // Assert
            var metadata = actual.RestoreMetadata;
            var warningProperties = actual.RestoreMetadata.ProjectWideWarningProperties;

            Assert.NotNull(metadata);
            Assert.NotNull(warningProperties);
        }
    }
}
