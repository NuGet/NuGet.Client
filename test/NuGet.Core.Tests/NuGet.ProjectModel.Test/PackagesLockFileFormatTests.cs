// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Test.Utility;
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

            PackagesLockFile lockFile = ParseWithSystemTextJson(nuGetLockFileContent);

            Assert.Equal(1, lockFile.Targets.Count);

            var target = lockFile.Targets.First();
            Assert.Equal(".NETFramework,Version=v4.5", target.Name);
            Assert.Equal(2, target.Dependencies.Count);

            Assert.Equal("PackageA", target.Dependencies[0].Id);
            Assert.Equal(PackageDependencyType.Direct, target.Dependencies[0].Type);
            Assert.Equal("[1.*, )", target.Dependencies[0].RequestedVersion?.ToNormalizedString());
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion?.ToNormalizedString());
            Assert.False(string.IsNullOrEmpty(target.Dependencies[0].ContentHash));
            Assert.Equal(1, target.Dependencies[0].Dependencies.Count);
            Assert.Equal("PackageB", target.Dependencies[0].Dependencies[0].Id);


            Assert.Equal("PackageB", target.Dependencies[1].Id);
            Assert.Equal(PackageDependencyType.Transitive, target.Dependencies[1].Type);
            Assert.Null(target.Dependencies[1].RequestedVersion);
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion?.ToNormalizedString());
            Assert.False(string.IsNullOrEmpty(target.Dependencies[1].ContentHash));
        }

        [Fact]
        public void PackagesLockFileFormat_ReadWithCommentsAndTrailingCommas()
        {
            var lockFileContent = """
                {
                    // NuGet writes the version property first.
                    "version": 1,
                    "dependencies": {
                        "net8.0": {
                            "PackageA": {
                                "type": "Direct",
                                "resolved": "1.0.0",
                                "dependencies": {
                                    "PackageB": null,
                                },
                            },
                        },
                    },
                }
                """;

            PackagesLockFile lockFile = ParseWithSystemTextJson(lockFileContent);

            Assert.Equal(1, lockFile.Version);
            var target = Assert.Single(lockFile.Targets);
            Assert.Equal(NuGetFramework.Parse("net8.0"), target.TargetFramework);
            var package = Assert.Single(target.Dependencies);
            Assert.Equal("PackageA", package.Id);
            Assert.Equal(VersionRange.All, Assert.Single(package.Dependencies).VersionRange);
        }

        [Theory]
        [InlineData("""{"dependencies":{},"version":1}""")]
        [InlineData("""{"net8.0":{"framework":"net8.0","dependencies":{}},"version":3}""")]
        public void PackagesLockFileFormat_ReadVersionNotFirst_Throws(string content)
        {
            JsonException exception = Assert.Throws<JsonException>(() => ParseWithSystemTextJson(content));

            Assert.Equal(Strings.PackagesLockFile_ExpectedVersionFirst, exception.Message);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadVersionNotFirst_LogsDiagnosticAndDisposesStream()
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{"dependencies":{},"version":1}"""));
            var logger = new TestLogger();

            PackagesLockFile lockFile = PackagesLockFileFormat.Read(stream, logger, "invalid-version.lock.json");

            Assert.Equal(int.MinValue, lockFile.Version);
            Assert.False(stream.CanRead);
            string message = Assert.Single(logger.InformationMessages);
            Assert.Contains("invalid-version.lock.json", message);
            Assert.Contains(Strings.PackagesLockFile_ExpectedVersionFirst, message);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void PackagesLockFileFormat_ReadUtf8Stream_ParsesAndDisposesSource(
            bool includeBom,
            bool useNonSeekableStream)
        {
            const string content = """{"version":1,"dependencies":{"net10.0":{}}}""";
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            byte[] bytes;
            if (includeBom)
            {
                byte[] preamble = Encoding.UTF8.GetPreamble();
                bytes = new byte[preamble.Length + contentBytes.Length];
                Array.Copy(preamble, bytes, preamble.Length);
                Array.Copy(contentBytes, 0, bytes, preamble.Length, contentBytes.Length);
            }
            else
            {
                bytes = contentBytes;
            }

            Stream source = new MemoryStream(bytes);
            if (useNonSeekableStream)
            {
                source = new ChunkedReadStream(source, maxBytesPerRead: 1);
            }

            PackagesLockFile lockFile = PackagesLockFileFormat.Read(source, NullLogger.Instance, "encoded.lock.json");

            Assert.Equal(1, lockFile.Version);
            Assert.Equal(NuGetFramework.Parse("net10.0"), Assert.Single(lockFile.Targets).TargetFramework);
            Assert.False(source.CanRead);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadLockFileWithSystemTextJsonLeadingTriviaLargerThanBuffer_ParsesLockFile()
        {
            string content = new string(' ', 20_000) + """{"version":1,"dependencies":{}}""";

            PackagesLockFile lockFile = ParseWithSystemTextJson(content);

            Assert.Equal(1, lockFile.Version);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadMalformedJson_LogsDiagnosticAndDisposesStream()
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{"version":1"""));
            var logger = new TestLogger();

            PackagesLockFile lockFile = PackagesLockFileFormat.Read(stream, logger, "broken.lock.json");

            Assert.Equal(int.MinValue, lockFile.Version);
            Assert.False(stream.CanRead);
            Assert.Contains("broken.lock.json", Assert.Single(logger.InformationMessages));
        }

        [Fact]
        public void PackagesLockFileFormat_ReadWhenInitialReadThrows_ReturnsFallbackAndDisposesSource()
        {
            var stream = new ThrowingReadStream();
            var logger = new TestLogger();

            PackagesLockFile lockFile = PackagesLockFileFormat.Read(stream, logger, "unreadable.lock.json");

            Assert.Equal(int.MinValue, lockFile.Version);
            Assert.True(stream.IsDisposed);
            Assert.Contains("unreadable.lock.json", Assert.Single(logger.InformationMessages));
        }

        [Fact]
        public void PackagesLockFileFormat_ReadStreamWithNullLog_Throws()
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("""{"version":1,"dependencies":{}}"""));

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => PackagesLockFileFormat.Read(stream, log: null!, "packages.lock.json"));

            Assert.Equal("log", exception.ParamName);
        }

        [Theory]
        [InlineData("""{"version":1.0,"dependencies":{}}""")]
        [InlineData("""{"version":"1","dependencies":{}}""")]
        [InlineData("""{"version":true,"dependencies":{}}""")]
        [InlineData("""{"version":{},"dependencies":{}}""")]
        [InlineData("""{"version":[],"dependencies":{}}""")]
        public void PackagesLockFileFormat_ReadNonIntegerVersion_Throws(string content)
        {
            Assert.Throws<JsonException>(() => ParseWithSystemTextJson(content));
        }

        [Theory]
        [InlineData("""{"version":1}""")]
        [InlineData("""{"version":1,"dependencies":[]}""")]
        [InlineData("""{"version":1,"dependencies":{"net10.0":[]}}""")]
        [InlineData("""{"version":1,"dependencies":{"net10.0":{"PackageA":[]}}}""")]
        [InlineData("""{"version":1,"dependencies":{"net10.0":{"PackageA":{"dependencies":[]}}}}""")]
        [InlineData("""{"version":3,"net10.0":[]}""")]
        [InlineData("""{"version":3,"net10.0":{"dependencies":{}}}""")]
        [InlineData("""{"version":3,"net10.0":{"framework":"net10.0"}}""")]
        [InlineData("""{"version":3,"net10.0":{"framework":1,"dependencies":{}}}""")]
        [InlineData("""{"version":3,"net10.0":{"framework":"net10.0","dependencies":[]}}""")]
        public void PackagesLockFileFormat_ReadInvalidKnownSchema_Throws(string content)
        {
            Assert.Throws<JsonException>(() => ParseWithSystemTextJson(content));
        }

        [Theory]
        [InlineData("requested", "1.0")]
        [InlineData("resolved", "1.0")]
        [InlineData("contentHash", "1.0")]
        [InlineData("type", "1.0")]
        public void PackagesLockFileFormat_ReadNonStringDependencyProperty_ReturnsInvalidFile(
            string propertyName,
            string propertyValue)
        {
            string content = $$"""
                {
                  "version": 1,
                  "dependencies": {
                    "net10.0": {
                      "PackageA": {
                        "{{propertyName}}": {{propertyValue}}
                      }
                    }
                  }
                }
                """;

            PackagesLockFile lockFile = PackagesLockFileFormat.Read(
                new MemoryStream(Encoding.UTF8.GetBytes(content)),
                NullLogger.Instance,
                "invalid.lock.json");

            Assert.Equal(int.MinValue, lockFile.Version);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadTrailingContent_ReturnsInvalidFile()
        {
            const string content = """
                {"version":1,"dependencies":{}}
                {"version":2,"dependencies":{}}
                """;

            PackagesLockFile lockFile = PackagesLockFileFormat.Read(
                new MemoryStream(Encoding.UTF8.GetBytes(content)),
                NullLogger.Instance,
                "trailing.lock.json");

            Assert.Equal(int.MinValue, lockFile.Version);
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
            PackagesLockFile lockFile = ParseWithSystemTextJson(lockFileContents);

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

            PackagesLockFile lockFile = ParseWithSystemTextJson(nuGetLockFileContent);

            Assert.Equal(2, lockFile.Targets.Count);

            var target = lockFile.Targets.First(t => !string.IsNullOrEmpty(t.RuntimeIdentifier));
            Assert.Equal(".NETFramework,Version=v4.5/win10-arm", target.Name);
            Assert.Equal(2, target.Dependencies.Count);

            Assert.Equal("PackageA", target.Dependencies[0].Id);
            Assert.Equal(PackageDependencyType.Direct, target.Dependencies[0].Type);
            Assert.Equal("[1.*, )", target.Dependencies[0].RequestedVersion?.ToNormalizedString());
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion?.ToNormalizedString());
            Assert.False(string.IsNullOrEmpty(target.Dependencies[0].ContentHash));
            Assert.Equal(2, target.Dependencies[0].Dependencies.Count);
            Assert.Equal("PackageB", target.Dependencies[0].Dependencies[0].Id);
            Assert.Equal("runtime.win10-arm.PackageA", target.Dependencies[0].Dependencies[1].Id);

            // Runtime graph will only have additional transitive dependenies which are not part of
            // original TFM graph
            Assert.Equal("runtime.win10-arm.PackageA", target.Dependencies[1].Id);
            Assert.Equal(PackageDependencyType.Transitive, target.Dependencies[1].Type);
            Assert.Null(target.Dependencies[1].RequestedVersion);
            Assert.Equal("1.0.0", target.Dependencies[0].ResolvedVersion?.ToNormalizedString());
            Assert.False(string.IsNullOrEmpty(target.Dependencies[1].ContentHash));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PackagesLockFileFormat_Write_ProducesIndentedDeterministicSchema(bool useStream)
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

            PackagesLockFile lockFile = ParseWithSystemTextJson(nuGetLockFileContent);
            var output = RenderToString(lockFile, useStream);

            Assert.True(JToken.DeepEquals(JToken.Parse(nuGetLockFileContent), JToken.Parse(output)));
            Assert.Contains("\n  \"version\"", output);
            Assert.True(output.IndexOf("\"version\"", StringComparison.Ordinal) < output.IndexOf("\"dependencies\"", StringComparison.Ordinal));
            Assert.True(output.IndexOf("\"PackageA\"", StringComparison.Ordinal) < output.IndexOf("\"PackageB\"", StringComparison.Ordinal));
        }

        [Fact]
        public void PackagesLockFileFormat_WriteLargeLockFile_PreservesOutputAcrossBufferBoundaries()
        {
            PackagesLockFile lockFile = CreateLockFile(packageCount: 1_000);
            var stream = new MemoryStream();

            PackagesLockFileFormat.Write(stream, lockFile);

            byte[] output = stream.ToArray();
            Assert.True(output.Length > 16 * 1024);
            Assert.Equal(RenderToString(lockFile), Encoding.UTF8.GetString(output));
            Assert.False(stream.CanWrite);
        }

        [Fact]
        public void PackagesLockFileFormat_WriteFile_ReplacesDestinationWithoutTemporaryFiles()
        {
            using var directory = TestDirectory.Create();
            string filePath = Path.Combine(directory.Path, PackagesLockFileFormat.LockFileName);
            File.WriteAllText(filePath, "original content");
            PackagesLockFile lockFile = CreateLockFile();

            PackagesLockFileFormat.Write(filePath, lockFile);

            Assert.Equal(RenderToString(lockFile), File.ReadAllText(filePath));
            Assert.Equal(new[] { filePath }, Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PackagesLockFileFormat_WriteFileValidationFailure_PreservesDestinationAndDeletesTemporaryFile(
            bool fileExists)
        {
            using var directory = TestDirectory.Create();
            string filePath = Path.Combine(directory.Path, "nested", PackagesLockFileFormat.LockFileName);
            byte[] originalContent = Encoding.UTF8.GetBytes("""{"version":1,"dependencies":{}}""");
            if (fileExists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                File.WriteAllBytes(filePath, originalContent);
            }

            PackagesLockFile lockFile = CreateLockFile(packageCount: 1_000);
            PackagesLockFileTarget target = lockFile.Targets[0];
            target.Dependencies.Add(new LockFileDependency
            {
                Id = target.Dependencies[0].Id
            });

            Assert.Throws<ArgumentException>(() => PackagesLockFileFormat.Write(filePath, lockFile));

            if (fileExists)
            {
                Assert.Equal(originalContent, File.ReadAllBytes(filePath));
                Assert.Equal(new[] { filePath }, Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories));
            }
            else
            {
                Assert.Empty(Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories));
            }
        }

        [Theory]
        [InlineData(1, "target")]
        [InlineData(3, "target")]
        [InlineData(1, "package")]
        [InlineData(1, "dependency")]
        [InlineData(3, "version")]
        public void PackagesLockFileFormat_WriteDuplicatePropertyName_Throws(int version, string scope)
        {
            PackagesLockFile lockFile = CreateLockFile();
            lockFile.Version = version;
            PackagesLockFileTarget target = lockFile.Targets[0];
            string propertyName;
            switch (scope)
            {
                case "target":
                    lockFile.Targets.Add(target);
                    propertyName = target.Name;
                    break;
                case "package":
                    target.Dependencies.Add(target.Dependencies[0]);
                    propertyName = target.Dependencies[0].Id;
                    break;
                case "dependency":
                    propertyName = "PackageB";
                    target.Dependencies[0].Dependencies.Add(new NuGet.Packaging.Core.PackageDependency(propertyName));
                    target.Dependencies[0].Dependencies.Add(new NuGet.Packaging.Core.PackageDependency(propertyName));
                    break;
                default:
                    propertyName = "version";
                    target.TargetAlias = propertyName;
                    break;
            }

            ArgumentException exception = Assert.Throws<ArgumentException>(() => RenderToString(lockFile));

            Assert.Contains(
                string.Format(CultureInfo.CurrentCulture, Strings.PackagesLockFile_DuplicatePropertyName, propertyName),
                exception.Message);
        }

        [Fact]
        public void PackagesLockFileFormat_WriteNullPropertyName_Throws()
        {
            PackagesLockFile lockFile = CreateLockFile();
            lockFile.Targets[0].Dependencies[0].Id = null!;

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => RenderToString(lockFile));

            Assert.Contains(Strings.PackagesLockFile_NullPropertyName, exception.Message);
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

            PackagesLockFile lockFile = ParseWithSystemTextJson(lockFileContent);

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

            PackagesLockFile lockFile = ParseWithSystemTextJson(lockFileContent);

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PackagesLockFileFormat_WriteVersion3WithAliases_PreservesSchema(bool useStream)
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
                        ""PackageB\u00e9\u0022\u005c<&\uD83D\uDE00"": {
                            ""type"": ""Direct"",
                            ""resolved"": ""2.0.0"",
                            ""contentHash"": ""hash2-\u00e9<&\uD83D\uDE00""
                        }
                    }
                }
            }";

            PackagesLockFile lockFile = ParseWithSystemTextJson(originalContent);
            var output = RenderToString(lockFile, useStream);

            Assert.True(JToken.DeepEquals(JToken.Parse(originalContent), JToken.Parse(output)));
        }

        [Fact]
        public void PackagesLockFileFormat_ReadVersion1()
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

            PackagesLockFile lockFile = ParseWithSystemTextJson(v1Content);

            Assert.Equal(1, lockFile.Version);
            Assert.Equal(1, lockFile.Targets.Count);
            Assert.Null(lockFile.Targets[0].TargetAlias);
            Assert.Equal(NuGetFramework.Parse(".NETFramework,Version=v4.7.2"), lockFile.Targets[0].TargetFramework);
        }

        [Fact]
        public void PackagesLockFileFormat_ReadVersion2()
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

            PackagesLockFile lockFile = ParseWithSystemTextJson(v2Content);

            Assert.Equal(2, lockFile.Version);
            Assert.Equal(1, lockFile.Targets.Count);
            Assert.Null(lockFile.Targets[0].TargetAlias);
        }

        private static PackagesLockFile ParseWithSystemTextJson(string content)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            return PackagesLockFileFormat.ReadLockFile(stream);
        }

        private static string RenderToString(PackagesLockFile lockFile, bool useStream = false)
        {
            if (useStream)
            {
                var stream = new MemoryStream();
                PackagesLockFileFormat.Write(stream, lockFile);
                return Encoding.UTF8.GetString(stream.ToArray());
            }

#pragma warning disable CS0618 // Testing the obsolete whole-file string rendering API.
            return PackagesLockFileFormat.Render(lockFile);
#pragma warning restore CS0618
        }

        private sealed class ChunkedReadStream : Stream
        {
            private readonly Stream _stream;
            private readonly int _maxBytesPerRead;

            internal ChunkedReadStream(Stream stream, int maxBytesPerRead)
            {
                _stream = stream;
                _maxBytesPerRead = maxBytesPerRead;
            }

            public override bool CanRead => _stream.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _stream.Read(buffer, offset, Math.Min(count, _maxBytesPerRead));
            }

            public override void Flush()
            {
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _stream.Dispose();
                }

                base.Dispose(disposing);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private sealed class ThrowingReadStream : Stream
        {
            internal bool IsDisposed { get; private set; }

            public override bool CanRead => !IsDisposed;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new IOException("The source cannot be read.");
            }

            public override void Flush()
            {
            }

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private static PackagesLockFile CreateLockFile(int packageCount = 1)
        {
            var lockFile = new PackagesLockFile(3);
            var target = new PackagesLockFileTarget
            {
                TargetAlias = "net10.0",
                TargetFramework = NuGetFramework.Parse("net10.0")
            };
            NuGetVersion version = NuGetVersion.Parse("1.0.0");

            for (int index = 0; index < packageCount; index++)
            {
                target.Dependencies.Add(new LockFileDependency
                {
                    Id = $"Package{index}",
                    Type = PackageDependencyType.Transitive,
                    ResolvedVersion = version,
                    ContentHash = $"sha512-package-{index}"
                });
            }

            lockFile.Targets.Add(target);
            return lockFile;
        }
    }
}
