// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
            Assert.Throws<JsonException>(() => ParseWithSystemTextJson(content));
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

        [Fact]
        public void PackagesLockFileFormat_ReadTextReaderWithNullLog_Throws()
        {
            var reader = new StringReader("""{"version":1,"dependencies":{}}""");

#pragma warning disable CS0618 // Testing the obsolete TextReader overload's argument validation.
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => PackagesLockFileFormat.Read(reader, log: null!, "packages.lock.json"));
#pragma warning restore CS0618

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

        [Fact]
        public void PackagesLockFileFormat_Write_ProducesIndentedDeterministicSchema()
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
            var output = RenderToString(lockFile);

            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement root = document.RootElement;
            Assert.Equal(1, root.GetProperty("version").GetInt32());
            JsonElement dependencies = root.GetProperty("dependencies");
            JsonElement target = dependencies.GetProperty(".NETFramework,Version=v4.5");
            Assert.Equal("Direct", target.GetProperty("PackageA").GetProperty("type").GetString());
            Assert.Equal("1.0.0", target.GetProperty("PackageB").GetProperty("resolved").GetString());

            Assert.Contains("\n  \"version\"", output);
            Assert.True(output.IndexOf("\"version\"", StringComparison.Ordinal) < output.IndexOf("\"dependencies\"", StringComparison.Ordinal));
            Assert.True(output.IndexOf("\"PackageA\"", StringComparison.Ordinal) < output.IndexOf("\"PackageB\"", StringComparison.Ordinal));
            Assert.Equal(output, RenderToString(lockFile));
        }

        [Fact]
        public void PackagesLockFileFormat_WritePublicApis_ProduceSameOutput()
        {
            const string lockFileContent = """
                {
                  "version": 2,
                  "dependencies": {
                    "net8.0": {
                      "ProjectA": {
                        "type": "Project",
                        "resolved": "1.0.0",
                        "contentHash": "café<&",
                        "dependencies": {
                          "PackageB": "[1.0.0, 2.0.0)"
                        }
                      }
                    }
                  }
                }
                """;
            PackagesLockFile lockFile = ParseWithSystemTextJson(lockFileContent);
            string filePath = Path.GetTempFileName();

            try
            {
                string renderedOutput = RenderToString(lockFile);

                var stream = new MemoryStream();
                PackagesLockFileFormat.Write(stream, lockFile);
                string streamOutput = Encoding.UTF8.GetString(stream.ToArray());

                PackagesLockFileFormat.Write(filePath, lockFile);
                string fileOutput = File.ReadAllText(filePath);

                Assert.Equal(renderedOutput, streamOutput);
                Assert.Equal(renderedOutput, fileOutput);
                Assert.False(stream.CanWrite);

                using JsonDocument document = JsonDocument.Parse(renderedOutput);
                JsonElement project = document.RootElement
                    .GetProperty("dependencies")
                    .GetProperty("net8.0")
                    .GetProperty("ProjectA");
                Assert.Equal("Project", project.GetProperty("type").GetString());
                Assert.Equal("café<&", project.GetProperty("contentHash").GetString());
                Assert.Equal("[1.0.0, 2.0.0)", project.GetProperty("dependencies").GetProperty("PackageB").GetString());
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [Fact]
        public void PackagesLockFileFormat_WriteLargeLockFile_WritesIncrementallyAndDisposesDestination()
        {
            PackagesLockFile lockFile = CreateLargeLockFile(packageCount: 1_000);
            var stream = new RecordingWriteStream();

            PackagesLockFileFormat.Write(stream, lockFile);

            Assert.True(stream.IsDisposed);
            Assert.True(stream.WriteCount > 1);
            Assert.True(stream.BytesWritten > 16 * 1024);

            using JsonDocument document = JsonDocument.Parse(stream.ToArray());
            Assert.Equal(
                1_000,
                document.RootElement
                    .GetProperty("net10.0")
                    .GetProperty("dependencies")
                    .EnumerateObject()
                    .Count());
        }

        [Fact]
        public void PackagesLockFileFormat_WriteWhenDestinationWriteThrows_DisposesDestination()
        {
            PackagesLockFile lockFile = CreateLargeLockFile(packageCount: 1_000);
            var stream = new RecordingWriteStream(throwOnWrite: true);

            IOException exception = Assert.Throws<IOException>(
                () => PackagesLockFileFormat.Write(stream, lockFile));

            Assert.Equal("The destination cannot be written.", exception.Message);
            Assert.True(stream.IsDisposed);
        }

        [Fact]
        public void PackagesLockFileFormat_WriteUnicodeAndSpecialCharacters_PreserveModelSemantics()
        {
            const string value = "emoji-\U0001F600-Latin-café-Ångström-html-<>&'-quote-\"-backslash-\\";
            PackagesLockFile lockFile = CreateLockFileWithValue(value);

            string output = RenderToString(lockFile);

            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement target = document.RootElement.GetProperty($"target-{value}");
            JsonElement package = target.GetProperty("dependencies").GetProperty($"package-{value}");
            Assert.Equal(value, package.GetProperty("contentHash").GetString());
            Assert.Equal(
                "(, )",
                package.GetProperty("dependencies").GetProperty($"dependency-{value}").GetString());
        }

        [Fact]
        public void PackagesLockFileFormat_WriteDuplicateTargets_DoesNotWriteToStream()
        {
            var lockFile = new PackagesLockFile(1);
            lockFile.Targets.Add(new PackagesLockFileTarget { TargetFramework = NuGetFramework.Parse("net8.0") });
            lockFile.Targets.Add(new PackagesLockFileTarget { TargetFramework = NuGetFramework.Parse("net8.0") });

            AssertDuplicateWriteIsAtomic(lockFile, "net8.0");
        }

        [Fact]
        public void PackagesLockFileFormat_WriteDuplicatePackages_DoesNotWriteToStream()
        {
            var lockFile = new PackagesLockFile(1);
            var target = new PackagesLockFileTarget { TargetFramework = NuGetFramework.Parse("net8.0") };
            target.Dependencies.Add(new LockFileDependency { Id = "PackageA" });
            target.Dependencies.Add(new LockFileDependency { Id = "PackageA" });
            lockFile.Targets.Add(target);

            AssertDuplicateWriteIsAtomic(lockFile, "PackageA");
        }

        [Fact]
        public void PackagesLockFileFormat_WriteDuplicateDependencies_DoesNotWriteToStream()
        {
            var lockFile = new PackagesLockFile(1);
            var target = new PackagesLockFileTarget { TargetFramework = NuGetFramework.Parse("net8.0") };
            var package = new LockFileDependency { Id = "PackageA" };
            package.Dependencies.Add(new NuGet.Packaging.Core.PackageDependency("PackageB"));
            package.Dependencies.Add(new NuGet.Packaging.Core.PackageDependency("PackageB"));
            target.Dependencies.Add(package);
            lockFile.Targets.Add(target);

            AssertDuplicateWriteIsAtomic(lockFile, "PackageB");
        }

        [Fact]
        public void PackagesLockFileFormat_WriteVersion3TargetNamedVersion_DoesNotWriteToStream()
        {
            var lockFile = new PackagesLockFile(3);
            lockFile.Targets.Add(new PackagesLockFileTarget
            {
                TargetAlias = "version",
                TargetFramework = NuGetFramework.Parse("net8.0")
            });

            AssertDuplicateWriteIsAtomic(lockFile, "version");
        }

        [Fact]
        public void PackagesLockFileFormat_WriteNullPackageNameAfterLargeContent_DoesNotWriteToStream()
        {
            var lockFile = new PackagesLockFile(1);
            var target = new PackagesLockFileTarget { TargetFramework = NuGetFramework.Parse("net8.0") };
            target.Dependencies.Add(new LockFileDependency
            {
                Id = "LargePackage",
                ContentHash = new string('a', 100_000)
            });
            target.Dependencies.Add(new LockFileDependency { Id = null! });
            lockFile.Targets.Add(target);

            AssertNullNameWriteIsAtomic(lockFile);
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

            var output = RenderToString(lockFile);
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
        public void PackagesLockFileFormat_WriteVersion3WithAliases_IsDeterministicAndReadable()
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

            PackagesLockFile lockFile = ParseWithSystemTextJson(originalContent);
            var output = RenderToString(lockFile);

            Assert.Equal(output, RenderToString(lockFile));

            PackagesLockFile roundTripped = ParseWithSystemTextJson(output);
            Assert.Equal(3, roundTripped.Version);
            Assert.Equal(2, roundTripped.Targets.Count);
            Assert.Equal("netcoreapp10.0", roundTripped.Targets[0].TargetAlias);
            Assert.Equal("PackageA", roundTripped.Targets[0].Dependencies[0].Id);
            Assert.Equal("net10.0", roundTripped.Targets[1].TargetAlias);
            Assert.Equal("PackageB", roundTripped.Targets[1].Dependencies[0].Id);
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

        private static string RenderToString(PackagesLockFile lockFile)
        {
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

        private static void AssertDuplicateWriteIsAtomic(PackagesLockFile lockFile, string propertyName)
        {
            var stream = new MemoryStream();

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => PackagesLockFileFormat.Write(stream, lockFile));

            Assert.Contains(propertyName, exception.Message);
            Assert.Empty(stream.ToArray());
            Assert.False(stream.CanWrite);
        }

        private static void AssertNullNameWriteIsAtomic(PackagesLockFile lockFile)
        {
            var stream = new MemoryStream();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => PackagesLockFileFormat.Write(stream, lockFile));

            Assert.Equal("name", exception.ParamName);
            Assert.Empty(stream.ToArray());
            Assert.False(stream.CanWrite);
        }

        private static PackagesLockFile CreateLockFileWithValue(string value)
        {
            var lockFile = new PackagesLockFile(3);
            var target = new PackagesLockFileTarget
            {
                TargetAlias = $"target-{value}",
                TargetFramework = NuGetFramework.Parse("net8.0")
            };
            var package = new LockFileDependency
            {
                Id = $"package-{value}",
                Type = PackageDependencyType.Direct,
                ContentHash = value
            };
            package.Dependencies.Add(new NuGet.Packaging.Core.PackageDependency($"dependency-{value}"));
            target.Dependencies.Add(package);
            lockFile.Targets.Add(target);
            return lockFile;
        }

        private static PackagesLockFile CreateLargeLockFile(int packageCount)
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

        private sealed class RecordingWriteStream : MemoryStream
        {
            private readonly bool _throwOnWrite;
            private long _bytesWritten;

            internal RecordingWriteStream(bool throwOnWrite = false)
            {
                _throwOnWrite = throwOnWrite;
            }

            internal long BytesWritten => _bytesWritten;

            internal bool IsDisposed { get; private set; }

            internal int WriteCount { get; private set; }

            public override void Write(byte[] buffer, int offset, int count)
            {
                BeforeWrite();
                base.Write(buffer, offset, count);
                _bytesWritten += count;
            }

#if NET5_0_OR_GREATER
            public override void Write(ReadOnlySpan<byte> buffer)
            {
                BeforeWrite();
                base.Write(buffer);
                _bytesWritten += buffer.Length;
            }
#endif

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }

            private void BeforeWrite()
            {
                WriteCount++;
                if (_throwOnWrite)
                {
                    throw new IOException("The destination cannot be written.");
                }
            }
        }
    }
}
