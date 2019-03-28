// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;
using static NuGet.Test.Utility.TestPackagesCore;

namespace NuGet.ProjectModel.Test
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class LockFileFormatTests
    {
        // Verify the value of locked has no impact on the parsed lock file
        [Fact]
        public void LockFileFormat_LockedPropertyIsIgnored()
        {
            // Arrange
            var lockFileContentTrue = @"{
  ""locked"": true,
  ""version"": 1,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""dependencies"": {
          ""Frob"": ""[4.0.20, )""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""WFRsJnfRzXYIiDJRbTXGctncx6Hw1F/uS2c5a5CzUwHuA3D/CM152F2HjWt12dLgH0BOcGvcRjKl2AfJ6MnHVg=="",
      ""type"": ""package"",
      ""files"": [
        ""_rels/.rels"",
        ""System.Runtime.nuspec"",
        ""lib/DNXCore50/System.Runtime.dll"",
        ""lib/netcore50/System.Runtime.dll"",
        ""lib/net46/_._"",
        ""ref/dotnet/System.Runtime.dll"",
        ""runtimes/win8-aot/lib/netcore50/System.Runtime.dll"",
        ""ref/net46/_._"",
        ""package/services/metadata/core-properties/b7eb2b260f1846d69b1ccf1a4e614180.psmdcp"",
        ""[Content_Types].xml""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  }
}";

            var lockFileContentMissing = @"{
  ""version"": 1,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""dependencies"": {
          ""Frob"": ""[4.0.20, )""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""WFRsJnfRzXYIiDJRbTXGctncx6Hw1F/uS2c5a5CzUwHuA3D/CM152F2HjWt12dLgH0BOcGvcRjKl2AfJ6MnHVg=="",
      ""type"": ""package"",
      ""files"": [
        ""_rels/.rels"",
        ""System.Runtime.nuspec"",
        ""lib/DNXCore50/System.Runtime.dll"",
        ""lib/netcore50/System.Runtime.dll"",
        ""lib/net46/_._"",
        ""ref/dotnet/System.Runtime.dll"",
        ""runtimes/win8-aot/lib/netcore50/System.Runtime.dll"",
        ""ref/net46/_._"",
        ""package/services/metadata/core-properties/b7eb2b260f1846d69b1ccf1a4e614180.psmdcp"",
        ""[Content_Types].xml""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  }
}";

            var lockFileContentFalse = @"{
  ""locked"": false,
  ""version"": 1,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""dependencies"": {
          ""Frob"": ""[4.0.20, )""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""WFRsJnfRzXYIiDJRbTXGctncx6Hw1F/uS2c5a5CzUwHuA3D/CM152F2HjWt12dLgH0BOcGvcRjKl2AfJ6MnHVg=="",
      ""type"": ""package"",
      ""files"": [
        ""_rels/.rels"",
        ""System.Runtime.nuspec"",
        ""lib/DNXCore50/System.Runtime.dll"",
        ""lib/netcore50/System.Runtime.dll"",
        ""lib/net46/_._"",
        ""ref/dotnet/System.Runtime.dll"",
        ""runtimes/win8-aot/lib/netcore50/System.Runtime.dll"",
        ""ref/net46/_._"",
        ""package/services/metadata/core-properties/b7eb2b260f1846d69b1ccf1a4e614180.psmdcp"",
        ""[Content_Types].xml""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  }
}";

            var lockFileFormat = new LockFileFormat();

            // Act
            var lockFileTrue = lockFileFormat.Parse(lockFileContentTrue, "In Memory");
            var lockFileFalse = lockFileFormat.Parse(lockFileContentFalse, "In Memory");
            var lockFileMissing = lockFileFormat.Parse(lockFileContentMissing, "In Memory");

            var lockFileTrueString = lockFileFormat.Render(lockFileTrue);
            var lockFileFalseString = lockFileFormat.Render(lockFileFalse);
            var lockFileMissingString = lockFileFormat.Render(lockFileMissing);

            // Assert
            Assert.Equal(lockFileTrue, lockFileFalse);
            Assert.Equal(lockFileTrue, lockFileMissing);

            Assert.Equal(lockFileTrueString, lockFileFalseString);
            Assert.Equal(lockFileTrueString, lockFileMissingString);
        }

        [Fact]
        public void LockFileFormat_ReadsLockFileWithNoTools()
        {
            var lockFileContent = @"{
  ""version"": 1,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""dependencies"": {
          ""Frob"": ""[4.0.20, )""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""WFRsJnfRzXYIiDJRbTXGctncx6Hw1F/uS2c5a5CzUwHuA3D/CM152F2HjWt12dLgH0BOcGvcRjKl2AfJ6MnHVg=="",
      ""type"": ""package"",
      ""files"": [
        ""_rels/.rels"",
        ""System.Runtime.nuspec"",
        ""lib/DNXCore50/System.Runtime.dll"",
        ""lib/netcore50/System.Runtime.dll"",
        ""lib/net46/_._"",
        ""ref/dotnet/System.Runtime.dll"",
        ""runtimes/win8-aot/lib/netcore50/System.Runtime.dll"",
        ""ref/net46/_._"",
        ""package/services/metadata/core-properties/b7eb2b260f1846d69b1ccf1a4e614180.psmdcp"",
        ""[Content_Types].xml""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  }
}";
            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            Assert.Equal(1, lockFile.Version);

            var target = lockFile.Targets.Single();
            Assert.Equal(NuGetFramework.Parse("dotnet"), target.TargetFramework);

            var runtimeTargetLibrary = target.Libraries.Single();
            Assert.Equal("System.Runtime", runtimeTargetLibrary.Name);
            Assert.Equal(NuGetVersion.Parse("4.0.20-beta-22927"), runtimeTargetLibrary.Version);
            Assert.Equal(0, runtimeTargetLibrary.NativeLibraries.Count);
            Assert.Equal(0, runtimeTargetLibrary.ResourceAssemblies.Count);
            Assert.Equal(0, runtimeTargetLibrary.FrameworkAssemblies.Count);
            Assert.Equal(0, runtimeTargetLibrary.RuntimeAssemblies.Count);
            Assert.Equal("ref/dotnet/System.Runtime.dll", runtimeTargetLibrary.CompileTimeAssemblies.Single().Path);

            var dep = runtimeTargetLibrary.Dependencies.Single();
            Assert.Equal("Frob", dep.Id);
            Assert.Equal(new VersionRange(NuGetVersion.Parse("4.0.20")), dep.VersionRange);

            var runtimeLibrary = lockFile.Libraries.Single();
            Assert.Equal("System.Runtime", runtimeLibrary.Name);
            Assert.Equal(NuGetVersion.Parse("4.0.20-beta-22927"), runtimeLibrary.Version);
            Assert.False(string.IsNullOrEmpty(runtimeLibrary.Sha512));
            Assert.Equal(LibraryType.Package, runtimeLibrary.Type);
            Assert.Equal(10, runtimeLibrary.Files.Count);

            var emptyDepGroup = lockFile.ProjectFileDependencyGroups.First();
            Assert.True(string.IsNullOrEmpty(emptyDepGroup.FrameworkName));
            Assert.Equal("System.Runtime [4.0.10-beta-*, )", emptyDepGroup.Dependencies.Single());
            var netPlatDepGroup = lockFile.ProjectFileDependencyGroups.Last();
            Assert.Equal(NuGetFramework.Parse("dotnet").DotNetFrameworkName, netPlatDepGroup.FrameworkName);
            Assert.Empty(netPlatDepGroup.Dependencies);
        }

        [Fact]
        public void LockFileFormat_WritesLockFile()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 2,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  }
}";
            var lockFile = new LockFile()
            {
                Version = 2
            };

            var target = new LockFileTarget()
            {
                TargetFramework = FrameworkConstants.CommonFrameworks.DotNet
            };

            var targetLib = new LockFileTargetLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package
            };

            targetLib.Dependencies.Add(new NuGet.Packaging.Core.PackageDependency("Frob",
                new VersionRange(NuGetVersion.Parse("4.0.20"))));
            targetLib.CompileTimeAssemblies.Add(new LockFileItem("ref/dotnet/System.Runtime.dll"));
            target.Libraries.Add(targetLib);
            lockFile.Targets.Add(target);

            var lib = new LockFileLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package,
                Sha512 = "sup3rs3cur3"
            };
            lib.Files.Add("System.Runtime.nuspec");
            lockFile.Libraries.Add(lib);

            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup("", new string[] { "System.Runtime [4.0.10-beta-*, )" }));
            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup(FrameworkConstants.CommonFrameworks.DotNet.DotNetFrameworkName, Array.Empty<string>()));

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile));
            var expected = JObject.Parse(lockFileContent);

            // Assert
            Assert.Equal(expected.ToString(), output.ToString());
        }

        [Fact]
        public void LockFileFormat_WritesPackageSpec()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 2,
  ""targets"": {},
  ""libraries"": {},
  ""projectFileDependencyGroups"": {},
  ""project"": {
    ""frameworks"": {
      ""dotnet"": {}
    }
  }
}";
            var lockFile = new LockFile()
            {
                Version = 2,

                PackageSpec = new PackageSpec(new[]
                {
                    new TargetFrameworkInformation
                    {
                        FrameworkName = FrameworkConstants.CommonFrameworks.DotNet
                    }
                })
            };

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile));
            var expected = JObject.Parse(lockFileContent);

            // Assert
            Assert.Equal(expected.ToString(), output.ToString());
        }

        [Fact]
        public void LockFileFormat_ReadsPackageSpec()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 2,
  ""targets"": {},
  ""libraries"": {},
  ""projectFileDependencyGroups"": {},
  ""project"":   {
    ""version"": ""1.0.0"",
    ""restore"": {
      ""projectUniqueName"": ""X:\\ProjectPath\\ProjectPath.csproj"",
      ""projectName"": ""ProjectPath"",
      ""projectPath"": ""X:\\ProjectPath\\ProjectPath.csproj"",
      ""outputPath"": ""X:\\ProjectPath\\obj\\"",
      ""projectStyle"": ""PackageReference"",
      ""originalTargetFrameworks"": [
        ""netcoreapp1.0""
      ],
      ""frameworks"": {
        ""netcoreapp1.0"": {
          ""projectReferences"": {}
        }
      }
    },
    ""frameworks"": {
      ""netcoreapp1.0"": {
        ""dependencies"": {
         ""Microsoft.NET.Sdk"": {
                ""suppressParent"": ""All"",
                ""target"": ""Package"",
                ""version"": ""[1.0.0-alpha-20161104-2, )""
          },
          ""Microsoft.NETCore.App"": {
            ""target"": ""Package"",
            ""version"": ""[1.0.1, )""
          }
        }
      }
    }
  }
}";
            var lockFile = new LockFile()
            {
                Version = 2,

                PackageSpec = new PackageSpec(new[]
                {
                    new TargetFrameworkInformation
                    {
                        FrameworkName = FrameworkConstants.CommonFrameworks.NetCoreApp10,
                        Dependencies = new[]
                        {
                            new LibraryDependency
                            {
                                LibraryRange = new LibraryRange(
                                    "Microsoft.NETCore.App",
                                    new VersionRange(
                                        minVersion: new NuGetVersion("1.0.1"),
                                        originalString: "1.0.1"),
                                    LibraryDependencyTarget.Package)
                            },
                            new LibraryDependency
                            {
                                LibraryRange = new LibraryRange(
                                    "Microsoft.NET.Sdk",
                                    new VersionRange(
                                        minVersion: new NuGetVersion("1.0.0-alpha-20161104-2"),
                                        originalString: "1.0.0-alpha-20161104-2"),
                                    LibraryDependencyTarget.Package),
                                SuppressParent = LibraryIncludeFlags.All
                            }
                        }
                    }
                })
                {
                    Version = new NuGetVersion("1.0.0"),
                    RestoreMetadata = new ProjectRestoreMetadata
                    {
                        ProjectUniqueName = @"X:\ProjectPath\ProjectPath.csproj",
                        ProjectName = "ProjectPath",
                        ProjectPath = @"X:\ProjectPath\ProjectPath.csproj",
                        OutputPath = @"X:\ProjectPath\obj\",
                        ProjectStyle = ProjectStyle.PackageReference,
                        OriginalTargetFrameworks = new[] { "netcoreapp1.0" },
                        TargetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>
                        {
                            new ProjectRestoreMetadataFrameworkInfo(NuGetFramework.Parse("netcoreapp1.0"))
                        }
                    }
                }
            };

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile));
            var expected = JObject.Parse(lockFileContent);

            // Assert
            Assert.Equal(expected.ToString(), output.ToString());
        }

        [Fact]
        public void LockFileFormat_WritesMinimalErrorMessage()
        {

            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""message"": ""test log message""
    }
  ]
}";
            var lockFile = new LockFile()
            {
                Version = 3
            };

            var target = new LockFileTarget()
            {
                TargetFramework = FrameworkConstants.CommonFrameworks.DotNet
            };

            var targetLib = new LockFileTargetLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package
            };

            targetLib.Dependencies.Add(new Packaging.Core.PackageDependency("Frob",
                new VersionRange(NuGetVersion.Parse("4.0.20"))));
            targetLib.CompileTimeAssemblies.Add(new LockFileItem("ref/dotnet/System.Runtime.dll"));
            target.Libraries.Add(targetLib);
            lockFile.Targets.Add(target);

            var lib = new LockFileLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package,
                Sha512 = "sup3rs3cur3"
            };
            lib.Files.Add("System.Runtime.nuspec");
            lockFile.Libraries.Add(lib);

            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup("", new string[] { "System.Runtime [4.0.10-beta-*, )" }));
            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup(FrameworkConstants.CommonFrameworks.DotNet.DotNetFrameworkName, Array.Empty<string>()));

            lockFile.LogMessages = new List<IAssetsLogMessage>
            {
                new AssetsLogMessage(LogLevel.Error, NuGetLogCode.NU1000, "test log message")
            };

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile)).ToString();
            var expected = JObject.Parse(lockFileContent).ToString();

            // Assert
            Assert.Equal(expected, output);
        }

        [Fact]
        public void LockFileFormat_WritesMultipleErrorMessages()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""message"": ""test log message""
    },
    {
      ""code"": ""NU1000"",
      ""level"": ""Warning"",
      ""warningLevel"": 1,
      ""message"": ""test log message""
    },
    {
      ""code"": ""NU1000"",
      ""level"": ""Warning"",
      ""warningLevel"": 2,
      ""message"": ""test log message""
    },
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""message"": ""test log message""
    }
  ]
}";
            var lockFile = new LockFile()
            {
                Version = 3
            };

            var target = new LockFileTarget()
            {
                TargetFramework = FrameworkConstants.CommonFrameworks.DotNet
            };

            var targetLib = new LockFileTargetLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package
            };

            targetLib.Dependencies.Add(new Packaging.Core.PackageDependency("Frob",
                new VersionRange(NuGetVersion.Parse("4.0.20"))));
            targetLib.CompileTimeAssemblies.Add(new LockFileItem("ref/dotnet/System.Runtime.dll"));
            target.Libraries.Add(targetLib);
            lockFile.Targets.Add(target);

            var lib = new LockFileLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package,
                Sha512 = "sup3rs3cur3"
            };
            lib.Files.Add("System.Runtime.nuspec");
            lockFile.Libraries.Add(lib);

            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup("", new string[] { "System.Runtime [4.0.10-beta-*, )" }));
            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup(FrameworkConstants.CommonFrameworks.DotNet.DotNetFrameworkName, Array.Empty<string>()));

            lockFile.LogMessages = new List<IAssetsLogMessage>
            {
                new AssetsLogMessage(LogLevel.Error, NuGetLogCode.NU1000, "test log message"),
                new AssetsLogMessage(LogLevel.Warning, NuGetLogCode.NU1000, "test log message")
                {
                    WarningLevel = WarningLevel.Severe
                },
                new AssetsLogMessage(LogLevel.Warning, NuGetLogCode.NU1000, "test log message")
                {
                    WarningLevel = WarningLevel.Important
                },
                new AssetsLogMessage(LogLevel.Error, NuGetLogCode.NU1000, "test log message")
            };

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile)).ToString();
            var expected = JObject.Parse(lockFileContent).ToString();

            // Assert
            Assert.Equal(expected, output);
        }


        [Fact]
        public void LockFileFormat_WritesFullErrorMessage()
        {

            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""filePath"": ""kung\\fu\\fighting.targets"",
      ""startLineNumber"": 11,
      ""startColumnNumber"": 2,
      ""endLineNumber"": 11,
      ""endColumnNumber"": 10,
      ""message"": ""test log message"",
      ""libraryId"": ""nuget.versioning"",
      ""targetGraphs"": [
        ""net46"",
        ""netcoreapp1.0"",
        ""netstandard1.6""
      ]
    }
  ]
}";
            var lockFile = new LockFile()
            {
                Version = 3
            };

            var target = new LockFileTarget()
            {
                TargetFramework = FrameworkConstants.CommonFrameworks.DotNet
            };

            var targetLib = new LockFileTargetLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package
            };

            targetLib.Dependencies.Add(new Packaging.Core.PackageDependency("Frob",
                new VersionRange(NuGetVersion.Parse("4.0.20"))));
            targetLib.CompileTimeAssemblies.Add(new LockFileItem("ref/dotnet/System.Runtime.dll"));
            target.Libraries.Add(targetLib);
            lockFile.Targets.Add(target);

            var lib = new LockFileLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package,
                Sha512 = "sup3rs3cur3"
            };
            lib.Files.Add("System.Runtime.nuspec");
            lockFile.Libraries.Add(lib);

            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup("", new string[] { "System.Runtime [4.0.10-beta-*, )" }));
            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup(FrameworkConstants.CommonFrameworks.DotNet.DotNetFrameworkName, Array.Empty<string>()));

            lockFile.LogMessages = new List<IAssetsLogMessage>
            {
                new AssetsLogMessage(LogLevel.Error, NuGetLogCode.NU1000, "test log message")
                {
                    FilePath = @"kung\fu\fighting.targets",
                    ProjectPath = @"kung\fu\fighting.csproj",
                    TargetGraphs = new List<string>{ "net46", "netcoreapp1.0", "netstandard1.6" },
                    StartLineNumber = 11,
                    StartColumnNumber = 2,
                    EndLineNumber = 11,
                    EndColumnNumber = 10,
                    LibraryId = "nuget.versioning"
                }
            };

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile)).ToString();
            var expected = JObject.Parse(lockFileContent).ToString();

            // Assert
            Assert.Equal(expected, output);
        }

        [Fact]
        public void LockFileFormat_WritesErrorMessageWithFilePathSameAsProjectPath()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""project"": {
    ""restore"": {
      ""projectPath"": ""kung\\fu\\fighting.csproj""
    }
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""startLineNumber"": 11,
      ""startColumnNumber"": 2,
      ""endLineNumber"": 11,
      ""endColumnNumber"": 10,
      ""message"": ""test log message"",
      ""libraryId"": ""nuget.versioning"",
      ""targetGraphs"": [
        ""net46"",
        ""netcoreapp1.0"",
        ""netstandard1.6""
      ]
    }
  ]
}";
            var lockFile = new LockFile()
            {
                Version = 3
            };

            var target = new LockFileTarget()
            {
                TargetFramework = FrameworkConstants.CommonFrameworks.DotNet
            };

            var targetLib = new LockFileTargetLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package
            };

            targetLib.Dependencies.Add(new Packaging.Core.PackageDependency("Frob",
                new VersionRange(NuGetVersion.Parse("4.0.20"))));
            targetLib.CompileTimeAssemblies.Add(new LockFileItem("ref/dotnet/System.Runtime.dll"));
            target.Libraries.Add(targetLib);
            lockFile.Targets.Add(target);

            var lib = new LockFileLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package,
                Sha512 = "sup3rs3cur3"
            };
            lib.Files.Add("System.Runtime.nuspec");
            lockFile.Libraries.Add(lib);

            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup("", new string[] { "System.Runtime [4.0.10-beta-*, )" }));
            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup(FrameworkConstants.CommonFrameworks.DotNet.DotNetFrameworkName, Array.Empty<string>()));
            lockFile.PackageSpec = new PackageSpec(new List<TargetFrameworkInformation>())
            {
                RestoreMetadata = new ProjectRestoreMetadata()
                {
                    ProjectPath = "kung\\fu\\fighting.csproj"
                }
            };

            lockFile.LogMessages = new List<IAssetsLogMessage>
            {
                new AssetsLogMessage(LogLevel.Error, NuGetLogCode.NU1000, "test log message")
                {
                    FilePath = @"kung\fu\fighting.csproj",
                    ProjectPath = @"kung\fu\fighting.csproj",
                    TargetGraphs = new List<string>{ "net46", "netcoreapp1.0", "netstandard1.6" },
                    StartLineNumber = 11,
                    StartColumnNumber = 2,
                    EndLineNumber = 11,
                    EndColumnNumber = 10,
                    LibraryId = "nuget.versioning"
                }
            };

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile)).ToString();
            var expected = JObject.Parse(lockFileContent).ToString();

            // Assert
            Assert.Equal(expected, output);
        }

        [Fact]
        public void LockFileFormat_WritesMinimalWarningMessageWithWarningLevel()
        {

            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Warning"",
      ""warningLevel"": 2,
      ""message"": ""test log message""
    }
  ]
}";
            var lockFile = new LockFile()
            {
                Version = 3
            };

            var target = new LockFileTarget()
            {
                TargetFramework = FrameworkConstants.CommonFrameworks.DotNet
            };

            var targetLib = new LockFileTargetLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package
            };

            targetLib.Dependencies.Add(new Packaging.Core.PackageDependency("Frob",
                new VersionRange(NuGetVersion.Parse("4.0.20"))));
            targetLib.CompileTimeAssemblies.Add(new LockFileItem("ref/dotnet/System.Runtime.dll"));
            target.Libraries.Add(targetLib);
            lockFile.Targets.Add(target);

            var lib = new LockFileLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package,
                Sha512 = "sup3rs3cur3"
            };
            lib.Files.Add("System.Runtime.nuspec");
            lockFile.Libraries.Add(lib);

            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup("", new string[] { "System.Runtime [4.0.10-beta-*, )" }));
            lockFile.ProjectFileDependencyGroups.Add(
                new ProjectFileDependencyGroup(FrameworkConstants.CommonFrameworks.DotNet.DotNetFrameworkName, Array.Empty<string>()));

            lockFile.LogMessages = new List<IAssetsLogMessage>
            {
                new AssetsLogMessage(LogLevel.Warning, NuGetLogCode.NU1000, "test log message")
                {
                    WarningLevel = WarningLevel.Important
                }
            };

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile)).ToString();
            var expected = JObject.Parse(lockFileContent).ToString();

            // Assert
            Assert.Equal(expected, output);
        }

        [Fact]
        public void LockFileFormat_ReadsMinimalErrorMessage()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""message"": ""test log message""
    }
  ]
}";
            LockFile lockFileObj = null;
            IAssetsLogMessage logMessage = null;
            using (var lockFile = new TempFile())
            {

                File.WriteAllText(lockFile, lockFileContent);

                // Act
                var reader = new LockFileFormat();
                lockFileObj = reader.Read(lockFile);
                logMessage = lockFileObj?.LogMessages?.First();
            }


            // Assert
            Assert.NotNull(lockFileObj);
            Assert.NotNull(logMessage);
            Assert.Equal(1, lockFileObj.LogMessages.Count());
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Equal(NuGetLogCode.NU1000, logMessage.Code);
            Assert.Null(logMessage.FilePath);
            Assert.Equal(0, logMessage.StartLineNumber);
            Assert.Equal(0, logMessage.EndLineNumber);
            Assert.Equal(0, logMessage.StartColumnNumber);
            Assert.Equal(0, logMessage.EndColumnNumber);
            Assert.NotNull(logMessage.TargetGraphs);
            Assert.Equal(0, logMessage.TargetGraphs.Count);
            Assert.Equal("test log message", logMessage.Message);
        }

        [Fact]
        public void LockFileFormat_ReadsFullErrorMessage()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""filePath"": ""kung\\fu\\fighting.targets"",
      ""startLineNumber"": 11,
      ""startColumnNumber"": 2,
      ""endLineNumber"": 11,
      ""endColumnNumber"": 10,
      ""message"": ""test log message"",
      ""targetGraphs"": [
        ""net46"",
        ""netcoreapp1.0"",
        ""netstandard1.6""
      ]
    }
  ]
}";
            LockFile lockFileObj = null;
            IAssetsLogMessage logMessage = null;
            using (var lockFile = new TempFile())
            {

                File.WriteAllText(lockFile, lockFileContent);

                // Act
                var reader = new LockFileFormat();
                lockFileObj = reader.Read(lockFile);
                logMessage = lockFileObj?.LogMessages?.First();
            }


            // Assert
            Assert.NotNull(lockFileObj);
            Assert.NotNull(logMessage);
            Assert.Equal(1, lockFileObj.LogMessages.Count());
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Equal(NuGetLogCode.NU1000, logMessage.Code);
            Assert.Equal("kung\\fu\\fighting.targets", logMessage.FilePath);
            Assert.Equal(11, logMessage.StartLineNumber);
            Assert.Equal(11, logMessage.EndLineNumber);
            Assert.Equal(2, logMessage.StartColumnNumber);
            Assert.Equal(10, logMessage.EndColumnNumber);
            Assert.NotNull(logMessage.TargetGraphs);
            Assert.Equal(3, logMessage.TargetGraphs.Count);
            Assert.Equal("test log message", logMessage.Message);
        }

        [Fact]
        public void LockFileFormat_SafeRead()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Warning"",
      ""warningLevel"": 2,
      ""filePath"": ""kung\\fu\\fighting.targets"",
      ""startLineNumber"": 11,
      ""startColumnNumber"": 2,
      ""endLineNumber"": 11,
      ""endColumnNumber"": 10,
      ""message"": ""test log message"",
      ""targetGraphs"": [
        ""net46"",
        ""netcoreapp1.0"",
        ""netstandard1.6""
      ]
    }
  ]
}";
            LockFile lockFileObj = null;
            IAssetsLogMessage logMessage = null;
            using (var lockFile = new TempFile())
            {

                File.WriteAllText(lockFile, lockFileContent);

                // Act
                var reader = new LockFileFormat();
                lockFileObj = FileUtility.SafeRead(lockFile, (stream,path) => reader.Read(stream, NullLogger.Instance, path));
                logMessage = lockFileObj?.LogMessages?.First();
            }


            // Assert
            Assert.NotNull(lockFileObj);
            Assert.NotNull(logMessage);
            Assert.Equal(1, lockFileObj.LogMessages.Count());
            Assert.Equal(LogLevel.Warning, logMessage.Level);
            Assert.Equal(WarningLevel.Important, logMessage.WarningLevel);
            Assert.Equal(NuGetLogCode.NU1000, logMessage.Code);
            Assert.Equal("kung\\fu\\fighting.targets", logMessage.FilePath);
            Assert.Equal(11, logMessage.StartLineNumber);
            Assert.Equal(11, logMessage.EndLineNumber);
            Assert.Equal(2, logMessage.StartColumnNumber);
            Assert.Equal(10, logMessage.EndColumnNumber);
            Assert.NotNull(logMessage.TargetGraphs);
            Assert.Equal(3, logMessage.TargetGraphs.Count);
            Assert.Equal("test log message", logMessage.Message);
        }


        [Fact]
        public void LockFileFormat_ReadsWarningMessage()
        {

            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Warning"",
      ""warningLevel"": 2,
      ""filePath"": ""kung\\fu\\fighting.targets"",
      ""startLineNumber"": 11,
      ""startColumnNumber"": 2,
      ""endLineNumber"": 11,
      ""endColumnNumber"": 10,
      ""message"": ""test log message"",
      ""targetGraphs"": [
        ""net46"",
        ""netcoreapp1.0"",
        ""netstandard1.6""
      ]
    }
  ]
}";
            LockFile lockFileObj = null;
            IAssetsLogMessage logMessage = null;
            using (var lockFile = new TempFile())
            {

                File.WriteAllText(lockFile, lockFileContent);

                // Act
                var reader = new LockFileFormat();
                lockFileObj = reader.Read(lockFile);
                logMessage = lockFileObj?.LogMessages?.First();
            }


            // Assert
            Assert.NotNull(lockFileObj);
            Assert.NotNull(logMessage);
            Assert.Equal(1, lockFileObj.LogMessages.Count());
            Assert.Equal(LogLevel.Warning, logMessage.Level);
            Assert.Equal(WarningLevel.Important, logMessage.WarningLevel);
            Assert.Equal(NuGetLogCode.NU1000, logMessage.Code);
            Assert.Equal("kung\\fu\\fighting.targets", logMessage.FilePath);
            Assert.Equal(11, logMessage.StartLineNumber);
            Assert.Equal(11, logMessage.EndLineNumber);
            Assert.Equal(2, logMessage.StartColumnNumber);
            Assert.Equal(10, logMessage.EndColumnNumber);
            Assert.NotNull(logMessage.TargetGraphs);
            Assert.Equal(3, logMessage.TargetGraphs.Count);
            Assert.Equal("test log message", logMessage.Message);
        }

        [Fact]
        public void LockFileFormat_ReadsWarningMessageWithoutWarningLevel()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Warning"",
      ""filePath"": ""kung\\fu\\fighting.targets"",
      ""startLineNumber"": 11,
      ""startColumnNumber"": 2,
      ""endLineNumber"": 11,
      ""endColumnNumber"": 10,
      ""message"": ""test log message"",
      ""targetGraphs"": [
        ""net46"",
        ""netcoreapp1.0"",
        ""netstandard1.6""
      ]
    }
  ]
}";
            LockFile lockFileObj = null;
            IAssetsLogMessage logMessage = null;
            using (var lockFile = new TempFile())
            {

                File.WriteAllText(lockFile, lockFileContent);

                // Act
                var reader = new LockFileFormat();
                lockFileObj = reader.Read(lockFile);
                logMessage = lockFileObj?.LogMessages?.First();
            }


            // Assert
            Assert.NotNull(lockFileObj);
            Assert.NotNull(logMessage);
            Assert.Equal(1, lockFileObj.LogMessages.Count());
            Assert.Equal(LogLevel.Warning, logMessage.Level);
            Assert.Equal(WarningLevel.Severe, logMessage.WarningLevel);
            Assert.Equal(NuGetLogCode.NU1000, logMessage.Code);
            Assert.Equal("kung\\fu\\fighting.targets", logMessage.FilePath);
            Assert.Equal(11, logMessage.StartLineNumber);
            Assert.Equal(11, logMessage.EndLineNumber);
            Assert.Equal(2, logMessage.StartColumnNumber);
            Assert.Equal(10, logMessage.EndColumnNumber);
            Assert.NotNull(logMessage.TargetGraphs);
            Assert.Equal(3, logMessage.TargetGraphs.Count);
            Assert.Equal("test log message", logMessage.Message);
        }


        [Fact]
        public void LockFileFormat_ReadsMultipleMessages()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""message"": ""test log message""
    },
    {
      ""code"": ""NU1500"",
      ""level"": ""Warning"",
      ""warningLevel"": 1,
      ""message"": ""test warning message""
    },
    {
      ""code"": ""NU1500"",
      ""level"": ""Warning"",
      ""warningLevel"": 1,
      ""message"": ""test warning message""
    },
    {
      ""code"": ""NU1001"",
      ""level"": ""Error"",
      ""message"": ""test error message with type NU1001""
    },
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""message"": ""test log message""
    }
  ]
}";
            LockFile lockFileObj = null;
            using (var lockFile = new TempFile())
            {

                File.WriteAllText(lockFile, lockFileContent);

                // Act
                var reader = new LockFileFormat();
                lockFileObj = reader.Read(lockFile);
            }


            // Assert
            Assert.NotNull(lockFileObj);
            Assert.Equal(5, lockFileObj.LogMessages.Count());
            Assert.Equal(3, lockFileObj.LogMessages.Where(m => m.Level == LogLevel.Error).Count());
            Assert.Equal(2, lockFileObj.LogMessages.Where(m => m.Level == LogLevel.Warning).Count());
            Assert.Equal(2, lockFileObj.LogMessages.Where(m => m.Message == "test log message").Count());
            Assert.Equal(2, lockFileObj.LogMessages.Where(m => m.Message == "test warning message").Count());
            Assert.Equal(1, lockFileObj.LogMessages.Where(m => m.Message == "test error message with type NU1001").Count());
            Assert.Equal(2, lockFileObj.LogMessages.Where(m => m.Code == NuGetLogCode.NU1000).Count());
            Assert.Equal(2, lockFileObj.LogMessages.Where(m => m.Code == NuGetLogCode.NU1500).Count());
            Assert.Equal(1, lockFileObj.LogMessages.Where(m => m.Code == NuGetLogCode.NU1001).Count());
        }

        [Fact]
        public void LockFileFormat_ReadsLogMessageWithSameFilePathAndProjectPath()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""project"": {
    ""restore"": {
      ""projectPath"": ""kung\\fu\\fighting.csproj""
    }
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""message"": ""test log message"",
      ""filePath"": ""kung\\fu\\fighting.csproj""
    }
  ]
}";
            LockFile lockFileObj = null;
            IAssetsLogMessage logMessage = null;
            using (var lockFile = new TempFile())
            {

                File.WriteAllText(lockFile, lockFileContent);

                // Act
                var reader = new LockFileFormat();
                lockFileObj = reader.Read(lockFile);
                logMessage = lockFileObj?.LogMessages?.First();
            }


            // Assert
            Assert.NotNull(lockFileObj);
            Assert.NotNull(logMessage);
            Assert.Equal(1, lockFileObj.LogMessages.Count());
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Equal(NuGetLogCode.NU1000, logMessage.Code);
            Assert.NotNull(logMessage.FilePath);
            Assert.Equal("kung\\fu\\fighting.csproj", logMessage.FilePath);
            Assert.Equal(0, logMessage.StartLineNumber);
            Assert.Equal(0, logMessage.EndLineNumber);
            Assert.Equal(0, logMessage.StartColumnNumber);
            Assert.Equal(0, logMessage.EndColumnNumber);
            Assert.NotNull(logMessage.TargetGraphs);
            Assert.Equal(0, logMessage.TargetGraphs.Count);
            Assert.Equal("test log message", logMessage.Message);
        }

        [Fact]
        public void LockFileFormat_ReadsLogMessageWithNoFilePath()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.20-beta-22927"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Frob"": ""4.0.20""
        },
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""System.Runtime.nuspec""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime [4.0.10-beta-*, )""
    ],
    "".NETPlatform,Version=v5.0"": []
  },
  ""project"": {
    ""restore"": {
      ""projectPath"": ""kung\\fu\\fighting.csproj""
    }
  },
  ""logs"": [
    {
      ""code"": ""NU1000"",
      ""level"": ""Error"",
      ""message"": ""test log message""
    }
  ]
}";
            LockFile lockFileObj = null;
            IAssetsLogMessage logMessage = null;
            using (var lockFile = new TempFile())
            {

                File.WriteAllText(lockFile, lockFileContent);

                // Act
                var reader = new LockFileFormat();
                lockFileObj = reader.Read(lockFile);
                logMessage = lockFileObj?.LogMessages?.First();
            }


            // Assert
            Assert.NotNull(lockFileObj);
            Assert.NotNull(logMessage);
            Assert.Equal(1, lockFileObj.LogMessages.Count());
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.Equal(NuGetLogCode.NU1000, logMessage.Code);
            Assert.NotNull(logMessage.FilePath);
            Assert.Equal("kung\\fu\\fighting.csproj", logMessage.FilePath);
            Assert.Equal(0, logMessage.StartLineNumber);
            Assert.Equal(0, logMessage.EndLineNumber);
            Assert.Equal(0, logMessage.StartColumnNumber);
            Assert.Equal(0, logMessage.EndColumnNumber);
            Assert.NotNull(logMessage.TargetGraphs);
            Assert.Equal(0, logMessage.TargetGraphs.Count);
            Assert.Equal("test log message", logMessage.Message);
        }

        [Fact]
        public void LockFileFormat_ReadsLockFileWithTools()
        {
            var lockFileContent = @"{
              ""version"": 1,
              ""targets"": {
                "".NETPlatform,Version=v5.0"": {
                  ""GlobalTool/1.0.0"": {
                    ""dependencies"": {
                    },
                    ""tools"": {
                      ""tools/dotnet/any/test.dll"": {}
                    }
                  }
                }
              },
              ""libraries"": {
                ""GlobalTool/1.0.0"": {
                  ""sha512"": ""WFRsJnfRzXYIiDJRbTXGctncx6Hw1F/uS2c5a5CzUwHuA3D/CM152F2HjWt12dLgH0BOcGvcRjKl2AfJ6MnHVg=="",
                  ""type"": ""package"",
                  ""files"": [
                    ""_rels/.rels"",
                    ""GlobalTool.nuspec"",
                    ""tools/dotnet/any/test.dll"",
                    ""package/services/metadata/core-properties/b7eb2b260f1846d69b1ccf1a4e614180.psmdcp"",
                    ""[Content_Types].xml""
                  ]
                }
              },
              ""projectFileDependencyGroups"": {
                """": [
                  ""GlobalTool [1.0.0, )""
                ],
                "".NETPlatform,Version=v5.0"": []
              }
            }";
            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            Assert.Equal(1, lockFile.Version);

            var target = lockFile.Targets.Single();
            Assert.Equal(NuGetFramework.Parse("dotnet"), target.TargetFramework);

            var runtimeTargetLibrary = target.Libraries.Single();
            Assert.Equal("GlobalTool", runtimeTargetLibrary.Name);
            Assert.Equal(NuGetVersion.Parse("1.0.0"), runtimeTargetLibrary.Version);
            Assert.Equal(0, runtimeTargetLibrary.NativeLibraries.Count);
            Assert.Equal(0, runtimeTargetLibrary.ResourceAssemblies.Count);
            Assert.Equal(0, runtimeTargetLibrary.FrameworkAssemblies.Count);
            Assert.Equal(0, runtimeTargetLibrary.RuntimeAssemblies.Count);
            Assert.Equal(1, runtimeTargetLibrary.ToolsAssemblies.Count);
            Assert.Equal("tools/dotnet/any/test.dll", runtimeTargetLibrary.ToolsAssemblies.Single().Path);
            Assert.Equal(0, runtimeTargetLibrary.Dependencies.Count());

            var runtimeLibrary = lockFile.Libraries.Single();
            Assert.Equal("GlobalTool", runtimeLibrary.Name);
            Assert.Equal(NuGetVersion.Parse("1.0.0"), runtimeLibrary.Version);
            Assert.False(string.IsNullOrEmpty(runtimeLibrary.Sha512));
            Assert.Equal(LibraryType.Package, runtimeLibrary.Type);
            Assert.Equal(5, runtimeLibrary.Files.Count);

            var emptyDepGroup = lockFile.ProjectFileDependencyGroups.First();
            Assert.True(string.IsNullOrEmpty(emptyDepGroup.FrameworkName));
            Assert.Equal("GlobalTool [1.0.0, )", emptyDepGroup.Dependencies.Single());
            var netPlatDepGroup = lockFile.ProjectFileDependencyGroups.Last();
            Assert.Equal(NuGetFramework.Parse("dotnet").DotNetFrameworkName, netPlatDepGroup.FrameworkName);
            Assert.Empty(netPlatDepGroup.Dependencies);
        }

        [Fact]
        public void LockFileFormat_ReadsLockFileWithEmbedAssemblies()
        {
            var lockFileContent = @"{
              ""version"": 1,
              ""targets"": {
                "".NETCoreApp,Version=v2.1"": {
                  ""packageA.interop/1.0.0"": {
                    ""compile"": {
                      ""lib/netstandard2.0/packageA.interop.dll"": {}
                    },
                    ""embed"": {
                      ""embed/netstandard2.0/packageA.interop.dll"": {}
                    },
                    ""runtime"": {
                      ""lib/netstandard2.0/packageA.interop.dll"": {}
                    }
                  }
                }
              },
              ""libraries"": {
                ""packageA.interop/1.0.0"": {
                  ""sha512"": ""WFRsJnfRzXYIiDJRbTXGctncx6Hw1F/uS2c5a5CzUwHuA3D/CM152F2HjWt12dLgH0BOcGvcRjKl2AfJ6MnHVg=="",
                  ""type"": ""package"",
                  ""files"": [
                    ""_rels/.rels"",
                    ""packageA.interop.nuspec"",
                    ""lib/netstandard2.0/packageA.interop.dll"",
                    ""embed/netstandard2.0/packageA.interop.dll"",
                    ""package/services/metadata/core-properties/b7eb2b260f1846d69b1ccf1a4e614180.psmdcp"",
                    ""[Content_Types].xml""
                  ]
                }
              },
              ""projectFileDependencyGroups"": {
                """": [
                  ""packageA.interop [1.0.0, )""
                ],
                "".NETCoreApp,Version=v2.1"": []
              }
            }";

            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            Assert.Equal(1, lockFile.Version);

            var target = lockFile.Targets.Single();

            var targetLibrary = target.Libraries.Single();
            Assert.Equal("packageA.interop", targetLibrary.Name);
            Assert.Equal(NuGetVersion.Parse("1.0.0"), targetLibrary.Version);
            Assert.Equal("lib/netstandard2.0/packageA.interop.dll", targetLibrary.CompileTimeAssemblies.Single().Path);
            Assert.Equal("lib/netstandard2.0/packageA.interop.dll", targetLibrary.RuntimeAssemblies.Single().Path);
            Assert.Equal("embed/netstandard2.0/packageA.interop.dll", targetLibrary.EmbedAssemblies.Single().Path);
        }

        [Fact]
        public void LockFileFormat_WritesFrameworkReference()
        {

            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
                "".NETCoreApp,Version=v3.0"": {
                    ""My.Nice.Package.With.WPF.Reference/2.0.0"": {
                        ""type"": ""package"",
        ""compile"": {
                            ""lib/netcoreapp3.0/a.dll"": { }
                        },
        ""frameworkReferences"": [
          ""Microsoft.Windows.Desktop|WPF"",
          ""Microsoft.Windows.Desktop|WindowsForms""
        ]
    }
}
  },
  ""libraries"": {
    ""My.Nice.Package.With.WPF.Reference/2.0.0"": {
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""files"": [
        ""My.Nice.Package.With.WPF.Reference.nuspec"",
        ""lib/netcoreapp3.0/a.dll""
      ]
    }
  },
  ""projectFileDependencyGroups"": { }
}";
            var lockFile = new LockFile()
            {
                Version = 3
            };

            var target = new LockFileTarget()
            {
                TargetFramework = FrameworkConstants.CommonFrameworks.NetCoreApp30
            };

            var targetLib = new LockFileTargetLibrary()
            {
                Name = "My.Nice.Package.With.WPF.Reference",
                Version = NuGetVersion.Parse("2.0.0"),
                Type = LibraryType.Package
            };

            targetLib.CompileTimeAssemblies.Add(new LockFileItem("lib/netcoreapp3.0/a.dll"));
            // the order is important, the test assures that they are sorted.
            targetLib.FrameworkReferences.Add("Microsoft.Windows.Desktop|WindowsForms");
            targetLib.FrameworkReferences.Add("Microsoft.Windows.Desktop|WPF");

            target.Libraries.Add(targetLib);
            lockFile.Targets.Add(target);

            var lib = new LockFileLibrary()
            {
                Name = "My.Nice.Package.With.WPF.Reference",
                Version = NuGetVersion.Parse("2.0.0"),
                Type = LibraryType.Package,
                Sha512 = "sup3rs3cur3"
            };
            // the order is important, the test assures that they are sorted.
            lib.Files.Add("lib/netcoreapp3.0/a.dll");
            lib.Files.Add("My.Nice.Package.With.WPF.Reference.nuspec");
            lockFile.Libraries.Add(lib);

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile)).ToString();
            var expected = JObject.Parse(lockFileContent).ToString();

            // Assert
            Assert.Equal(expected, output);
        }

    }
}
