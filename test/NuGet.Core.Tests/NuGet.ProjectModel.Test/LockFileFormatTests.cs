﻿using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;
using System.Linq;
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

            Assert.Equal(0, lockFile.ProjectFileToolGroups.Count);
            Assert.Equal(0, lockFile.Tools.Count);
        }

        [Fact]
        public void LockFileFormat_ReadsLockFileWithTools()
        {
            string lockFileContent = @"{
  ""tools"": {
    "".NETStandard,Version=v1.2"": {
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
  ""projectFileToolGroups"": {
    "".NETFramework,Version=v4.5.1"": [],
    "".NETStandard,Version=v1.5"": [
      ""System.Runtime [4.0.10-beta-*, )""
    ]
  }
}";
            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            var tool = lockFile.Tools.Single();
            Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard12, tool.TargetFramework);

            var runtimeTargetLibrary = tool.Libraries.Single();
            Assert.Equal("System.Runtime", runtimeTargetLibrary.Name);
            Assert.Equal(NuGetVersion.Parse("4.0.20-beta-22927"), runtimeTargetLibrary.Version);
            Assert.Equal(0, runtimeTargetLibrary.NativeLibraries.Count);
            Assert.Equal(0, runtimeTargetLibrary.ResourceAssemblies.Count);
            Assert.Equal(0, runtimeTargetLibrary.FrameworkAssemblies.Count);
            Assert.Equal(0, runtimeTargetLibrary.RuntimeAssemblies.Count);
            Assert.Equal("ref/dotnet/System.Runtime.dll", runtimeTargetLibrary.CompileTimeAssemblies.Single().Path);

            var net451Group = lockFile.ProjectFileToolGroups.First();
            Assert.Equal(FrameworkConstants.CommonFrameworks.Net451, NuGetFramework.Parse(net451Group.FrameworkName));

            var netStandardGroup = lockFile.ProjectFileToolGroups.Last();
            Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard15, NuGetFramework.Parse(netStandardGroup.FrameworkName));
            Assert.Equal("System.Runtime [4.0.10-beta-*, )", netStandardGroup.Dependencies.Single());
        }

        [Fact]
        public void LockFileFormat_WritesLockFileWithNoTools()
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
  },
  ""tools"": {},
  ""projectFileToolGroups"": {}
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
        public void LockFileFormat_WritesLockFileWithTools()
        {
            // Arrange
            string lockFileContent = @"{
  ""version"": 2,
  ""targets"": {},
  ""libraries"": {},
  ""projectFileDependencyGroups"": {},
  ""tools"": {
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
  ""projectFileToolGroups"": {
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
            lockFile.Tools.Add(target);
            
            lockFile.ProjectFileToolGroups.Add(
                new ProjectFileDependencyGroup("", new string[] { "System.Runtime [4.0.10-beta-*, )" }));
            lockFile.ProjectFileToolGroups.Add(
                new ProjectFileDependencyGroup(FrameworkConstants.CommonFrameworks.DotNet.DotNetFrameworkName, new string[0]));

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile));
            var expected = JObject.Parse(lockFileContent);

            // Assert
            Assert.Equal(expected.ToString(), output.ToString());
        }
    }
}
