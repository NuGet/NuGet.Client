// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class PackagesLockFileFormatTests
    {
        [Fact]
        public void PackagesLockFileFormat_Read()
        {
            var nuGetLockFileContent = @"{
                ""version"": 1,
                ""dependencies"": {
                    "".NETFramework,Version=v4.5"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""requested"": ""[1.*, )"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""sbWWhjA2/cXJHBBKAVo3m2U0KxzNuW5dQANDwx8L96V+L6SML96cM/Myvmp6fiBqIDibvF6+Ss9YC+qqclrXnw=="",
                            ""dependencies"": {
                                 ""PackageB"": ""1.0.0""
                            }
                        },
                        ""PackageB"": {
                            ""type"": ""Transitive"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""Fjiywrwerewr4dgbdgbfgjkoiuiorwrwn24+8hjnnuerwrwsfsHYWD3HJYUI7NJHssxDFSFSFEWEW34DFDFCVsxv=="",
                        }
                    }
                }
            }";

            var lockFile = PackagesLockFileFormat.Parse(nuGetLockFileContent, "In Memory");

            Assert.Equal(1, lockFile.Targets.Count);

            var target = lockFile.Targets.First();
            Assert.Equal(".NETFramework,Version=v4.5", target.Name);
            Assert.Equal(2, target.Dependencies.Count);

            Assert.Equal("PackageA", target.Dependencies[0].Id);
            Assert.Equal(PackageDependencyType.Direct, target.Dependencies[0].Type);
            Assert.Equal("[1.*, )", target.Dependencies[0].RequestedVersion.ToNormalizedString());
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion.ToNormalizedString());
            Assert.NotEmpty(target.Dependencies[0].ContentHash);
            Assert.Equal(1, target.Dependencies[0].Dependencies.Count);
            Assert.Equal("PackageB", target.Dependencies[0].Dependencies[0].Id);


            Assert.Equal("PackageB", target.Dependencies[1].Id);
            Assert.Equal(PackageDependencyType.Transitive, target.Dependencies[1].Type);
            Assert.Null(target.Dependencies[1].RequestedVersion);
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion.ToNormalizedString());
            Assert.NotEmpty(target.Dependencies[1].ContentHash);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadWithCommentsAndVersionAfterDependencies()
        {
            var lockFileContent = @"{
                // The version property is not required to be first.
                ""dependencies"": {
                    ""net8.0"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""resolved"": ""1.0.0"",
                            ""dependencies"": {
                                ""PackageB"": null,
                            },
                        },
                    },
                },
                ""version"": 1,
            }";

            PackagesLockFile lockFile = ParseWithSystemTextJson(lockFileContent);

            Assert.Equal(1, lockFile.Version);
            var target = Assert.Single(lockFile.Targets);
            Assert.Equal(NuGetFramework.Parse("net8.0"), target.TargetFramework);
            var package = Assert.Single(target.Dependencies);
            Assert.Equal("PackageA", package.Id);
            Assert.Equal(VersionRange.All, Assert.Single(package.Dependencies).VersionRange);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadVersion3WithVersionAfterTargets()
        {
            var lockFileContent = @"{
                ""net8.0"": {
                    ""framework"": ""net8.0"",
                    ""dependencies"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""resolved"": ""1.0.0""
                        }
                    }
                },
                ""version"": 3
            }";

            PackagesLockFile lockFile = ParseWithSystemTextJson(lockFileContent);

            Assert.Equal(3, lockFile.Version);
            var target = Assert.Single(lockFile.Targets);
            Assert.Equal("net8.0", target.TargetAlias);
            Assert.Equal("PackageA", Assert.Single(target.Dependencies).Id);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadLockFileWithSystemTextJsonUtf8Bom_ParsesLockFile()
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                bufferSize: 1024,
                leaveOpen: true))
            {
                writer.Write(@"{ ""version"": 1, ""dependencies"": {} }");
            }
            stream.Position = 0;

            PackagesLockFile lockFile = PackagesLockFileFormat.ReadLockFileWithSystemTextJson(stream);

            Assert.False(stream.CanRead);
            Assert.Equal(1, lockFile.Version);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadLockFileWithSystemTextJsonLeadingTriviaLargerThanBuffer_ParsesLockFile()
        {
            string content = new string(' ', 20_000) + """{"version":1,"dependencies":{}}""";

            PackagesLockFile lockFile = ParseWithSystemTextJson(content);

            Assert.Equal(1, lockFile.Version);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadLockFileWithSystemTextJsonDuplicateDependencies_UsesLastValue()
        {
            const string content = """
                {
                    "version": 1,
                    "dependencies": {
                        "net8.0": {
                            "PackageA": null,
                            "PackageA": { "type": "Direct", "resolved": "2.0.0" }
                        }
                    }
                }
                """;

            PackagesLockFile lockFile = ParseWithSystemTextJson(content);

            LockFileDependency dependency = Assert.Single(Assert.Single(lockFile.Targets).Dependencies);
            Assert.Equal(NuGetVersion.Parse("2.0.0"), dependency.ResolvedVersion);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadLockFileWithSystemTextJsonInvalidLastDuplicateTarget_IgnoresTarget()
        {
            const string content = """
                {
                    "version": 3,
                    "net8.0": { "framework": "net8.0", "dependencies": {} },
                    "net8.0": null
                }
                """;

            PackagesLockFile lockFile = ParseWithSystemTextJson(content);

            Assert.Empty(lockFile.Targets);
        }

        [Fact]
        public void Read_VariousTargetFrameworksAndRuntimeIdentifiers_ParsedCorrectly()
        {
            // Arrange
            var lockFileContents =
@"{
    ""version"": 1,
    ""dependencies"": {
        "".NETFramework,Version=v4.7.2"": { },
        "".NETStandard,Version=v2.0"": { },
        "".NETCoreApp,Version=3.1"": { },
        "".NETCoreApp,Version=3.1/win-x64"": { },
        "".NETCoreApp,Version=5.0"": { },
        "".NETCoreApp,Version=5.0/win-x64"": { },
        ""net5.0-windows7.0"": { },
        ""net5.0-windows7.0/win-x64"": { },
        ""net6.0"": { },
        ""net6.0/win-x64"": { },
        ""net6.0-windows7.0"": { },
        ""net6.0-windows7.0/win-x64"": { },
    }
}";

            // Act
            var lockFile = PackagesLockFileFormat.Parse(lockFileContents, "In memory");

            // Assert
            Assert.Equal(12, lockFile.Targets.Count);

            Assert.Equal(FrameworkConstants.CommonFrameworks.Net472, lockFile.Targets[0].TargetFramework);
            Assert.Null(lockFile.Targets[0].RuntimeIdentifier);

            Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, lockFile.Targets[1].TargetFramework);
            Assert.Null(lockFile.Targets[1].RuntimeIdentifier);

            Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp31, lockFile.Targets[2].TargetFramework);
            Assert.Null(lockFile.Targets[2].RuntimeIdentifier);

            Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp31, lockFile.Targets[3].TargetFramework);
            Assert.Equal("win-x64", lockFile.Targets[3].RuntimeIdentifier);

            Assert.Equal(FrameworkConstants.CommonFrameworks.Net50, lockFile.Targets[4].TargetFramework);
            Assert.Null(lockFile.Targets[4].RuntimeIdentifier);

            Assert.Equal(FrameworkConstants.CommonFrameworks.Net50, lockFile.Targets[5].TargetFramework);
            Assert.Equal("win-x64", lockFile.Targets[5].RuntimeIdentifier);

            NuGetFramework net5win7 = NuGetFramework.Parse("net5.0-windows7.0");
            Assert.Equal(net5win7, lockFile.Targets[6].TargetFramework);
            Assert.Null(lockFile.Targets[6].RuntimeIdentifier);

            Assert.Equal(net5win7, lockFile.Targets[7].TargetFramework);
            Assert.Equal("win-x64", lockFile.Targets[7].RuntimeIdentifier);

            Assert.Equal(FrameworkConstants.CommonFrameworks.Net60, lockFile.Targets[8].TargetFramework);
            Assert.Null(lockFile.Targets[8].RuntimeIdentifier);

            Assert.Equal(FrameworkConstants.CommonFrameworks.Net60, lockFile.Targets[9].TargetFramework);
            Assert.Equal("win-x64", lockFile.Targets[9].RuntimeIdentifier);

            NuGetFramework net6win7 = NuGetFramework.Parse("net6.0-windows7.0");
            Assert.Equal(net6win7, lockFile.Targets[10].TargetFramework);
            Assert.Null(lockFile.Targets[10].RuntimeIdentifier);

            Assert.Equal(net6win7, lockFile.Targets[11].TargetFramework);
            Assert.Equal("win-x64", lockFile.Targets[11].RuntimeIdentifier);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadWithRuntimeGraph()
        {
            var nuGetLockFileContent = @"{
                ""version"": 1,
                ""dependencies"": {
                    "".NETFramework,Version=v4.5"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""requested"": ""[1.*, )"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""sbWWhjA2/cXJHBBKAVo3m2U0KxzNuW5dQANDwx8L96V+L6SML96cM/Myvmp6fiBqIDibvF6+Ss9YC+qqclrXnw=="",
                            ""dependencies"": {
                                 ""PackageB"": ""1.0.0""
                            }
                        },
                        ""PackageB"": {
                            ""type"": ""Transitive"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""Fjiywrwerewr4dgbdgbfgjkoiuiorwrwn24+8hjnnuerwrwsfsHYWD3HJYUI7NJHssxDFSFSFEWEW34DFDFCVsxv==""
                        }
                    },
                    "".NETFramework,Version=v4.5/win10-arm"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""requested"": ""[1.*, )"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""QuiokjhjA2/cXJHBBKAVo3m2U0KxzNuW5dQANDwx8L96V+L6SML96cM/Myvmp6fiBqIDibvF6+Ss9YC+qqcfwef=="",
                            ""dependencies"": {
                                 ""PackageB"": ""1.0.0"",
                                 ""runtime.win10-arm.PackageA"": ""1.0.0""
                            }
                        },
                        ""runtime.win10-arm.PackageA"": {
                            ""type"": ""Transitive"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""dfgdgdfIY434jhjkhkRARFSZSGFSDG423452bgdnuerwrwsfsHYWD3HJYUI7NJHssxDFSFSFEWEW34DFjkyuerd=="",
                        }
                    }
                }
            }";

            var lockFile = PackagesLockFileFormat.Parse(nuGetLockFileContent, "In Memory");

            Assert.Equal(2, lockFile.Targets.Count);

            var target = lockFile.Targets.First(t => !string.IsNullOrEmpty(t.RuntimeIdentifier));
            Assert.Equal(".NETFramework,Version=v4.5/win10-arm", target.Name);
            Assert.Equal(2, target.Dependencies.Count);

            Assert.Equal("PackageA", target.Dependencies[0].Id);
            Assert.Equal(PackageDependencyType.Direct, target.Dependencies[0].Type);
            Assert.Equal("[1.*, )", target.Dependencies[0].RequestedVersion.ToNormalizedString());
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion.ToNormalizedString());
            Assert.NotEmpty(target.Dependencies[0].ContentHash);
            Assert.Equal(2, target.Dependencies[0].Dependencies.Count);
            Assert.Equal("PackageB", target.Dependencies[0].Dependencies[0].Id);
            Assert.Equal("runtime.win10-arm.PackageA", target.Dependencies[0].Dependencies[1].Id);

            // Runtime graph will only have additional transitive dependenies which are not part of
            // original TFM graph
            Assert.Equal("runtime.win10-arm.PackageA", target.Dependencies[1].Id);
            Assert.Equal(PackageDependencyType.Transitive, target.Dependencies[1].Type);
            Assert.Null(target.Dependencies[1].RequestedVersion);
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion.ToNormalizedString());
            Assert.NotEmpty(target.Dependencies[1].ContentHash);
        }

        [Fact]
        public void PackagesLockFileFormat_Write()
        {
            var nuGetLockFileContent = @"{
                ""version"": 1,
                ""dependencies"": {
                    "".NETFramework,Version=v4.5"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""requested"": ""[1.*, )"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""sbWWhjA2/cXJHBBKAVo3m2U0KxzNuW5dQANDwx8L96V+L6SML96cM/Myvmp6fiBqIDibvF6+Ss9YC+qqclrXnw=="",
                            ""dependencies"": {
                                 ""PackageB"": ""1.0.0""
                            }
                        },
                        ""PackageB"": {
                            ""type"": ""Transitive"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""Fjiywrwerewr4dgbdgbfgjkoiuiorwrwn24+8hjnnuerwrwsfsHYWD3HJYUI7NJHssxDFSFSFEWEW34DFDFCVsxv=="",
                        }
                    }
                }
            }";

            var lockFile = PackagesLockFileFormat.Parse(nuGetLockFileContent, "In Memory");
            var output = PackagesLockFileFormat.Render(lockFile);
            var expected = JObject.Parse(nuGetLockFileContent).ToString();

            // Assert
            Assert.Equal(expected, output);
        }

        [Fact]
        public void PackagesLockFileFormat_WriteDuplicateTargets_Throws()
        {
            var lockFile = new PackagesLockFile(1);
            lockFile.Targets.Add(new PackagesLockFileTarget { TargetFramework = NuGetFramework.Parse("net8.0") });
            lockFile.Targets.Add(new PackagesLockFileTarget { TargetFramework = NuGetFramework.Parse("net8.0") });

            Assert.Throws<ArgumentException>(() => PackagesLockFileFormat.Render(lockFile));
        }

        [Fact]
        public void PackagesLockFileFormat_WriteVersion3TargetNamedVersion_Throws()
        {
            var lockFile = new PackagesLockFile(3);
            lockFile.Targets.Add(new PackagesLockFileTarget
            {
                TargetAlias = "version",
                TargetFramework = NuGetFramework.Parse("net8.0")
            });

            Assert.Throws<ArgumentException>(() => PackagesLockFileFormat.Render(lockFile));
        }

        [Fact]
        public void PackagesLockFileFormat_WriteVersion2_PreservesNewtonsoftOutput()
        {
            const string lockFileContent = """
                {
                  "version": 2,
                  "dependencies": {
                    "net8.0": {
                      "ProjectA": {
                        "type": "Project",
                        "resolved": "1.0.0",
                        "contentHash": "caf\u00e9<&",
                        "dependencies": {
                          "PackageB": "[1.0.0, 2.0.0)"
                        }
                      }
                    }
                  }
                }
                """;
            PackagesLockFile lockFile = PackagesLockFileFormat.Parse(lockFileContent, "In Memory");
            string expected = JObject.Parse(lockFileContent).ToString();

            string output = PackagesLockFileFormat.Render(lockFile);

            Assert.Equal(expected, output);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadVersion3WithAliases()
        {
            var lockFileContent = @"{
                ""version"": 3,
                ""netcoreapp10.0"": {
                    ""framework"": ""net10.0"",
                    ""dependencies"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""requested"": ""[1.0.0, )"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""hash1""
                        }
                    }
                },
                ""net10.0"": {
                    ""framework"": ""net10.0"",
                    ""dependencies"": {
                        ""PackageB"": {
                            ""type"": ""Direct"",
                            ""requested"": ""[2.0.0, )"",
                            ""resolved"": ""2.0.0"",
                            ""contentHash"": ""hash2""
                        }
                    }
                }
            }";

            var lockFile = PackagesLockFileFormat.Parse(lockFileContent, "In Memory");

            Assert.Equal(3, lockFile.Version);
            Assert.Equal(2, lockFile.Targets.Count);

            var target1 = lockFile.Targets[0];
            Assert.Equal("netcoreapp10.0", target1.TargetAlias);
            Assert.Equal(NuGetFramework.Parse("net10.0"), target1.TargetFramework);
            Assert.Equal("PackageA", target1.Dependencies[0].Id);

            var target2 = lockFile.Targets[1];
            Assert.Equal("net10.0", target2.TargetAlias);
            Assert.Equal(NuGetFramework.Parse("net10.0"), target2.TargetFramework);
            Assert.Equal("PackageB", target2.Dependencies[0].Id);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadVersion3WithAliasesAndRid()
        {
            var lockFileContent = @"{
                ""version"": 3,
                ""netcoreapp10.0"": {
                    ""framework"": ""net10.0"",
                    ""dependencies"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""hash1""
                        }
                    }
                },
                ""netcoreapp10.0/win-x64"": {
                    ""framework"": ""net10.0"",
                    ""dependencies"": {
                        ""PackageC"": {
                            ""type"": ""Transitive"",
                            ""resolved"": ""1.5.0"",
                            ""contentHash"": ""hash3""
                        }
                    }
                }
            }";

            var lockFile = PackagesLockFileFormat.Parse(lockFileContent, "In Memory");

            Assert.Equal(3, lockFile.Version);
            Assert.Equal(2, lockFile.Targets.Count);

            var target1 = lockFile.Targets[0];
            Assert.Equal("netcoreapp10.0", target1.TargetAlias);
            Assert.Equal(NuGetFramework.Parse("net10.0"), target1.TargetFramework);
            Assert.Null(target1.RuntimeIdentifier);

            var target2 = lockFile.Targets[1];
            Assert.Equal("netcoreapp10.0", target2.TargetAlias);
            Assert.Equal(NuGetFramework.Parse("net10.0"), target2.TargetFramework);
            Assert.Equal("win-x64", target2.RuntimeIdentifier);
        }

        [Fact]
        public void PackagesLockFileFormat_WriteVersion3WithAliases()
        {
            var lockFile = new PackagesLockFile(3);

            var target1 = new PackagesLockFileTarget
            {
                TargetFramework = NuGetFramework.Parse("net10.0"),
                TargetAlias = "netcoreapp10.0"
            };
            target1.Dependencies.Add(new LockFileDependency
            {
                Id = "PackageA",
                Type = PackageDependencyType.Direct,
                ResolvedVersion = NuGetVersion.Parse("1.0.0"),
                ContentHash = "hash1"
            });

            var target2 = new PackagesLockFileTarget
            {
                TargetFramework = NuGetFramework.Parse("net10.0"),
                TargetAlias = "net10.0"
            };
            target2.Dependencies.Add(new LockFileDependency
            {
                Id = "PackageB",
                Type = PackageDependencyType.Direct,
                ResolvedVersion = NuGetVersion.Parse("2.0.0"),
                ContentHash = "hash2"
            });

            lockFile.Targets.Add(target1);
            lockFile.Targets.Add(target2);

            var output = PackagesLockFileFormat.Render(lockFile);
            var json = JObject.Parse(output);

            Assert.Equal(3, (int)json["version"]!);
            Assert.True(json.ContainsKey("netcoreapp10.0"));
            Assert.True(json.ContainsKey("net10.0"));

            var target1Json = json["netcoreapp10.0"] as JObject;
            Assert.NotNull(target1Json);
            Assert.Equal("net10.0", (string)target1Json["framework"]!);
            Assert.NotNull(target1Json["dependencies"]);

            var target2Json = json["net10.0"] as JObject;
            Assert.NotNull(target2Json);
            Assert.Equal("net10.0", (string)target2Json["framework"]!);
            Assert.NotNull(target2Json["dependencies"]);
        }

        [Fact]
        public void PackagesLockFileFormat_RoundTripVersion3WithAliases()
        {
            var originalContent = @"{
                ""version"": 3,
                ""netcoreapp10.0"": {
                    ""framework"": ""net10.0"",
                    ""dependencies"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""hash1""
                        }
                    }
                },
                ""net10.0"": {
                    ""framework"": ""net10.0"",
                    ""dependencies"": {
                        ""PackageB"": {
                            ""type"": ""Direct"",
                            ""resolved"": ""2.0.0"",
                            ""contentHash"": ""hash2""
                        }
                    }
                }
            }";

            var lockFile = PackagesLockFileFormat.Parse(originalContent, "In Memory");
            var output = PackagesLockFileFormat.Render(lockFile);
            var expected = JObject.Parse(originalContent).ToString();

            Assert.Equal(expected, output);
        }

        [Fact]
        public void PackagesLockFileFormat_BackwardCompatibilityVersion1()
        {
            var v1Content = @"{
                ""version"": 1,
                ""dependencies"": {
                    "".NETFramework,Version=v4.7.2"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""hash1""
                        }
                    }
                }
            }";

            var lockFile = PackagesLockFileFormat.Parse(v1Content, "In Memory");

            Assert.Equal(1, lockFile.Version);
            Assert.Equal(1, lockFile.Targets.Count);
            Assert.Null(lockFile.Targets[0].TargetAlias);
            Assert.Equal(NuGetFramework.Parse(".NETFramework,Version=v4.7.2"), lockFile.Targets[0].TargetFramework);
        }

        [Fact]
        public void PackagesLockFileFormat_BackwardCompatibilityVersion2()
        {
            var v2Content = @"{
                ""version"": 2,
                ""dependencies"": {
                    ""net6.0"": {
                        ""PackageA"": {
                            ""type"": ""Direct"",
                            ""resolved"": ""1.0.0"",
                            ""contentHash"": ""hash1""
                        }
                    }
                }
            }";

            var lockFile = PackagesLockFileFormat.Parse(v2Content, "In Memory");

            Assert.Equal(2, lockFile.Version);
            Assert.Equal(1, lockFile.Targets.Count);
            Assert.Null(lockFile.Targets[0].TargetAlias);
        }

        [Fact]
        public void Read_WithTextReader_DisposesReader()
        {
            var reader = new StringReader("""{"version":1,"dependencies":{}}""");

#pragma warning disable CS0618 // Verify the obsolete API's ownership behavior.
            PackagesLockFileFormat.Read(reader, NullLogger.Instance, "In memory");
#pragma warning restore CS0618

            Assert.Throws<ObjectDisposedException>(() => reader.Read());
        }

        private static PackagesLockFile ParseWithSystemTextJson(string content)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            return PackagesLockFileFormat.ReadLockFileWithSystemTextJson(stream);
        }

    }
}
