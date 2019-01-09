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
