// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;
using static NuGet.Test.Utility.TestPackagesCore;

namespace NuGet.ProjectModel.Test
{
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

        [Theory]
        [InlineData("1.0.0", "1.0.0")]
        [InlineData("1.0.0-beta", "1.0.0-beta")]
        [InlineData("1.0.0-*", "1.0.0")]
        [InlineData("1.0.*", "1.0.0")]
        [InlineData("(1.0.*, )", "(1.0.0, )")]
        public void Test_WritePackageDependencyWithLegacyString(string version, string expectedVersion)
        {
            var package = new PackageDependency("a", VersionRange.Parse(version));

            using var writer = new JTokenWriter();

            JsonUtility.WritePackageDependencyWithLegacyString(writer, package);

            JToken actual = ((JProperty)writer.Token).Value;

            Assert.Equal(expectedVersion, actual);
        }

        [Theory]
        [InlineData("1.0.0", "[1.0.0, )")]
        [InlineData("1.0.0-beta", "[1.0.0-beta, )")]
        [InlineData("1.0.0-*", "[1.0.0-*, )")]
        [InlineData("1.0.*", "[1.0.*, )")]
        [InlineData("(1.0.*, )", "(1.0.*, )")]
        public void Test_WritePackageDependency(string version, string expectedVersion)
        {
            var package = new PackageDependency("a", VersionRange.Parse(version));

            using var writer = new JTokenWriter();

            JsonUtility.WritePackageDependency(writer, package);

            JToken actual = ((JProperty)writer.Token).Value;

            Assert.Equal(expectedVersion, actual);
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
        public void Render_LockFileWithPackageFolder_WritesPackageFolder()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 2,
  ""targets"": {},
  ""libraries"": {},
  ""projectFileDependencyGroups"": {},
  ""packageFolders"": {
    ""a"": {}
  }
}";

            var lockFile = new LockFile
            {
                Version = 2,
                PackageFolders = new List<LockFileItem>
                {
                    new("a")
                }
            };

            var lockFileFormat = new LockFileFormat();

            // Act
            string actual = lockFileFormat.Render(lockFile);

            // Assert
            JObject expected = JObject.Parse(lockFileContent);
            JObject output = JObject.Parse(actual);

