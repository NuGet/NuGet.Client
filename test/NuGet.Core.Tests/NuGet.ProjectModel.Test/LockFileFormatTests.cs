// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;

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
            string lockFileContent = @"{
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
            string lockFileContent = @"{
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
            var lockFile = new LockFile();
            lockFile.Version = 2;

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
                new ProjectFileDependencyGroup(FrameworkConstants.CommonFrameworks.DotNet.DotNetFrameworkName, new string[0]));

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
            string lockFileContent = @"{
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
            var lockFile = new LockFile();
            lockFile.Version = 2;

            lockFile.PackageSpec = new PackageSpec(new[]
            {
                new TargetFrameworkInformation
                {
                    FrameworkName = FrameworkConstants.CommonFrameworks.DotNet
                }
            });

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
            string lockFileContent = @"{
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
      ""outputType"": ""NETCore"",
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
          ""Microsoft.NETCore.App"": {
            ""target"": ""Package"",
            ""version"": ""1.0.1""
          },
          ""Microsoft.NET.Sdk"": {
            ""suppressParent"": ""All"",
            ""target"": ""Package"",
            ""version"": ""1.0.0-alpha-20161104-2""
          }
        }
      }
    }
  }
}";
            var lockFile = new LockFile();
            lockFile.Version = 2;

            lockFile.PackageSpec = new PackageSpec(new[]
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
            };

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile));
            var expected = JObject.Parse(lockFileContent);

            // Assert
            Assert.Equal(expected.ToString(), output.ToString());
        }
    }
}
