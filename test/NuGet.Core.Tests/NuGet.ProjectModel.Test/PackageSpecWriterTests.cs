// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Internal.NuGet.Testing.SignedPackages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class PackageSpecWriterTests
    {
        private static readonly PackageSpec EmptyPackageSpec = JsonPackageSpecReader.GetPackageSpec("{}", null, null);

        [Theory]
        [InlineData(false, 0)]
        [InlineData(false, 1023)]
        [InlineData(false, 1024)]
        [InlineData(false, 4095)]
        [InlineData(false, 4096)]
        [InlineData(false, 8192)]
        [InlineData(true, 0)]
        [InlineData(true, 1023)]
        [InlineData(true, 1024)]
        [InlineData(true, 4095)]
        [InlineData(true, 4096)]
        [InlineData(true, 8192)]
        public void Write_WithCachedPackageSections_PreservesHashAcrossBufferBoundaries(bool useLegacyHashFunction, int prefixLength)
        {
            string specialName = "Quote\"Backslash\\Newline\nTab\t\u00e9\u6f22\u2028\u2029\ud83d\ude00" + new string('x', 2500);
            var framework = new TargetFrameworkInformation
            {
                FrameworkName = NuGetFramework.Parse("net10.0"),
                TargetAlias = "net10.0",
                CentralPackageVersions = new Dictionary<string, CentralPackageVersion>(StringComparer.Ordinal)
                {
                    ["FirstKey"] = new CentralPackageVersion(specialName, VersionRange.Parse("[1.0,2.0)")),
                    ["SecondKey"] = new CentralPackageVersion("AnotherPackage", new VersionRange(new NuGetVersion(1, 0, 0)))
                },
                PackagesToPrune = new Dictionary<string, PrunePackageReference>(StringComparer.Ordinal)
                {
                    [specialName] = PrunePackageReference.Create("DifferentName", "10.0.0"),
                    ["AnotherPackage"] = PrunePackageReference.Create("IgnoredName", "9.0.0")
                }
            };
            var spec = new PackageSpec([framework]);
            string originalJson = GetJsonString(spec);
            string prefix = new string('p', prefixLength);

            foreach (bool useLegacyWriter in new[] { false, true })
            {
                string expected = GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter, prefix, useCache: false);
                Assert.Equal(expected, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter, prefix, useCache: true));
                Assert.Equal(expected, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter, prefix, useCache: true));
                Assert.Equal(expected, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter, prefix, useCache: true));
                Assert.Equal(expected, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter, prefix, useCache: true));
            }
            Assert.Equal(originalJson, GetJsonString(spec));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Write_WithInvalidUnicodeInCachedSection_PreservesEncodingFailure(bool useLegacyHashFunction)
        {
            var framework = new TargetFrameworkInformation
            {
                FrameworkName = NuGetFramework.Parse("net10.0"),
                PackagesToPrune = new Dictionary<string, PrunePackageReference>
                {
                    ["Invalid\ud800"] = PrunePackageReference.Create("Pruned", "10.0.0")
                }
            };
            var spec = new PackageSpec([framework]);

            Assert.Throws<EncoderFallbackException>(() =>
                GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: false));
            Assert.Throws<EncoderFallbackException>(() =>
                GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
            Assert.Throws<EncoderFallbackException>(() =>
                GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
            Assert.Throws<EncoderFallbackException>(() =>
                GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
            Assert.Throws<EncoderFallbackException>(() =>
                GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Write_WithConcurrentCachedSectionReaders_PreservesHash(bool useLegacyHashFunction)
        {
            var framework = new TargetFrameworkInformation
            {
                FrameworkName = NuGetFramework.Parse("net10.0"),
                CentralPackageVersions = new Dictionary<string, CentralPackageVersion>
                {
                    ["Package"] = new CentralPackageVersion("Package", VersionRange.Parse("1.0.0"))
                },
                PackagesToPrune = new Dictionary<string, PrunePackageReference>
                {
                    ["Pruned"] = PrunePackageReference.Create("Pruned", "10.0.0")
                }
            };
            var spec = new PackageSpec([framework]);
            string expected = GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: false);

            Parallel.For(0, 32, _ =>
            {
                Assert.Equal(expected, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
            });
        }

        [Theory]
        [InlineData("add", false)]
        [InlineData("add", true)]
        [InlineData("remove", false)]
        [InlineData("remove", true)]
        [InlineData("replace", false)]
        [InlineData("replace", true)]
        [InlineData("case", false)]
        [InlineData("case", true)]
        [InlineData("reorder", false)]
        [InlineData("reorder", true)]
        [InlineData("clear", false)]
        [InlineData("clear", true)]
        public void Write_AfterWarmPackageSectionsChange_ReflectsCurrentContents(string change, bool changePruning)
        {
            foreach (bool useLegacyHashFunction in new[] { false, true })
            {
                var central = new Dictionary<string, CentralPackageVersion>(StringComparer.Ordinal)
                {
                    ["z"] = new CentralPackageVersion("z", VersionRange.Parse("1.0.0")),
                    ["a"] = new CentralPackageVersion("a", VersionRange.Parse("1.0.0")),
                    ["A"] = new CentralPackageVersion("A", VersionRange.Parse("1.0.0"))
                };
                var pruning = new Dictionary<string, PrunePackageReference>(StringComparer.Ordinal)
                {
                    ["z"] = PrunePackageReference.Create("z", "1.0.0"),
                    ["a"] = PrunePackageReference.Create("a", "1.0.0"),
                    ["A"] = PrunePackageReference.Create("A", "1.0.0")
                };
                var framework = new TargetFrameworkInformation
                {
                    FrameworkName = NuGetFramework.Parse("net10.0"),
                    CentralPackageVersions = central,
                    PackagesToPrune = pruning
                };
                var spec = new PackageSpec([framework]);
                string original = GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true);
                Assert.Equal(original, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
                Assert.Equal(original, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
                Assert.Equal(original, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));

                if (changePruning)
                {
                    MutatePackageReferences(pruning, change, static name => PrunePackageReference.Create(name, "2.0.0"));
                }
                else
                {
                    MutatePackageReferences(central, change, static name => new CentralPackageVersion(name, VersionRange.Parse("2.0.0")));
                }

                var freshSpec = new PackageSpec([new TargetFrameworkInformation(framework)]);
                string expected = GetPackageSpecHash(freshSpec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: false);
                Assert.Equal(expected, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
                Assert.Equal(expected, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
                Assert.Equal(expected, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
                Assert.Equal(expected, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void GetHash_WithEquivalentSections_SeedsExistingContentBeforeProjectOrder(bool useLegacyHashFunction, bool changeOriginalInput)
        {
            TargetFrameworkInformation existing = CreateSharingFramework("1.0.0", out var central, out var pruning);
            var existingSpec = new PackageSpec([existing])
            {
                Name = "SHARED",
                RestoreMetadata = new ProjectRestoreMetadata { ProjectUniqueName = "SHARED" }
            };
            GetPackageSpecHash(existingSpec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true);
            GetPackageSpecHash(existingSpec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true);
            GetPackageSpecHash(existingSpec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true);
            SerializedPackageReferences<CentralPackageVersion> cachedCentral = existing.GetCachedCentralPackageVersions();
            SerializedPackageReferences<PrunePackageReference> cachedPruning = existing.GetCachedPackagesToPrune();
            Assert.NotNull(cachedCentral);
            Assert.NotNull(cachedPruning);

            if (changeOriginalInput)
            {
                central["CentralKey"] = new CentralPackageVersion("Package", VersionRange.Parse("2.0.0"));
                pruning["Pruned"] = PrunePackageReference.Create("IgnoredName", "2.0.0");
            }

            TargetFrameworkInformation root = CreateSharingFramework("1.0.0", out _, out _);
            var rootSpec = new PackageSpec([root])
            {
                Name = "ROOT",
                RestoreMetadata = new ProjectRestoreMetadata { ProjectUniqueName = "ROOT" }
            };
            GetPackageSpecHash(rootSpec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true);
            GetPackageSpecHash(rootSpec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true);
            Assert.Null(root.GetCachedCentralPackageVersions());
            Assert.Null(root.GetCachedPackagesToPrune());

            var graph = new DependencyGraphSpec();
            graph.AddRestore("ROOT");
            graph.AddProject(rootSpec);
            graph.AddProject(existingSpec);
            string actual = GetGraphHash(graph, useLegacyHashFunction);

            Assert.Same(cachedCentral, root.GetCachedCentralPackageVersions());
            Assert.Same(cachedPruning, root.GetCachedPackagesToPrune());

            PackageSpec freshRoot = rootSpec.Clone();
            freshRoot.TargetFrameworks[0] = new TargetFrameworkInformation(root);
            PackageSpec freshExisting = existingSpec.Clone();
            freshExisting.TargetFrameworks[0] = new TargetFrameworkInformation(existing);
            var freshGraph = new DependencyGraphSpec();
            freshGraph.AddRestore("ROOT");
            freshGraph.AddProject(freshRoot);
            freshGraph.AddProject(freshExisting);
            string expected = GetGraphHash(freshGraph, useLegacyHashFunction);
            Assert.Equal(expected, actual);
            Assert.Equal(expected, GetGraphHash(graph, useLegacyHashFunction));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetHash_WithConcurrentEquivalentSections_PreservesPerOperationIsolation(bool useLegacyHashFunction)
        {
            var graph = new DependencyGraphSpec();
            for (int i = 0; i < 4; i++)
            {
                TargetFrameworkInformation framework = CreateSharingFramework("1.0.0", out _, out _);
                string name = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                graph.AddProject(new PackageSpec([framework])
                {
                    Name = name,
                    RestoreMetadata = new ProjectRestoreMetadata { ProjectUniqueName = name }
                });
            }
            graph.AddRestore("0");
            string expected = GetGraphHash(graph, useLegacyHashFunction);
            Parallel.For(0, 32, _ => Assert.Equal(expected, GetGraphHash(graph, useLegacyHashFunction)));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Write_WithMutableGeneratedVersionText_ReflectsFormatterChanges(bool useLegacyHashFunction, bool changePruning)
        {
            TargetFrameworkInformation framework = CreateSharingFramework("1.0.0", out var central, out var pruning);
            var range = new MutableFormattedVersionRange();
            if (changePruning)
            {
                pruning["Pruned"] = new PrunePackageReference("IgnoredName", range);
            }
            else
            {
                central["CentralKey"] = new CentralPackageVersion("Package", range);
            }
            var spec = new PackageSpec([framework]);
            string original = GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true);
            Assert.Equal(original, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
            Assert.Equal(original, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));

            range.FormattedText = "2.0.0";
            string expected = GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: false);
            Assert.NotEqual(original, expected);
            Assert.Equal(expected, GetPackageSpecHash(spec, useLegacyHashFunction, useLegacyWriter: false, prefix: "", useCache: true));
            if (changePruning)
            {
                Assert.Null(framework.GetCachedPackagesToPrune());
            }
            else
            {
                Assert.Null(framework.GetCachedCentralPackageVersions());
            }
        }

        [Fact]
        public void RoundTripAutoReferencedProperty()
        {
            // Arrange
            var json = @"{
                  ""frameworks"": {
                    ""net46"": {
                        ""framework"": ""net46"",
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
                        ""framework"": ""net46"",
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                                ""autoReferenced"": true
                            }
                        },
                        ""downloadDependencies"": [
                            {""name"" : ""a"", ""version"" : ""[1.0.0, 1.0.0]""},
                            {""name"" : ""b"", ""version"" : ""[1.0.0, 1.0.0];[2.0.0, 2.0.0]""}
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
            using (var jsonWriter = new JTokenWriter())
            using (var writer = new JsonObjectWriter(jsonWriter))
            {
                Assert.Throws<ArgumentNullException>(() => PackageSpecWriter.Write(packageSpec: null, writer: writer));
            }
        }

        [Fact]
        public void Write_ThrowsForNullWriter()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => PackageSpecWriter.Write(EmptyPackageSpec, writer: null));
        }

        [Fact]
        public void Write_ReadWriteDependencies()
        {
            // Arrange
            var json = @"{
  ""version"": ""1.2.3"",
  ""frameworks"": {
    ""net46"": {
        ""framework"": ""net46"",
        ""dependencies"": {
            ""packageA"": {
                ""suppressParent"": ""All"",
                ""target"": ""Project""
            }
        }
    }
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
    ""restoreUseLegacyDependencyResolver"": true,
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
        ""framework"": ""net45"",
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
      ],
      ""warnNotAsError"": [
        ""NU1801"",
        ""NU1802""
      ]
    }
  }
}";
            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_ReadWrite_RestoreDoNotWriteDependencyGraphSpec_True()
        {
            // Arrange
            var json = @"{
                            ""restore"": {
    ""projectUniqueName"": ""projectUniqueName"",
    ""projectName"": ""projectName"",
    ""projectPath"": ""projectPath"",
    ""packagesPath"": ""packagesPath"",
    ""outputPath"": ""outputPath"",
    ""projectStyle"": ""PackageReference"",
    ""restoreDoNotWriteDependencyGraphSpec"": true,
    ""frameworks"": {
      ""net45"": {
        ""framework"": ""net45"",
        ""projectReferences"": {}
      }
    }
  }
}";
            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_ReadWrite_RestoreDoNotWriteDependencyGraphSpec_DefaultFalse_NotWritten()
        {
            // Arrange - when RestoreDoNotWriteDependencyGraphSpec is false (default), it should not appear in output
            var json = @"{
                            ""restore"": {
    ""projectUniqueName"": ""projectUniqueName"",
    ""projectName"": ""projectName"",
    ""projectPath"": ""projectPath"",
    ""packagesPath"": ""packagesPath"",
    ""outputPath"": ""outputPath"",
    ""projectStyle"": ""PackageReference"",
    ""frameworks"": {
      ""net45"": {
        ""framework"": ""net45"",
        ""projectReferences"": {}
      }
    }
  }
}";
            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "TestProject", "project.csproj");

            // Assert - default value should be false
            spec.RestoreMetadata.RestoreDoNotWriteDependencyGraphSpec.Should().BeFalse();

            // And it should not appear in the serialized output
            var output = GetJsonString(spec);
            output.Should().NotContain("restoreDoNotWriteDependencyGraphSpec");
        }

        [Fact]
        public void Write_ReadWrite_RestoreEnableAnalyzerAssets_True()
        {
            // Arrange
            var spec = new PackageSpec(new[]
            {
                new TargetFrameworkInformation
                {
                    FrameworkName = NuGetFramework.Parse("net45")
                }
            })
            {
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    ProjectUniqueName = "projectUniqueName",
                    ProjectName = "projectName",
                    ProjectStyle = ProjectStyle.PackageReference,
                    RestoreEnableAnalyzerAssets = true
                }
            };

            // Act
            var json = GetJsonString(spec);
            var roundTripped = JsonPackageSpecReader.GetPackageSpec(json, "projectName", "project.csproj");

            // Assert
            json.Should().Contain("restoreEnableAnalyzerAssets");
            roundTripped.RestoreMetadata.RestoreEnableAnalyzerAssets.Should().BeTrue();
        }

        [Fact]
        public void Write_ReadWrite_RestoreEnableAnalyzerAssets_DefaultFalse_NotWritten()
        {
            // Arrange
            var spec = new PackageSpec(new[]
            {
                new TargetFrameworkInformation
                {
                    FrameworkName = NuGetFramework.Parse("net45")
                }
            })
            {
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    ProjectUniqueName = "projectUniqueName",
                    ProjectName = "projectName",
                    ProjectStyle = ProjectStyle.PackageReference
                }
            };

            // Act
            var output = GetJsonString(spec);

            // Assert
            spec.RestoreMetadata.RestoreEnableAnalyzerAssets.Should().BeFalse();
            output.Should().NotContain("restoreEnableAnalyzerAssets");
        }

        [Fact]
        public void Write_SerializesMembersAsJson()
        {
            // Arrange && Act
            var expectedJson = ResourceTestUtility.GetResource("NuGet.ProjectModel.Test.compiler.resources.PackageSpecWriter_Write_SerializesMembersAsJson.json", typeof(PackageSpecWriterTests));
            var packageSpec = CreatePackageSpec(withRestoreSettings: true);
            var actualJson = GetJsonString(packageSpec);

            // Assert
            actualJson.Should().Be(expectedJson);
        }

        [Fact]
        public void Write_SerializesMembersAsJsonWithoutRestoreSettings()
        {
            // Arrange && Act
            var expectedJson = ResourceTestUtility.GetResource("NuGet.ProjectModel.Test.compiler.resources.PackageSpecWriter_Write_SerializesMembersAsJson.json", typeof(PackageSpecWriterTests));
            var packageSpec = CreatePackageSpec(withRestoreSettings: false);
            var actualJson = GetJsonString(packageSpec);

            // Assert
            actualJson.Should().Be(expectedJson);
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
  ],
  ""warnNotAsError"": [
    ""NU1801"",
    ""NU1802""
  ]
}";
            var allWarningsAsErrors = true;
            var warningsAsErrors = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500, NuGetLogCode.NU1501 };
            var noWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1602, NuGetLogCode.NU1601 };
            var warningsNotAsErrors = new HashSet<NuGetLogCode> { NuGetLogCode.NU1801, NuGetLogCode.NU1802 };
            var warningProperties = new WarningProperties(warningsAsErrors, noWarn, allWarningsAsErrors, warningsNotAsErrors);
            var packageSpec = CreatePackageSpec(withRestoreSettings: true, warningProperties: warningProperties);
            var actualJson = packageSpec.ToJObject();
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
  ],
  ""warnNotAsError"": [
    ""NU1802"",
    ""NU1803""
  ]
}";
            var allWarningsAsErrors = false;
            var warningsAsErrors = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500, NuGetLogCode.NU1501 };
            var noWarn = new HashSet<NuGetLogCode> { NuGetLogCode.NU1602, NuGetLogCode.NU1601 };
            var warningsNotAsErrors = new HashSet<NuGetLogCode>() { NuGetLogCode.NU1802, NuGetLogCode.NU1803 };
            var warningProperties = new WarningProperties(warningsAsErrors, noWarn, allWarningsAsErrors, warningsNotAsErrors);
            var packageSpec = CreatePackageSpec(withRestoreSettings: true, warningProperties: warningProperties);
            var actualJson = packageSpec.ToJObject();
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
            var warningsNotAsErrors = new HashSet<NuGetLogCode>() { };
            var warningProperties = new WarningProperties(warningsAsErrors, noWarn, allWarningsAsErrors, warningsNotAsErrors);
            var packageSpec = CreatePackageSpec(withRestoreSettings: true, warningProperties: warningProperties);
            var actualJson = packageSpec.ToJObject();
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
            var warningsNotAsErrors = new HashSet<NuGetLogCode>() { };
            var warningProperties = new WarningProperties(warningsAsErrors, noWarn, allWarningsAsErrors, warningsNotAsErrors);
            var packageSpec = CreatePackageSpec(withRestoreSettings: true, warningProperties: warningProperties);
            var actualJson = packageSpec.ToJObject();
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
            var warningsNotAsErrors = new HashSet<NuGetLogCode>() { };
            var warningProperties = new WarningProperties(warningsAsErrors, noWarn, allWarningsAsErrors, warningsNotAsErrors);
            var packageSpec = CreatePackageSpec(withRestoreSettings: true, warningProperties: warningProperties);
            var actualJson = packageSpec.ToJObject();
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
                  ""frameworks"": {
                    ""net46"": {
                        ""framework"": ""net46"",
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
                       ""frameworkReferences"" : {
                            ""b"": {
                                ""privateAssets"": ""none"",
                            },
                            ""a"": {
                                ""privateAssets"": ""none"",
                            }
                        }
                    }
                  }
                }";

            var expectedJson = @"{
                  ""frameworks"": {
                    ""net46"": {
                      ""framework"": ""net46"",
                      ""dependencies"": {
                        ""a"": ""[1.0.0, )"",
                        ""b"": ""[1.0.0, )""
                      },
                      ""downloadDependencies"": [
                            {""name"" : ""c"", ""version"" : ""[2.0.0, 2.0.0]""},
                            {""name"" : ""d"", ""version"" : ""[2.0.0, 2.0.0]""},
                       ],
                      ""frameworkReferences"" : {
                            ""a"": {
                                ""privateAssets"": ""none"",
                            },
                            ""b"": {
                                ""privateAssets"": ""none"",
                            }
                        }
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
                  ""frameworks"": {
                    ""net46"": {
                        ""framework"": ""net46"",
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
                  ""frameworks"": {
                    ""net46"": {
                      ""framework"": ""net46"",
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
                        ""framework"": ""net46"",
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                                ""autoReferenced"": true
                            }
                        },
                        ""frameworkReferences"": {
                            ""Microsoft.WindowsDesktop.App|WinForms"" : {
                                ""privateAssets"" : ""none""
                            },
                            ""Microsoft.WindowsDesktop.App|WPF"" : {
                                ""privateAssets"" : ""none""
                            }
                        }
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void RoundTripRuntimeIdentifierGraphPath()
        {
            // Arrange
            var json = @"{
                  ""frameworks"": {
                    ""netcoreapp3.0"": {
                        ""framework"": ""netcoreapp3.0"",
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                                ""autoReferenced"": true
                            }
                        },
                        ""runtimeIdentifierGraphPath"": ""path\\to\\sdk\\3.0.100\\runtime.json""
                    },
                    ""netcoreapp3.1"": {
                        ""framework"": ""netcoreapp3.1"",
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[2.0.0, )"",
                                ""autoReferenced"": true
                            }
                        },
                        ""runtimeIdentifierGraphPath"": ""path\\to\\sdk\\3.1.100\\runtime.json""
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void RoundTripPackageReferenceVersionCentrallyManaged()
        {
            // Arrange
            var json = @"{
                  ""frameworks"": {
                    ""net46"": {
                        ""framework"": ""net46"",
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                                ""versionCentrallyManaged"": true
                            }
                        }
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void RoundTripPackageReferenceGeneratePathProperty()
        {
            // Arrange
            var json = @"{
                  ""frameworks"": {
                    ""net46"": {
                        ""framework"": ""net46"",
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                                ""generatePathProperty"": true
                            }
                        }
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void RoundTripPackageReferenceAliases()
        {
            // Arrange
            var json = @"{
                  ""frameworks"": {
                    ""net46"": {
                        ""framework"": ""net46"",
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                                ""aliases"": ""yay""
                            }
                        }
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void RoundTripTargetFrameworkAliases()
        {
            var json = @"{
                        ""restore"": {
                        ""projectUniqueName"": ""projectUniqueName"",
                        ""projectName"": ""projectName"",
                        ""projectPath"": ""projectPath"",
                        ""projectJsonPath"": ""projectJsonPath"",
                        ""packagesPath"": ""packagesPath"",
                        ""outputPath"": ""outputPath"",
                        ""projectStyle"": ""PackageReference"",
                        ""frameworks"": {
                          ""minNetVersion"": {
                            ""framework"": ""net45"",
                            ""targetAlias"" : ""minNetVersion"",
                            ""projectReferences"": {}
                          }
                        }
                      },
                  ""frameworks"": {
                    ""minNetVersion"": {
                        ""framework"" : ""net45"",
                        ""targetAlias"" : ""minNetVersion"",
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                                ""aliases"": ""yay""
                            }
                        }
                    }
                }
            }";
            // Arrange

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_WithAssetTargetFallbackAndDualCompatibilityFramework_RoundTrips()
        {
            // Arrange
            var json = @"{
                  ""frameworks"": {
                    ""net5.0"": {
                        ""framework"": ""net5.0"",
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""[1.0.0, )"",
                                ""autoReferenced"": true
                            }
                        },
                        ""imports"": [
                           ""net472"",
                           ""net471""
                        ],
                        ""assetTargetFallback"" : true,
                        ""secondaryFramework"" : ""native""
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_RestoreAuditProperties_RoundTrips()
        {
            // Arrange
            var json = @"{
                  ""restore"": {
                    ""projectUniqueName"": ""projectUniqueName"",
                    ""restoreAuditProperties"": {
                        ""enableAudit"": ""true"",
                        ""auditLevel"": ""moderate"",
                        ""auditMode"": ""all""
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_RestoreSdkAnalysisLevel_RoundTrips()
        {
            // Arrange
            var json = @"{
                  ""restore"": {
                    ""projectUniqueName"": ""projectUniqueName"",
                    ""SdkAnalysisLevel"": ""9.0.100""
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_RestoreUsingMicrosoftNetSdk_RoundTrips()
        {
            // Arrange
            var json = @"{
                  ""restore"": {
                    ""projectUniqueName"": ""projectUniqueName"",
                    ""UsingMicrosoftNETSdk"": false
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_RestoreAuditPropertiesWithSuppressions_RoundTrips()
        {
            // Arrange
            var json = @"{
                  ""restore"": {
                    ""projectUniqueName"": ""projectUniqueName"",
                    ""restoreAuditProperties"": {
                        ""enableAudit"": ""true"",
                        ""auditLevel"": ""moderate"",
                        ""auditMode"": ""all"",
                        ""suppressedAdvisories"": {
                            ""https://github.com/advisories/example-cve-1"": null,
                            ""https://github.com/advisories/example-cve-2"": null
                        },
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void RestoreMetadataWithMacros_RoundTrips()
        {
            // Arrange
            var json = @"{
                            ""restore"": {
    ""projectUniqueName"": ""C:\\users\\me\\source\\code\\project.csproj"",
    ""projectName"": ""project"",
    ""projectPath"": ""C:\\users\\me\\source\\code\\project.csproj"",
    ""projectJsonPath"": ""C:\\users\\me\\source\\code\\project.json"",
    ""packagesPath"": ""$(User).nuget\\packages"",
    ""outputPath"": ""C:\\users\\me\\source\\code\\obj"",
    ""projectStyle"": ""PackageReference"",
    ""crossTargeting"": true,
    ""fallbackFolders"": [
        ""C:\\Program Files\\dotnet\\sdk\\NuGetFallbackFolder"",
        ""$(User)fallbackFolder""


    ],
    ""configFilePaths"": [
        ""$(User)source\\code\\NuGet.Config"",
        ""$(User)AppData\\Roaming\\NuGet\\NuGet.Config"",
        ""C:\\Program Files (x86)\\NuGet\\Config\\Microsoft.VisualStudio.FallbackLocation.config"",
        ""C:\\Program Files (x86)\\NuGet\\Config\\Microsoft.VisualStudio.Offline.config""
    ]
  }
}";
            var environmentReader = new TestEnvironmentVariableReader(new Dictionary<string, string>()
                {
                    { MacroStringsUtility.NUGET_ENABLE_EXPERIMENTAL_MACROS, "true" }
            });

            // Act
            var actual = PackageSpecTestUtility.RoundTripJson(json, environmentReader);

            // Assert
            var metadata = actual.RestoreMetadata;
            var userSettingsDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);

            Assert.NotNull(metadata);
            metadata.PackagesPath.Should().Be(@$"{userSettingsDirectory}.nuget\packages");

            metadata.ConfigFilePaths.Should().Contain(@$"{userSettingsDirectory}source\code\NuGet.Config");
            metadata.ConfigFilePaths.Should().Contain(@"C:\Program Files (x86)\NuGet\Config\Microsoft.VisualStudio.FallbackLocation.config");
            metadata.ConfigFilePaths.Should().Contain(@"C:\Program Files (x86)\NuGet\Config\Microsoft.VisualStudio.Offline.config");
            metadata.ConfigFilePaths.Should().Contain(@$"{userSettingsDirectory}AppData\Roaming\NuGet\NuGet.Config");

            metadata.FallbackFolders.Should().Contain(@"C:\Program Files\dotnet\sdk\NuGetFallbackFolder");
            metadata.FallbackFolders.Should().Contain(@$"{userSettingsDirectory}fallbackFolder");
        }

        [Fact]
        public void RoundTripPackagesToPrune()
        {
            // Arrange
            var json = @"{
                  ""frameworks"": {
                    ""net46"": {
                        ""framework"": ""net46"",
                        ""packagesToPrune"": {
                            ""a"": ""(, 2.1.3]""
                        }
                    }
                  }
                }";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }


        [Fact]
        public void RoundTripAliasedFrameworks()
        {
            // Arrange
            var json = @"{
                  ""frameworks"": {
                    ""latestNet"": {
                        ""framework"": ""net10.0"",
                        ""targetAlias"": ""latestNet"",
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

        private sealed class MutableFormattedVersionRange : VersionRange
        {
            public string FormattedText { get; set; } = "1.0.0";

            public override string ToNormalizedString() => FormattedText;
        }

        private static TargetFrameworkInformation CreateSharingFramework(
            string version,
            out Dictionary<string, CentralPackageVersion> central,
            out Dictionary<string, PrunePackageReference> pruning)
        {
            central = new Dictionary<string, CentralPackageVersion>
            {
                ["CentralKey"] = new CentralPackageVersion("Package", VersionRange.Parse(version))
            };
            pruning = new Dictionary<string, PrunePackageReference>
            {
                ["Pruned"] = PrunePackageReference.Create("IgnoredName", version)
            };
            return new TargetFrameworkInformation
            {
                FrameworkName = NuGetFramework.Parse("net10.0"),
                CentralPackageVersions = central,
                PackagesToPrune = pruning
            };
        }

        private static string GetGraphHash(DependencyGraphSpec graph, bool useLegacyHashFunction)
        {
            return useLegacyHashFunction
                ? graph.GetHash(static () => new Sha512HashFunction())
                : graph.GetHash(static () => new FnvHash64Function());
        }

        private static void MutatePackageReferences<T>(Dictionary<string, T> references, string change, Func<string, T> createReference)
        {
            switch (change)
            {
                case "add":
                    references.Add("b", createReference("b"));
                    break;
                case "remove":
                    references.Remove("a");
                    break;
                case "replace":
                    references["a"] = createReference("a");
                    break;
                case "case":
                    T value = references["z"];
                    references.Remove("z");
                    references.Add("Z", value);
                    break;
                case "reorder":
                    KeyValuePair<string, T>[] entries = references.ToArray();
                    references.Clear();
                    for (int i = entries.Length - 1; i >= 0; i--)
                    {
                        references.Add(entries[i].Key, entries[i].Value);
                    }
                    break;
                case "clear":
                    references.Clear();
                    break;
                default:
                    throw new ArgumentException("Unknown dictionary mutation.", nameof(change));
            }
        }

        private static string GetPackageSpecHash(PackageSpec spec, bool useLegacyHashFunction, bool useLegacyWriter, string prefix, bool useCache)
        {
            using IHashFunction hash = useLegacyHashFunction ? new Sha512HashFunction() : new FnvHash64Function();
            using var writer = new HashObjectWriter(hash);
            writer.WriteObjectStart();
            writer.WriteNameValue("prefix", prefix);
            IObjectWriter objectWriter = useCache ? writer : new UncachedObjectWriter(writer);
            PackageSpecWriter.Write(spec, objectWriter, hashing: true, EnvironmentVariableWrapper.Instance, useLegacyWriter);
            writer.WriteNameValue("tail", "After the cached sections");
            writer.WriteObjectEnd();
            return writer.GetHash();
        }

        private sealed class UncachedObjectWriter : IObjectWriter
        {
            private readonly IObjectWriter _inner;

            public UncachedObjectWriter(IObjectWriter inner) => _inner = inner;

            public void WriteObjectStart() => _inner.WriteObjectStart();
            public void WriteObjectStart(string name) => _inner.WriteObjectStart(name);
            public void WriteObjectEnd() => _inner.WriteObjectEnd();
            public void WriteNameValue(string name, int value) => _inner.WriteNameValue(name, value);
            public void WriteNameValue(string name, bool value) => _inner.WriteNameValue(name, value);
            public void WriteNameValue(string name, string value) => _inner.WriteNameValue(name, value);
            public void WriteNameArray(string name, IEnumerable<string> values) => _inner.WriteNameArray(name, values);
            public void WriteNonEmptyNameArray(string name, IEnumerable<string> values) => _inner.WriteNonEmptyNameArray(name, values);
            public void WriteArrayStart(string name) => _inner.WriteArrayStart(name);
            public void WriteArrayEnd() => _inner.WriteArrayEnd();
        }

        private static string GetJsonString(PackageSpec packageSpec)
        {
            JObject jObject = packageSpec.ToJObject();

            return jObject.ToString(Formatting.Indented);
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
                NoWarn = [NuGetLogCode.NU1500, NuGetLogCode.NU1601]
            };

            var libraryDependencyWithNoWarnGlobal = new LibraryDependency()
            {
                IncludeType = LibraryIncludeFlags.Build,
                LibraryRange = libraryRangeWithNoWarnGlobal,
                NoWarn = [NuGetLogCode.NU1500, NuGetLogCode.NU1608]
            };

            var nugetFramework = new NuGetFramework("frameworkIdentifier", new Version("1.2.3"), "frameworkProfile");
            var nugetFrameworkWithNoWarn = new NuGetFramework("frameworkIdentifierWithNoWarn", new Version("1.2.5"), "frameworkProfileWithNoWarn");

            var packageSpec = new PackageSpec()
            {
                Name = "name",
                FilePath = "filePath",
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

            packageSpec.TargetFrameworks.Add(new TargetFrameworkInformation()
            {
                Dependencies = [libraryDependency, libraryDependencyWithNoWarnGlobal],
                FrameworkName = nugetFramework,
                Imports = [nugetFramework],
            });

            packageSpec.TargetFrameworks.Add(new TargetFrameworkInformation()
            {
                Dependencies = [libraryDependencyWithNoWarn, libraryDependency, libraryDependencyWithNoWarnGlobal],
                FrameworkName = nugetFrameworkWithNoWarn,
                Imports = [nugetFrameworkWithNoWarn],
                Warn = true
            });

            return packageSpec;
        }

        private static void VerifyJsonPackageSpecRoundTrip(string json)
        {
            // Arrange & Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "testName", @"C:\fake\path");
            string actualResult = GetJsonString(spec);
            string expected = JObject.Parse(json).ToString();

            // Assert
            Assert.Equal(expected, actualResult);
        }

        private static void VerifyPackageSpecWrite(string json, string expectedJson)
        {
            // Arrange & Act
            PackageSpec spec = JsonPackageSpecReader.GetPackageSpec(json, "testName", @"C:\fake\path");
            string actualResult = GetJsonString(spec);
            string expected = JObject.Parse(expectedJson).ToString();

            // Assert
            Assert.Equal(expected, actualResult);
        }
    }
}