            Assert.Equal(expected.ToString(), output.ToString());
        }

        [Fact]
        public void Render_LockFileWithLibrary_WritesLibrary()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 2,
  ""targets"": {},
  ""libraries"": {
    ""System.Runtime/4.0.20-beta-22927"": {
      ""servicable"": true,
      ""sha512"": ""sup3rs3cur3"",
      ""type"": ""package"",
      ""path"": ""foo"",
      ""msbuildProject"": ""bar"",
      ""hasTools"": true
    }
  },
  ""projectFileDependencyGroups"": {}
}";

            var lockFile = new LockFile
            {
                Version = 2,
                Libraries = new List<LockFileLibrary>
                {
                    new()
                    {
                        Name = "System.Runtime",
                        Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                        Type = LibraryType.Package,
                        Sha512 = "sup3rs3cur3",
                        IsServiceable = true,
                        Path = "foo",
                        MSBuildProject = "bar",
                        HasTools = true
                    }
                }
            };

            var lockFileFormat = new LockFileFormat();

            // Act
            string actual = lockFileFormat.Render(lockFile);

            // Assert
            JObject expected = JObject.Parse(lockFileContent);
            JObject output = JObject.Parse(actual);

            Assert.Equal(expected.ToString(), output.ToString());
        }

        [Fact]
        public void Render_LockFileWithTarget_WritesTarget()
        {
            // Arrange
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    ""net6.0"": {
      ""Microsoft.AspNetCore.JsonPatch/6.0.4"": {
        ""type"": ""package"",
        ""dependencies"": {
          ""Microsoft.CSharp"": ""4.7.0"",
          ""Newtonsoft.Json"": ""13.0.1""
        },
        ""frameworkAssemblies"": [
          ""System.Configuration""
        ],
        ""compile"": {
          ""lib/net6.0/Microsoft.AspNetCore.JsonPatch.dll"": {}
        },
        ""runtime"": {
          ""lib/net6.0/Microsoft.AspNetCore.JsonPatch.dll"": {}
        },
        ""resource"": {
          ""lib/net45/cs/FSharp.Core.resources.dll"": {
            ""locale"": ""cs""
          }
        },
        ""contentFiles"": {
          ""baz"": {
            ""copyToOutput"": false
          },
          ""foo"": {
            ""copyToOutput"": true,
            ""outputPath"": ""bar""
          }
        }
      },
      ""Project10/1.0.0"": {
        ""type"": ""project"",
        ""framework"": "".NETCoreApp,Version=v6.0""
      },
      ""Microsoft.Extensions.ApiDescription.Server/3.0.0"": {
        ""type"": ""package"",
         ""build"": {
          ""build/Microsoft.Extensions.ApiDescription.Server.props"": {},
          ""build/Microsoft.Extensions.ApiDescription.Server.targets"": {}
        },
        ""buildMultiTargeting"": {
          ""buildMultiTargeting/Microsoft.Extensions.ApiDescription.Server.props"": {},
          ""buildMultiTargeting/Microsoft.Extensions.ApiDescription.Server.targets"": {}
        }        
      },
       ""runtime.debian.8-x64.runtime.native.System.Security.Cryptography.OpenSsl/4.3.0"": {
        ""type"": ""package"",
        ""runtimeTargets"": {
          ""runtimes/debian.8-x64/native/System.Security.Cryptography.Native.OpenSsl.so"": {
            ""assetType"": ""native"",
            ""rid"": ""debian.8-x64""
          }
        }
      }
    }
  },
  ""libraries"": {},
  ""projectFileDependencyGroups"": {}
}";

            var lockFile = new LockFile
            {
                Version = 3,
                Targets = new List<LockFileTarget>
                {
                    new()
                    {
                        TargetFramework = FrameworkConstants.CommonFrameworks.Net60,
                        Libraries = new List<LockFileTargetLibrary>
                        {
                            new()
                            {
                                Name = "Microsoft.AspNetCore.JsonPatch",
                                Version = NuGetVersion.Parse("6.0.4"),
                                Type = LibraryType.Package,
                                Dependencies = new List<PackageDependency>
                                {
                                    new("Microsoft.CSharp", new VersionRange(NuGetVersion.Parse("4.7.0"))),
                                    new("Newtonsoft.Json", new VersionRange(NuGetVersion.Parse("13.0.1"))),
                                },
                                CompileTimeAssemblies = new List<LockFileItem>()
                                {
                                    new("lib/net6.0/Microsoft.AspNetCore.JsonPatch.dll")
                                },
                                FrameworkAssemblies = new List<string>
                                {
                                    "System.Configuration"
                                },
                                RuntimeAssemblies = new List<LockFileItem>
                                {
                                    new("lib/net6.0/Microsoft.AspNetCore.JsonPatch.dll")
                                },
                                ResourceAssemblies = new List<LockFileItem>
                                {
                                    new("lib/net45/cs/FSharp.Core.resources.dll")
                                    {
                                        Properties =
                                        {
                                            ["locale"] = "cs"
                                        }
                                    }
                                },
                                ContentFiles = new List<LockFileContentFile>
                                {
                                    new("foo")
                                    {
                                        OutputPath = "bar",
                                        CopyToOutput = true
                                    },
                                    new("baz")
                                    {
                                        CopyToOutput = false
                                    }
                                }
                            },
                            new()
                            {
                                Name = "Project10",
                                Version = NuGetVersion.Parse("1.0.0"),
                                Type = "project",
                                Framework = ".NETCoreApp,Version=v6.0",
                            },
                            new()
                            {
                                Name = "Microsoft.Extensions.ApiDescription.Server",
                                Version = NuGetVersion.Parse("3.0.0"),
                                Type = "package",
                                Build = new List<LockFileItem>
                                {
                                    new("build/Microsoft.Extensions.ApiDescription.Server.props"),
                                    new("build/Microsoft.Extensions.ApiDescription.Server.targets"),
                                },
                                BuildMultiTargeting = new List<LockFileItem>()
                                {
                                    new("buildMultiTargeting/Microsoft.Extensions.ApiDescription.Server.props"),
                                    new("buildMultiTargeting/Microsoft.Extensions.ApiDescription.Server.targets")
                                }
                            },
                            new()
                            {
                                Name = "runtime.debian.8-x64.runtime.native.System.Security.Cryptography.OpenSsl",
                                Version = NuGetVersion.Parse("4.3.0"),
                                Type = "package",
                                RuntimeTargets = new List<LockFileRuntimeTarget>
                                {
                                    new("runtimes/debian.8-x64/native/System.Security.Cryptography.Native.OpenSsl.so")
                                    {
                                        AssetType = "native",
                                        Runtime = "debian.8-x64"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var lockFileFormat = new LockFileFormat();

            // Act
            string actual = lockFileFormat.Render(lockFile);

            // Assert
            JObject expected = JObject.Parse(lockFileContent);
            JObject output = JObject.Parse(actual);

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
        ""netcoreapp10""
      ],
      ""frameworks"": {
        ""netcoreapp1.0"": {
          ""targetAlias"": ""netcoreapp10"",
          ""projectReferences"": {}
        }
      }
    },
    ""frameworks"": {
      ""netcoreapp1.0"": {
        ""targetAlias"": ""netcoreapp10"",
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
                        TargetAlias = "netcoreapp10",
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
                        OriginalTargetFrameworks = new[] { "netcoreapp10" },
                        TargetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>
                        {
                            new ProjectRestoreMetadataFrameworkInfo(NuGetFramework.Parse("netcoreapp1.0"))
                            {
                                TargetAlias = "netcoreapp10",
                            }
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
                lockFileObj = FileUtility.SafeRead(lockFile, (stream, path) => reader.Read(stream, NullLogger.Instance, path));
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
        public void LockFileFormat_ReadsLockFully()
        {
            var lockFileContent = @"{
  ""version"": 3,
  ""targets"": {
    ""net6.0"": {
      ""Microsoft.AspNetCore.JsonPatch/6.0.4"": {
        ""type"": ""package"",
        ""framework"": "".NETCoreApp,Version=v6.0"",
        ""dependencies"": {
          ""Microsoft.CSharp"": ""4.7.0"",
          ""Newtonsoft.Json"": ""13.0.1""
        },
        ""frameworkAssemblies"": [
          ""System.Configuration""
        ],
        ""compile"": {
          ""lib/net6.0/Microsoft.AspNetCore.JsonPatch.dll"": {}
        },
        ""runtime"": {
          ""lib/net6.0/Microsoft.AspNetCore.JsonPatch.dll"": {
                ""property1"": ""val1"",
                ""property2"": 12,
                ""property3"": true,
                ""property4"": false,
            }
        },
        ""resource"": {
        },
        ""contentFiles"": {
          ""baz"": {
            ""copyToOutput"": false
          },
          ""foo"": {
            ""copyToOutput"": true,
            ""outputPath"": ""bar""
          }
        },
        ""runtimeTargets"": {
          ""foo"": {
            ""copyToOutput"": 33
          },
          ""fooz"": {
            ""copyToOutput"": true,
            ""outputPath"": ""bar""
          }
        },
        ""frameworkReferences"": [
          ""Microsoft.Windows.Desktop|WPF"",
          ""Microsoft.Windows.Desktop|WindowsForms""
        ]
      },
      ""Project10/1.0.0"": {
        ""type"": ""project"",
        ""framework"": "".NETCoreApp,Version=v6.0""
      },
      ""Microsoft.Extensions.ApiDescription.Server/3.0.0"": {
        ""type"": ""package"",
         ""build"": {
          ""build/Microsoft.Extensions.ApiDescription.Server.props"": {},
          ""build/Microsoft.Extensions.ApiDescription.Server.targets"": {}
        },
        ""buildMultiTargeting"": {
          ""buildMultiTargeting/Microsoft.Extensions.ApiDescription.Server.props"": {},
          ""buildMultiTargeting/Microsoft.Extensions.ApiDescription.Server.targets"": {}
        }        
      },
       ""runtime.debian.8-x64.runtime.native.System.Security.Cryptography.OpenSsl/4.3.0"": {
        ""type"": ""package"",
        ""runtimeTargets"": {
          ""runtimes/debian.8-x64/native/System.Security.Cryptography.Native.OpenSsl.so"": {
            ""assetType"": ""native"",
            ""rid"": ""debian.8-x64""
          }
        }
      }
    },
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
    },
    ""System.Runtime/4.0.20-beta-22927"": {
        ""sha512"": ""sup3rs3cur3"",
        ""type"": ""package"",
        ""path"": ""C:\\\\a\\test\\path"",
        ""msbuildProject"": ""bar"",
        ""servicable"": true,
        ""hasTools"": true,
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
  ""packageFolders"": {
    ""a"": {
      ""location"": ""loc"",
      ""propbool"": false
    },
    ""b"": {}
  },
""project"": {
    ""version"": ""1.0.0"",
    ""authors"": [ ""person1"", ""person2"" ],
    ""copyright"": ""microsoft"",
    ""description"": ""test project"",
    ""language"": ""en-us"",
    ""restore"": {
        ""centralPackageVersionsManagementEnabled"": true,
        ""centralPackageVersionOverrideDisabled"": false,
        ""CentralPackageTransitivePinningEnabled"": true,
        ""legacyPackagesDirectory"": true,
        ""outputPath"": ""X:\\ProjectPath\\obj\\"",
        ""packagesConfigPath"": ""X:\\ProjectPath\\packages.config"",
        ""packagesPath"": ""X:\\ProjectPath\\packages"",
        ""projectJsonPath"": ""X:\\ProjectPath\\project.json"",
        ""projectName"": ""ProjectPath"",
        ""projectPath"": ""X:\\ProjectPath\\ProjectPath.csproj"",
        ""projectStyle"": ""Standalone"",
        ""projectUniqueName"": ""X:\\ProjectPath\\ProjectPath.csproj"",
        ""restoreLockProperties"": {
            ""nuGetLockFilePath"": ""X:\\ProjectPath\\obj\\project.assets.json"",
            ""restoreLockedMode"": false,
            ""restorePackagesWithLockFile"": ""true""
        },
        ""restoreAuditProperties"": {
            ""enableAudit"": ""true"",
            ""auditLevel"": ""four"",
            ""auditMode"": ""intrusive""
        },
        ""skipContentFileWrite"": true,
        ""sources"": {
            ""source1"": {},
            ""source2"": {
                ""noOne"": ""cares about me :(""
            }
        },
        ""validateRuntimeAssets"": false,
        ""warningProperties"": {
            ""allWarningsAsErrors"": true,
            ""noWarn"": [""NU1005"",""NU1011""],
            ""warnAsError"": [""NU1005""],
            ""warnNotAsError"": [""NU1011""]
        },
        ""configFilePaths"": [],
        ""crossTargeting"": true,
        ""fallbackFolders"": [],
        ""originalTargetFrameworks"": [
            ""netcoreapp10""
        ],
        ""files"": {
            ""file1"": ""X:\\ProjectPath\\ProjectPath.csproj""
        },
        ""frameworks"": {
            ""netcoreapp1.0"": {
                ""targetAlias"": ""netcoreapp10"",
                ""projectReferences"": {
                    ""project1"": {
                        ""excludeAssets"": ""build"",
                        ""includeAssets"": ""runtime"",
                        ""privateAssets"": ""all"",
                        ""projectPath"": ""X:\\ProjectPath\\ProjectPath.csproj""
                    }
                }
            }
        },
    },
    ""packOptions"": {
        ""files"": {
            ""excludeFiles"": ""**/*.txt"",
            ""exclude"": [""excl1"", ""excl2""],
            ""includeFiles"": [],
            ""include"": [""incl1"", ""incl2""],
            ""mappings"": {
                ""a"": ""first"",
                ""b"": [""first"", ""second""],
                ""c"": {
                    ""excludeFiles"": ""**/*.txt"",
                    ""exclude"": [""excl1"", ""excl2""],
                    ""includeFiles"": [],
                    ""include"": [""incl1"", ""incl2""],
                }
            }
        },
        ""iconUrl"": ""icon.png"",
        ""licenseUrl"": ""license/url"",
        ""projectUrl"": ""project/url"",
        ""releaseNotes"": ""some note s"",
        ""summary"": ""sum mary"",
        ""tags"": [],
        ""requireLicenseAcceptance"": false,
        ""owners"": [""first"", ""person""],
        ""packageType"": [""package1"", ""package2""],
    },
    ""dependencies"": {
        ""library1"": ""1.0.0"",
        ""library2"": {
            ""autoReferenced"": true,
            ""exclude"": ""build compile"",
            ""generatePathProperty"": false,
            ""include"": ""runtime,native"",
            ""noWarn"": [""NU1005"",""NU1011""],
            ""suppressParent"": """",
            ""target"": ""package"",
            ""version"": ""1.2.3"",
            ""versionOverride"": ""1.2.6"",
            ""versionCentrallyManaged"": true,
            ""aliases"": ""alias"",
        },
    },
    ""scripts"": {
            ""script1"": ""script1.cmd"",
            ""script2"": [""script1.ps1"", ""script2.js""]
    },
    ""buildOptions"": {
      ""outputName"": ""someoutput"",
    },
    ""contentFiles"": [
      ""outputName"",
      ""someoutput""
    ],
    ""frameworks"": {
        ""netcoreapp1.0"": {
            ""assetTargetFallback"": true,
            ""secondaryFramework"": ""dotnet"",
            ""centralPackageVersions"": {
                ""package1"": ""1.0.0"",
                ""package2"": ""2.0.0""
            },
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
            },
            ""downloadDependencies"": [
                {
                    ""name"": ""dep1"",
                    ""version"": ""1.0.0"",
                },
                {
                    ""name"": ""dep1"",
                    ""version"": ""2.0.0"",
                },
                {
                    ""name"": ""dep2"",
                    ""version"": ""2.0.0;[1.0.1, )"",
                }
            ],
            ""frameworkAssemblies"": { },
            ""frameworkReferences"": {
                ""frameworkName1"": {
                    ""privateAssets"": ""all,none"",
                },
                ""frameworkName2"": {
                    ""privateAssets"": ""all"",
                }
            },
            ""imports"": [ ""netstandard1.1"", ""netstandard1.2"", ""dnxcore50""  ],
            ""runtimeIdentifierGraphPath"": ""path\\to\\sdk\\3.0.100\\runtime.json"",
            ""targetAlias"" : ""minNetVersion"",
            ""warn"" : false,
        },
        ""netstandard1.1"": {
            ""assetTargetFallback"": false,
            ""imports"": [ ""netstandard1.1"", ""netstandard1.2"", ""dnxcore50""  ]
        },
        ""net6.0"": {
            ""secondaryFramework"": ""dotnet"",
        },
        ""dotnet"": {
        }
    },
    ""packInclude"": {
      ""pack1"": ""val"",
      ""pack2"": ""hello"",
    }, 
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
            var lockFileFormat = new LockFileFormat();


            //#pragma warning disable CS0618 // Type or member is obsolete
            //            var lockFile = lockFileFormat.Read(textreader, "In Memory");
            //#pragma warning restore CS0618 // Type or member is obsolete


            var lockFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            Assert.Equal(3, lockFile.Version);

            //Libraries validation
            Assert.Equal(2, lockFile.Libraries.Count);
            var runtimeLibrary = lockFile.Libraries.FirstOrDefault(x => x.Name == "System.Runtime");
            Assert.NotNull(runtimeLibrary);
            Assert.Equal(NuGetVersion.Parse("4.0.20-beta-22927"), runtimeLibrary.Version);
            Assert.Equal("package", runtimeLibrary.Type);
            Assert.Equal("C:\\\\a\\test\\path", runtimeLibrary.Path);
            Assert.Equal("bar", runtimeLibrary.MSBuildProject);
            Assert.Equal("sup3rs3cur3", runtimeLibrary.Sha512);
            Assert.True(runtimeLibrary.IsServiceable);
            Assert.True(runtimeLibrary.HasTools);
            Assert.Equal(1, runtimeLibrary.Files.Count);
            Assert.Equal("System.Runtime.nuspec", runtimeLibrary.Files[0]);

            //Target validation
            Assert.Equal(2, lockFile.Targets.Count);
            var net60Framework = NuGetFramework.Parse("net6.0");
            var target = lockFile.Targets.FirstOrDefault(x => x.Name == "net6.0");
            Assert.Equal(net60Framework, target.TargetFramework);
            Assert.Equal(4, target.Libraries.Count);
            var runtimeTargetLibrary = target.Libraries.FirstOrDefault(x => x.Name == "Microsoft.AspNetCore.JsonPatch");
            Assert.NotNull(runtimeTargetLibrary);
            Assert.Equal(NuGetVersion.Parse("6.0.4"), runtimeTargetLibrary.Version);
            Assert.Equal("package", runtimeTargetLibrary.Type);
            Assert.Equal(".NETCoreApp,Version=v6.0", runtimeTargetLibrary.Framework);
            Assert.Equal(".NETCoreApp,Version=v6.0", runtimeTargetLibrary.Framework);
            Assert.Equal(2, runtimeTargetLibrary.Dependencies.Count);
            Assert.Equal("Microsoft.CSharp", runtimeTargetLibrary.Dependencies[0].Id);
            Assert.Equal(VersionRange.Parse("4.7.0"), runtimeTargetLibrary.Dependencies[0].VersionRange);
            Assert.Equal(1, runtimeTargetLibrary.FrameworkAssemblies.Count);
            Assert.Equal("System.Configuration", runtimeTargetLibrary.FrameworkAssemblies[0]);
            Assert.Equal(1, runtimeTargetLibrary.RuntimeAssemblies.Count);
            Assert.Equal("lib/net6.0/Microsoft.AspNetCore.JsonPatch.dll", runtimeTargetLibrary.RuntimeAssemblies[0].Path);
            Assert.Equal(4, runtimeTargetLibrary.RuntimeAssemblies[0].Properties.Count);
            Assert.Equal("val1", runtimeTargetLibrary.RuntimeAssemblies[0].Properties["property1"]);
            Assert.Equal("12", runtimeTargetLibrary.RuntimeAssemblies[0].Properties["property2"]);
            Assert.Equal("True", runtimeTargetLibrary.RuntimeAssemblies[0].Properties["property3"]);
            Assert.Equal("False", runtimeTargetLibrary.RuntimeAssemblies[0].Properties["property4"]);
            Assert.Equal(1, runtimeTargetLibrary.CompileTimeAssemblies.Count);
            Assert.Equal("lib/net6.0/Microsoft.AspNetCore.JsonPatch.dll", runtimeTargetLibrary.CompileTimeAssemblies[0].Path);
            Assert.Equal(0, runtimeTargetLibrary.CompileTimeAssemblies[0].Properties.Count);
            Assert.Equal(0, runtimeTargetLibrary.ResourceAssemblies.Count);
            Assert.Equal(0, runtimeTargetLibrary.NativeLibraries.Count);
            Assert.Equal(0, runtimeTargetLibrary.Build.Count);
            Assert.Equal(0, runtimeTargetLibrary.BuildMultiTargeting.Count);
            Assert.Equal(2, runtimeTargetLibrary.ContentFiles.Count);
            Assert.Equal("baz", runtimeTargetLibrary.ContentFiles[0].Path);
            Assert.Equal("False", runtimeTargetLibrary.ContentFiles[0].Properties["copyToOutput"]);
            Assert.Equal(2, runtimeTargetLibrary.RuntimeTargets.Count);
            Assert.Equal("foo", runtimeTargetLibrary.RuntimeTargets[0].Path);
            Assert.Equal("33", runtimeTargetLibrary.RuntimeTargets[0].Properties["copyToOutput"]);
            Assert.Equal(0, runtimeTargetLibrary.ToolsAssemblies.Count);
            Assert.Equal(0, runtimeTargetLibrary.EmbedAssemblies.Count);
            Assert.Equal(2, runtimeTargetLibrary.FrameworkReferences.Count);
            Assert.Equal("Microsoft.Windows.Desktop|WPF", runtimeTargetLibrary.FrameworkReferences[0]);

            //Project file dependency tests
            Assert.Equal(2, lockFile.ProjectFileDependencyGroups.Count);
            var emptyDepGroup = lockFile.ProjectFileDependencyGroups.First();
            Assert.True(string.IsNullOrEmpty(emptyDepGroup.FrameworkName));
            Assert.Equal("System.Runtime [4.0.10-beta-*, )", emptyDepGroup.Dependencies.Single());
            var netPlatDepGroup = lockFile.ProjectFileDependencyGroups.Last();
            Assert.Equal(NuGetFramework.Parse("dotnet").DotNetFrameworkName, netPlatDepGroup.FrameworkName);
            Assert.Empty(netPlatDepGroup.Dependencies);

            //Package folders
            Assert.Equal(2, lockFile.PackageFolders.Count);
            var aFolder = lockFile.PackageFolders.First();
            Assert.Equal("loc", aFolder.Properties["location"]);

            //Package spec tests
            var packageSpec = lockFile.PackageSpec;
#pragma warning disable CS0612 // Type or member is obsolete
            Assert.Equal(2, packageSpec.Authors.Count());
            Assert.Equal("someoutput", packageSpec.BuildOptions.OutputName);
            Assert.Equal(2, packageSpec.ContentFiles.Count());
            Assert.Equal("outputName", packageSpec.ContentFiles[0]);
            Assert.Equal("microsoft", packageSpec.Copyright);
            Assert.Equal("test project", packageSpec.Description);
            Assert.Equal("en-us", packageSpec.Language);
            Assert.Equal(2, packageSpec.PackInclude.Count());
            Assert.Equal("val", packageSpec.PackInclude["pack1"]);
            Assert.Equal("hello", packageSpec.PackInclude["pack2"]);
            Assert.Equal(1, packageSpec.PackOptions.IncludeExcludeFiles.ExcludeFiles.Count());
            Assert.Equal("**/*.txt", packageSpec.PackOptions.IncludeExcludeFiles.ExcludeFiles[0]);
            Assert.Equal(2, packageSpec.PackOptions.IncludeExcludeFiles.Exclude.Count());
            Assert.Equal("excl1", packageSpec.PackOptions.IncludeExcludeFiles.Exclude[0]);
            Assert.Equal("excl2", packageSpec.PackOptions.IncludeExcludeFiles.Exclude[1]);
            Assert.Equal(0, packageSpec.PackOptions.IncludeExcludeFiles.IncludeFiles.Count());
            Assert.Equal(2, packageSpec.PackOptions.IncludeExcludeFiles.Include.Count());
            Assert.Equal(3, packageSpec.PackOptions.Mappings.Count());
            Assert.Equal(1, packageSpec.PackOptions.Mappings["a"].Include.Count());
            Assert.Equal("first", packageSpec.PackOptions.Mappings["a"].Include[0]);
            Assert.Equal(2, packageSpec.PackOptions.Mappings["b"].Include.Count());
            Assert.Equal("first", packageSpec.PackOptions.Mappings["b"].Include[0]);
            Assert.Equal("second", packageSpec.PackOptions.Mappings["b"].Include[1]);
            Assert.Equal(1, packageSpec.PackOptions.Mappings["c"].ExcludeFiles.Count());
            Assert.Equal(2, packageSpec.PackOptions.Mappings["c"].Exclude.Count());
            Assert.Equal(0, packageSpec.PackOptions.Mappings["c"].IncludeFiles.Count());
            Assert.Equal(2, packageSpec.PackOptions.Mappings["c"].Include.Count());
            Assert.Equal("icon.png", packageSpec.IconUrl);
            Assert.Equal("license/url", packageSpec.LicenseUrl);
            Assert.Equal("project/url", packageSpec.ProjectUrl);
            Assert.Equal("some note s", packageSpec.ReleaseNotes);
            Assert.Equal("sum mary", packageSpec.Summary);
            Assert.Equal(0, packageSpec.Tags.Length);
            Assert.Equal(2, packageSpec.Scripts.Count);
            Assert.Equal(1, packageSpec.Scripts["script1"].Count());
            Assert.Equal("script1.cmd", packageSpec.Scripts["script1"].First());
            Assert.Equal(2, packageSpec.Scripts["script2"].Count());
#pragma warning restore CS0612 // Type or member is obsolete
            Assert.Equal(2, packageSpec.Dependencies.Count());
            var library1 = packageSpec.Dependencies[0];
            Assert.Equal("library1", library1.Name);
            Assert.Equal(VersionRange.Parse("1.0.0"), library1.LibraryRange.VersionRange);
            var library2 = packageSpec.Dependencies[1];
            Assert.Equal("library2", library2.Name);
            Assert.True(library2.AutoReferenced);
            Assert.Equal((LibraryIncludeFlags.None | LibraryIncludeFlags.Runtime | LibraryIncludeFlags.Native) & ~(LibraryIncludeFlags.None | LibraryIncludeFlags.Build | LibraryIncludeFlags.Compile), library2.IncludeType);
            Assert.Equal(2, library2.NoWarn.Count);
            Assert.Equal(NuGetLogCode.NU1005, library2.NoWarn[0]);
            Assert.Equal(LibraryIncludeFlags.None, library2.SuppressParent);
            Assert.Equal(LibraryDependencyTarget.Package, library2.LibraryRange.TypeConstraint);
            Assert.Equal(VersionRange.Parse("1.2.3"), library2.LibraryRange.VersionRange);
            Assert.Equal(VersionRange.Parse("1.2.6"), library2.VersionOverride);
            Assert.True(library2.VersionCentrallyManaged);
            Assert.False(library2.GeneratePathProperty);
            Assert.Equal("alias", library2.Aliases);

            Assert.Equal(4, packageSpec.TargetFrameworks.Count());
            var framework1 = packageSpec.TargetFrameworks[0];
            Assert.True(framework1.AssetTargetFallback);
            Assert.False(framework1.Warn);
            Assert.Equal("minNetVersion", framework1.TargetAlias);
            Assert.Equal("path\\to\\sdk\\3.0.100\\runtime.json", framework1.RuntimeIdentifierGraphPath);
            Assert.Equal(2, framework1.CentralPackageVersions.Count);
            Assert.Equal("package1", framework1.CentralPackageVersions["package1"].Name);
            Assert.Equal(VersionRange.Parse("1.0.0"), framework1.CentralPackageVersions["package1"].VersionRange);
            Assert.Equal(2, framework1.Dependencies.Count);
            Assert.Equal(3, framework1.DownloadDependencies.Count);
            Assert.Equal("dep1", framework1.DownloadDependencies[0].Name);
            Assert.Equal(VersionRange.Parse("1.0.0"), framework1.DownloadDependencies[0].VersionRange);
            Assert.Equal("dep2", framework1.DownloadDependencies[1].Name);
            Assert.Equal(VersionRange.Parse("2.0.0"), framework1.DownloadDependencies[1].VersionRange);
            Assert.Equal("dep2", framework1.DownloadDependencies[2].Name);
            Assert.Equal(VersionRange.Parse("[1.0.1, )"), framework1.DownloadDependencies[2].VersionRange);
            Assert.Equal(2, framework1.FrameworkReferences.Count);
            Assert.Equal("frameworkName1", framework1.FrameworkReferences.First().Name);
            Assert.Equal(FrameworkDependencyFlags.All | FrameworkDependencyFlags.None, framework1.FrameworkReferences.First().PrivateAssets);
            Assert.Equal("frameworkName2", framework1.FrameworkReferences.Last().Name);
            Assert.Equal(FrameworkDependencyFlags.All, framework1.FrameworkReferences.Last().PrivateAssets);
            Assert.Equal(3, framework1.Imports.Count);
            Assert.Equal(NuGetFramework.Parse("netstandard1.1"), framework1.Imports[0]);
            Assert.Equal(typeof(AssetTargetFallbackFramework), framework1.FrameworkName.GetType());
            Assert.Equal(NuGetFramework.Parse("netcoreapp1.0"), framework1.FrameworkName);

            var framework2 = packageSpec.TargetFrameworks[1];
            Assert.Equal(typeof(FallbackFramework), framework2.FrameworkName.GetType());
            Assert.Equal(NuGetFramework.Parse("netstandard1.1"), framework2.FrameworkName);

            var framework3 = packageSpec.TargetFrameworks[2];
            Assert.Equal(typeof(DualCompatibilityFramework), framework3.FrameworkName.GetType());
            Assert.Equal(NuGetFramework.Parse("net6.0"), framework3.FrameworkName);

            var framework4 = packageSpec.TargetFrameworks[3];
            Assert.Equal(typeof(NuGetFramework), framework4.FrameworkName.GetType());
            Assert.Equal(NuGetFramework.Parse("dotnet"), framework4.FrameworkName);

            Assert.Equal(typeof(ProjectRestoreMetadata), packageSpec.RestoreMetadata.GetType());
            Assert.True(packageSpec.RestoreMetadata.CentralPackageTransitivePinningEnabled);
            Assert.False(packageSpec.RestoreMetadata.CentralPackageVersionOverrideDisabled);
            Assert.True(packageSpec.RestoreMetadata.CentralPackageVersionsEnabled);
            Assert.Equal(1, packageSpec.RestoreMetadata.OriginalTargetFrameworks.Count);
            Assert.Equal("X:\\ProjectPath\\obj\\", packageSpec.RestoreMetadata.OutputPath);
            Assert.Equal("X:\\ProjectPath\\packages", packageSpec.RestoreMetadata.PackagesPath);
            Assert.Equal("X:\\ProjectPath\\project.json", packageSpec.RestoreMetadata.ProjectJsonPath);
            Assert.Equal("ProjectPath", packageSpec.RestoreMetadata.ProjectName);
            Assert.Equal(ProjectStyle.Standalone, packageSpec.RestoreMetadata.ProjectStyle);
            Assert.Equal("X:\\ProjectPath\\ProjectPath.csproj", packageSpec.RestoreMetadata.ProjectUniqueName);
            Assert.Equal("X:\\ProjectPath\\obj\\project.assets.json", packageSpec.RestoreMetadata.RestoreLockProperties.NuGetLockFilePath);
            Assert.Equal("true", packageSpec.RestoreMetadata.RestoreLockProperties.RestorePackagesWithLockFile);
            Assert.False(packageSpec.RestoreMetadata.RestoreLockProperties.RestoreLockedMode);
            Assert.Equal("true", packageSpec.RestoreMetadata.RestoreAuditProperties.EnableAudit);
            Assert.Equal("four", packageSpec.RestoreMetadata.RestoreAuditProperties.AuditLevel);
            Assert.Equal("intrusive", packageSpec.RestoreMetadata.RestoreAuditProperties.AuditMode);
            Assert.True(packageSpec.RestoreMetadata.SkipContentFileWrite);
            Assert.Equal(2, packageSpec.RestoreMetadata.Sources.Count);
            Assert.Equal(new PackageSource("source1"), packageSpec.RestoreMetadata.Sources[0]);
            Assert.Equal(new PackageSource("source2"), packageSpec.RestoreMetadata.Sources[1]);
            Assert.False(packageSpec.RestoreMetadata.ValidateRuntimeAssets);
            Assert.Equal(0, packageSpec.RestoreMetadata.ConfigFilePaths.Count);
            Assert.True(packageSpec.RestoreMetadata.CrossTargeting);
            Assert.Equal(0, packageSpec.RestoreMetadata.FallbackFolders.Count);
            Assert.Equal(1, packageSpec.RestoreMetadata.OriginalTargetFrameworks.Count);
            Assert.Equal(1, packageSpec.RestoreMetadata.Files.Count);
            Assert.Equal(new ProjectRestoreMetadataFile("file1", "X:\\ProjectPath\\ProjectPath.csproj"), packageSpec.RestoreMetadata.Files[0]);
            Assert.Equal(1, packageSpec.RestoreMetadata.TargetFrameworks.Count);
            Assert.Equal(2, packageSpec.RestoreMetadata.ProjectWideWarningProperties.NoWarn.Count);

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

        [Fact]
        public void LockFileFormat_WritesCentralTransitiveDependencyGroups()
        {
            // Arrange
            NuGetFramework framework = FrameworkConstants.CommonFrameworks.DotNet;
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
            ""centralTransitiveDependencyGroups"": {
                "".NETPlatform,Version=v5.0"": {
                ""Newtonsoft.Json"": {
                            ""include"": ""Compile, Native, BuildTransitive"",
                            ""suppressParent"": ""All"",
                            ""version"": ""[12.0.3, )""           
                        }
                    }
                }
            }";

            var lockFile = new LockFile()
            {
                Version = 3
            };

            var target = new LockFileTarget()
            {
                TargetFramework = framework
            };

            var targetLib = new LockFileTargetLibrary()
            {
                Name = "System.Runtime",
                Version = NuGetVersion.Parse("4.0.20-beta-22927"),
                Type = LibraryType.Package
            };

            targetLib.Dependencies.Add(new NuGet.Packaging.Core.PackageDependency("Frob", new VersionRange(NuGetVersion.Parse("4.0.20"))));
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

            lockFile.ProjectFileDependencyGroups
                .Add(new ProjectFileDependencyGroup("", new string[] { "System.Runtime [4.0.10-beta-*, )" }));
            lockFile.ProjectFileDependencyGroups
                .Add(new ProjectFileDependencyGroup(framework.DotNetFrameworkName, Array.Empty<string>()));

            var newtonSoftDependency = new LibraryDependency(
                        libraryRange: new LibraryRange("Newtonsoft.Json", VersionRange.Parse("[12.0.3, )"), LibraryDependencyTarget.Package),
                        includeType: LibraryIncludeFlags.Compile | LibraryIncludeFlags.BuildTransitive | LibraryIncludeFlags.Native,
                        suppressParent: LibraryIncludeFlags.All,
                        noWarn: new List<NuGetLogCode>(),
                        autoReferenced: true,
                        generatePathProperty: false,
                        versionCentrallyManaged: false,
                        LibraryDependencyReferenceType.Direct,
                        aliases: null,
                        versionOverride: null);
            newtonSoftDependency.VersionCentrallyManaged = true;

            lockFile.CentralTransitiveDependencyGroups
                .Add(new CentralTransitiveDependencyGroup(framework, new List<LibraryDependency>() { newtonSoftDependency }));

            // Act
            var lockFileFormat = new LockFileFormat();
            var output = JObject.Parse(lockFileFormat.Render(lockFile));
            var expected = JObject.Parse(lockFileContent);

            // Assert
            Assert.Equal(expected.ToString(), output.ToString());
        }
    }
}
