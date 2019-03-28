// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.RuntimeModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class PackageSpecWriterTests
    {
        private static readonly PackageSpec _emptyPackageSpec = JsonPackageSpecReader.GetPackageSpec(new JObject());

        [Fact]
        public void RoundTripAutoReferencedProperty()
        {
            // Arrange
            var json = @"{
                    ""dependencies"": {
                        ""b"": {
                            ""version"": ""[1.0.0, )"",
                            ""autoReferenced"": true
                        }
                    },
                  ""frameworks"": {
                    ""net46"": {
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                                ""autoReferenced"": true
                            }
                        }
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void RoundTripDownloadDependencies()
        {
            // Arrange
            var json = @"{
                  ""frameworks"": {
                    ""net46"": {
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                                ""autoReferenced"": true
                            }
                        },
                        ""downloadDependencies"": [
                            {""name"" : ""a"", ""version"" : ""[1.0.0, )""},
                            {""name"" : ""b"", ""version"" : ""[2.0.0, )""},
                            {""name"" : ""b"", ""version"" : ""[2.0.0, )""}
                        ]
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_ThrowsForNullPackageSpec()
        {
            var writer = new JsonObjectWriter();

            // Assert
            Assert.Throws<ArgumentNullException>(() => PackageSpecWriter.Write(packageSpec: null, writer: writer));
        }

        [Fact]
        public void Write_ThrowsForNullWriter()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => PackageSpecWriter.Write(_emptyPackageSpec, writer: null));
        }

        [Fact]
        public void Write_ReadWriteDependencies()
        {
            // Arrange
            var json = @"{
  ""title"": ""My Title"",
  ""version"": ""1.2.3"",
  ""description"": ""test"",
  ""authors"": [
    ""author1"",
    ""author2""
  ],
  ""copyright"": ""2016"",
  ""language"": ""en-US"",
  ""packInclude"": {
    ""file"": ""file.txt""
  },
  ""packOptions"": {
    ""owners"": [
      ""owner1"",
      ""owner2""
    ],
    ""tags"": [
      ""tag1"",
      ""tag2""
    ],
    ""projectUrl"": ""http://my.url.com"",
    ""iconUrl"": ""http://my.url.com"",
    ""summary"": ""Sum"",
    ""releaseNotes"": ""release noted"",
    ""licenseUrl"": ""http://my.url.com""
  },
  ""scripts"": {
    ""script1"": [
      ""script.js""
    ]
  },
  ""dependencies"": {
    ""packageA"": {
      ""suppressParent"": ""All"",
      ""target"": ""Project""
    }
  },
  ""frameworks"": {
    ""net46"": {}
  }
}";
            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_ReadWriteSinglePackageType()
        {
            // Arrange
            var json = @"{
  ""packOptions"": {
    ""packageType"": ""DotNetTool""
  }
}";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_ReadWriteMultiplePackageType()
        {
            // Arrange
            var json = @"{
  ""packOptions"": {
    ""packageType"": [
      ""Dependency"",
      ""DotNetTool""
    ]
  }
}";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_ReadWriteWarningProperties()
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
    ""fallbackFolders"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""configFilePaths"": [
      ""b"",
      ""a"",
      ""c""
    ],
    ""originalTargetFrameworks"": [
      ""a"",
      ""b"",
      ""c""
    ],
    ""sources"": {
      ""source"": {}
    },
    ""frameworks"": {
      ""net45"": {
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
            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void WriteToFile_ThrowsForNullPackageSpec()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => PackageSpecWriter.WriteToFile(packageSpec: null, filePath: @"C:\a.json"));
        }

        [Fact]
        public void WriteToFile_ThrowsForNullFilePath()
        {
            // Assert
            Assert.Throws<ArgumentException>(() => PackageSpecWriter.WriteToFile(_emptyPackageSpec, filePath: null));
        }

        [Fact]
        public void WriteToFile_ThrowsForEmptyFilePath()
        {
            // Assert
            Assert.Throws<ArgumentException>(() => PackageSpecWriter.WriteToFile(_emptyPackageSpec, filePath: null));
        }

        [Fact]
        public void Write_SerializesMembersAsJson()
        {
            // Arrange && Act
            var expectedJson = ResourceTestUtility.GetResource("NuGet.ProjectModel.Test.compiler.resources.PackageSpecWriter_Write_SerializesMembersAsJson.json", typeof(PackageSpecWriterTests));
            var packageSpec = CreatePackageSpec(withRestoreSettings: true);
            var actualJson = GetJsonString(packageSpec);

            // Assert
            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void Write_SerializesMembersAsJsonWithoutRestoreSettings()
        {
            // Arrange && Act
            var expectedJson = ResourceTestUtility.GetResource("NuGet.ProjectModel.Test.compiler.resources.PackageSpecWriter_Write_SerializesMembersAsJsonWithoutRestoreSettings.json", typeof(PackageSpecWriterTests));
            var packageSpec = CreatePackageSpec(withRestoreSettings: false);
            var actualJson = GetJsonString(packageSpec);

            // Assert
            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void Write_SerializesMembersAsJsonWithWarningProperties()
        {
            // Arrange && Act
            var expectedWarningPropertiesJson = @"{
  ""allWarningsAsErrors"": true,
  ""noWarn"": [
    ""NU1601"",
    ""NU1602""
  ],
  ""warnAsError"": [
    ""NU1500"",
    ""NU1501""
  ]
}";
            var allWarningsAsErrors = true;
            var warningsAsErrors = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500, NuGetLogCode.NU1501 };
            var noWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1602, NuGetLogCode.NU1601 };
            var warningProperties = new WarningProperties(warningsAsErrors, noWarn, allWarningsAsErrors);
            var packageSpec = CreatePackageSpec(withRestoreSettings: true, warningProperties: warningProperties);
            var actualJson = GetJsonObject(packageSpec);
            var actualWarningPropertiesJson = actualJson["restore"]["warningProperties"].ToString();

            // Assert
            Assert.NotNull(actualWarningPropertiesJson);
            Assert.Equal(expectedWarningPropertiesJson, actualWarningPropertiesJson);
        }

        [Fact]
        public void Write_SerializesMembersAsJsonWithWarningPropertiesAndNoAllWarningsAsErrors()
        {
            // Arrange && Act
            var expectedWarningPropertiesJson = @"{
  ""noWarn"": [
    ""NU1601"",
    ""NU1602""
  ],
  ""warnAsError"": [
    ""NU1500"",
    ""NU1501""
  ]
}";
            var allWarningsAsErrors = false;
            var warningsAsErrors = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500, NuGetLogCode.NU1501 };
            var noWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1602, NuGetLogCode.NU1601 };
            var warningProperties = new WarningProperties(warningsAsErrors, noWarn, allWarningsAsErrors);
            var packageSpec = CreatePackageSpec(withRestoreSettings: true, warningProperties: warningProperties);
            var actualJson = GetJsonObject(packageSpec);
            var actualWarningPropertiesJson = actualJson["restore"]["warningProperties"].ToString();

            // Assert
            Assert.NotNull(actualWarningPropertiesJson);
            Assert.Equal(expectedWarningPropertiesJson, actualWarningPropertiesJson);
        }

        [Fact]
        public void Write_SerializesMembersAsJsonWithWarningPropertiesAndNo_WarnAsError()
        {
            // Arrange && Act
            var expectedWarningPropertiesJson = @"{
  ""allWarningsAsErrors"": true,
  ""noWarn"": [
    ""NU1601"",
    ""NU1602""
  ]
}";
            var allWarningsAsErrors = true;
            var warningsAsErrors = new HashSet<NuGetLogCode> { };
            var noWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1602, NuGetLogCode.NU1601 };
            var warningProperties = new WarningProperties(warningsAsErrors, noWarn, allWarningsAsErrors);
            var packageSpec = CreatePackageSpec(withRestoreSettings: true, warningProperties: warningProperties);
            var actualJson = GetJsonObject(packageSpec);
            var actualWarningPropertiesJson = actualJson["restore"]["warningProperties"].ToString();

            // Assert
            Assert.NotNull(actualWarningPropertiesJson);
            Assert.Equal(expectedWarningPropertiesJson, actualWarningPropertiesJson);
        }

        [Fact]
        public void Write_SerializesMembersAsJsonWithEmptyWarningProperties()
        {
            // Arrange && Act
            var allWarningsAsErrors = false;
            var warningsAsErrors = new HashSet<NuGetLogCode> { };
            var noWarn = new HashSet<NuGetLogCode> { };
            var warningProperties = new WarningProperties(warningsAsErrors, noWarn, allWarningsAsErrors);
            var packageSpec = CreatePackageSpec(withRestoreSettings: true, warningProperties: warningProperties);
            var actualJson = GetJsonObject(packageSpec);
            var actualWarningPropertiesJson = actualJson["restore"]["warningProperties"];

            // Assert
            Assert.Null(actualWarningPropertiesJson);
        }

        [Fact]
        public void Write_SerializesMembersAsJsonWithWarningPropertiesAndNo_NoWarn()
        {
            // Arrange && Act
            var expectedWarningPropertiesJson = @"{
  ""allWarningsAsErrors"": true,
  ""warnAsError"": [
    ""NU1500"",
    ""NU1501""
  ]
}";
            var allWarningsAsErrors = true;
            var warningsAsErrors = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500, NuGetLogCode.NU1501 };
            var noWarn = new HashSet<NuGetLogCode> { };
            var warningProperties = new WarningProperties(warningsAsErrors, noWarn, allWarningsAsErrors);
            var packageSpec = CreatePackageSpec(withRestoreSettings: true, warningProperties: warningProperties);
            var actualJson = GetJsonObject(packageSpec);
            var actualWarningPropertiesJson = actualJson["restore"]["warningProperties"].ToString();

            // Assert
            Assert.NotNull(actualWarningPropertiesJson);
            Assert.Equal(expectedWarningPropertiesJson, actualWarningPropertiesJson);
        }

        [Fact]
        public void Write_ReadWriteDependenciesAreSorted()
        {
            // Arrange
            var json = @"{
                    ""dependencies"": {
                        ""b"": {
                                ""version"": ""[1.0.0, )"",
                        },
                        ""a"": {
                            ""version"": ""[1.0.0, )"",
                        }
                    },
                  ""frameworks"": {
                    ""net46"": {
                        ""dependencies"": {
                            ""b"": {
                                ""version"": ""[1.0.0, )"",
                            },
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                            }
                        },
                        ""downloadDependencies"": [
                            {""name"" : ""c"", ""version"" : ""[2.0.0]""},
                            {""name"" : ""d"", ""version"" : ""[2.0.0]""},
                       ],
                       ""frameworkReferences"" : [
                            ""b"",
                            ""a""
                       ]
                    }
                  }
                }";

            var expectedJson = @"{
                  ""dependencies"": {
                    ""a"": ""[1.0.0, )"",
                    ""b"": ""[1.0.0, )""
                  },
                  ""frameworks"": {
                    ""net46"": {
                      ""dependencies"": {
                        ""a"": ""[1.0.0, )"",
                        ""b"": ""[1.0.0, )""
                      },
                      ""downloadDependencies"": [
                            {""name"" : ""c"", ""version"" : ""[2.0.0, 2.0.0]""},
                            {""name"" : ""d"", ""version"" : ""[2.0.0, 2.0.0]""},
                       ],
                      ""frameworkReferences"" : [
                          ""a"",
                          ""b""
                      ]
                    }
                  }
                }";
            // Act & Assert
            VerifyPackageSpecWrite(json, expectedJson);
        }

        [Fact]
        public void Write_ReadWriteVersionsAreNormalized()
        {
            // Arrange
            var json = @"{
                    ""dependencies"": {
                        ""a"": {
                                ""version"": ""1.0.0"",
                        },
                    },
                  ""frameworks"": {
                    ""net46"": {
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""1.0.0"",
                            },
                        },
                        ""downloadDependencies"":  [
                            {""name"" : ""b"", ""version"" : ""[2.0.0]""},
                       ]
                    }
                  }
                }";

            var expectedJson = @"{
                  ""dependencies"": {
                    ""a"": ""[1.0.0, )""
                  },
                  ""frameworks"": {
                    ""net46"": {
                      ""dependencies"": {
                        ""a"": ""[1.0.0, )""
                      },
                      ""downloadDependencies"": [
                            {""name"" : ""b"", ""version"" : ""[2.0.0, 2.0.0]""},
                       ]
                    }
                  }
                }";
            // Act & Assert
            VerifyPackageSpecWrite(json, expectedJson);
        }

        [Fact]
        public void RoundTripFrameworkReferences()
        {
            // Arrange
            var json = @"{
                  ""frameworks"": {
                    ""net46"": {
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                                ""autoReferenced"": true
                            }
                        },
                        ""frameworkReferences"": [
                            ""Microsoft.WindowsDesktop.App|WinForms"",
                            ""Microsoft.WindowsDesktop.App|WPF""
                        ]
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        private static string GetJsonString(PackageSpec packageSpec)
        {
            var writer = new JsonObjectWriter();

            PackageSpecWriter.Write(packageSpec, writer);

            return writer.GetJson();
        }

        private static JObject GetJsonObject(PackageSpec packageSpec)
        {
            var writer = new JsonObjectWriter();

            PackageSpecWriter.Write(packageSpec, writer);

            return writer.GetJObject();
        }

        private static PackageSpec CreatePackageSpec(bool withRestoreSettings, WarningProperties warningProperties = null)
        {
            var unsortedArray = new[] { "b", "a", "c" };
            var unsortedReadOnlyList = new List<string>(unsortedArray).AsReadOnly();
            var libraryRange = new LibraryRange("library", new VersionRange(new NuGetVersion("1.2.3")), LibraryDependencyTarget.Package);
            var libraryRangeWithNoWarn = new LibraryRange("libraryWithNoWarn", new VersionRange(new NuGetVersion("1.2.3")), LibraryDependencyTarget.Package);
            var libraryRangeWithNoWarnGlobal = new LibraryRange("libraryRangeWithNoWarnGlobal", new VersionRange(new NuGetVersion("1.2.3")), LibraryDependencyTarget.Package);
            var libraryDependency = new LibraryDependency()
            {
                IncludeType = LibraryIncludeFlags.Build,
                LibraryRange = libraryRange
            };

            var libraryDependencyWithNoWarn = new LibraryDependency()
            {
                IncludeType = LibraryIncludeFlags.Build,
                LibraryRange = libraryRangeWithNoWarn,
                NoWarn = new List<NuGetLogCode> { NuGetLogCode.NU1500, NuGetLogCode.NU1601 }
            };

            var libraryDependencyWithNoWarnGlobal = new LibraryDependency()
            {
                IncludeType = LibraryIncludeFlags.Build,
                LibraryRange = libraryRangeWithNoWarnGlobal,
                NoWarn = new List<NuGetLogCode> { NuGetLogCode.NU1500, NuGetLogCode.NU1608 }
            };

            var nugetFramework = new NuGetFramework("frameworkIdentifier", new Version("1.2.3"), "frameworkProfile");
            var nugetFrameworkWithNoWarn = new NuGetFramework("frameworkIdentifierWithNoWarn", new Version("1.2.5"), "frameworkProfileWithNoWarn");

            var packageSpec = new PackageSpec()
            {
                Authors = unsortedArray,
                BuildOptions = new BuildOptions() { OutputName = "outputName" },
                ContentFiles = new List<string>(unsortedArray),
                Copyright = "copyright",
                Dependencies = new List<LibraryDependency>() { libraryDependency, libraryDependencyWithNoWarnGlobal },
                Description = "description",
                FilePath = "filePath",
                HasVersionSnapshot = true,
                IconUrl = "iconUrl",
                IsDefaultVersion = false,
                Language = "language",
                LicenseUrl = "licenseUrl",
                Name = "name",
                Owners = unsortedArray,
                PackOptions = new PackOptions()
                {
                    IncludeExcludeFiles = new IncludeExcludeFiles()
                    {
                        Exclude = unsortedReadOnlyList,
                        ExcludeFiles = unsortedReadOnlyList,
                        Include = unsortedReadOnlyList,
                        IncludeFiles = unsortedReadOnlyList
                    }
                },
                ProjectUrl = "projectUrl",
                ReleaseNotes = "releaseNotes",
                RequireLicenseAcceptance = true,
                RestoreMetadata = new ProjectRestoreMetadata()
                {
                    CrossTargeting = true,
                    FallbackFolders = unsortedReadOnlyList,
                    ConfigFilePaths = unsortedReadOnlyList,
                    LegacyPackagesDirectory = false,
                    OriginalTargetFrameworks = unsortedReadOnlyList,
                    OutputPath = "outputPath",
                    ProjectStyle = ProjectStyle.PackageReference,
                    PackagesPath = "packagesPath",
                    ProjectJsonPath = "projectJsonPath",
                    ProjectName = "projectName",
                    ProjectPath = "projectPath",
                    ProjectUniqueName = "projectUniqueName",
                    Sources = new List<PackageSource>()
                        {
                            new PackageSource("source", "name", isEnabled: true, isOfficial: false, isPersistable: true)
                        },
                    TargetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>()
                        {
                            new ProjectRestoreMetadataFrameworkInfo(nugetFramework)
                        }
                },
                Summary = "summary",
                Tags = unsortedArray,
                Title = "title",
                Version = new NuGetVersion("1.2.3")
            };

            if (withRestoreSettings)
            {
                packageSpec.RestoreSettings = new ProjectRestoreSettings() { HideWarningsAndErrors = true };
            }

            if (warningProperties != null)
            {
                packageSpec.RestoreMetadata.ProjectWideWarningProperties = warningProperties;
            }

            packageSpec.PackInclude.Add("b", "d");
            packageSpec.PackInclude.Add("a", "e");
            packageSpec.PackInclude.Add("c", "f");

            var runtimeDependencySet = new RuntimeDependencySet("id", new[]
            {
                new RuntimePackageDependency("id", new VersionRange(new NuGetVersion("1.2.3")))
            });
            var runtimes = new List<RuntimeDescription>()
            {
                new RuntimeDescription("runtimeIdentifier", unsortedArray, new [] { runtimeDependencySet })
            };
            var compatibilityProfiles = new List<CompatibilityProfile>()
            {
                new CompatibilityProfile("name", new[] { new FrameworkRuntimePair(nugetFramework, "runtimeIdentifier")})
            };

            packageSpec.RuntimeGraph = new RuntimeGraph(runtimes, compatibilityProfiles);

            packageSpec.Scripts.Add("b", unsortedArray);
            packageSpec.Scripts.Add("a", unsortedArray);
            packageSpec.Scripts.Add("c", unsortedArray);

            packageSpec.TargetFrameworks.Add(new TargetFrameworkInformation()
            {
                Dependencies = new List<LibraryDependency>(),
                FrameworkName = nugetFramework,
                Imports = new List<NuGetFramework>() { nugetFramework },
            });

            packageSpec.TargetFrameworks.Add(new TargetFrameworkInformation()
            {
                Dependencies = new List<LibraryDependency>() { libraryDependencyWithNoWarn },
                FrameworkName = nugetFrameworkWithNoWarn,
                Imports = new List<NuGetFramework>() { nugetFrameworkWithNoWarn },
                Warn = true
            });

            return packageSpec;
        }

        private static void VerifyJsonPackageSpecRoundTrip(string json)
        {
            // Arrange & Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "testName", @"C:\fake\path");

            var writer = new JsonObjectWriter();
            PackageSpecWriter.Write(spec, writer);

            var actualResult = writer.GetJson();

            var expected = JObject.Parse(json).ToString();

            // Assert
            Assert.Equal(expected, actualResult);
        }

        private static void VerifyPackageSpecWrite(string json, string expectedJson)
        {
            // Arrange & Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "testName", @"C:\fake\path");

            var writer = new JsonObjectWriter();
            PackageSpecWriter.Write(spec, writer);

            var actualResult = writer.GetJson();

            var expected = JObject.Parse(expectedJson).ToString();

            // Assert
            Assert.Equal(expected, actualResult);
        }
    }
}