// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class TargetFrameworkInformationTests
    {
        [Fact]
        public void GetSerializedPackageReferences_ReusesContentUntilDictionariesChange()
        {
            var central = new Dictionary<string, CentralPackageVersion>
            {
                ["Package"] = new CentralPackageVersion("Package", VersionRange.Parse("1.0.0"))
            };
            var pruning = new Dictionary<string, PrunePackageReference>
            {
                ["Pruned"] = PrunePackageReference.Create("Pruned", "1.0.0")
            };
            var framework = new TargetFrameworkInformation { CentralPackageVersions = central, PackagesToPrune = pruning };
            using var writer = new HashObjectWriter(new FnvHash64Function());
            int centralSerializations = 0;
            int pruningSerializations = 0;

            Assert.Null(framework.GetSerializedCentralPackageVersions(SerializeCentral, writer, out _));
            Assert.Null(framework.GetSerializedPackagesToPrune(SerializePruning, writer, out _));
            Assert.Null(framework.GetSerializedCentralPackageVersions(SerializeCentral, writer, out _));
            Assert.Null(framework.GetSerializedPackagesToPrune(SerializePruning, writer, out _));
            Assert.Equal(0, centralSerializations);
            Assert.Equal(0, pruningSerializations);

            string originalCentral = framework.GetSerializedCentralPackageVersions(SerializeCentral, writer, out _);
            string originalPruning = framework.GetSerializedPackagesToPrune(SerializePruning, writer, out _);
            Assert.Same(originalCentral, framework.GetSerializedCentralPackageVersions(SerializeCentral, writer, out _));
            Assert.Same(originalPruning, framework.GetSerializedPackagesToPrune(SerializePruning, writer, out _));
            Assert.Equal(1, centralSerializations);
            Assert.Equal(1, pruningSerializations);

            central["Package"] = new CentralPackageVersion("Package", VersionRange.Parse("2.0.0"));
            pruning["Pruned"] = PrunePackageReference.Create("Pruned", "2.0.0");
            Assert.Null(framework.GetSerializedCentralPackageVersions(SerializeCentral, writer, out _));
            Assert.Null(framework.GetSerializedPackagesToPrune(SerializePruning, writer, out _));
            Assert.Null(framework.GetSerializedCentralPackageVersions(SerializeCentral, writer, out _));
            Assert.Null(framework.GetSerializedPackagesToPrune(SerializePruning, writer, out _));
            Assert.NotEqual(originalCentral, framework.GetSerializedCentralPackageVersions(SerializeCentral, writer, out _));
            Assert.NotEqual(originalPruning, framework.GetSerializedPackagesToPrune(SerializePruning, writer, out _));
            Assert.Equal(2, centralSerializations);
            Assert.Equal(2, pruningSerializations);

            central.Clear();
            pruning.Clear();
            Assert.Null(framework.GetSerializedCentralPackageVersions(SerializeCentral, writer, out _));
            Assert.Null(framework.GetSerializedPackagesToPrune(SerializePruning, writer, out _));
            Assert.Equal(2, centralSerializations);
            Assert.Equal(2, pruningSerializations);

            central["Package"] = new CentralPackageVersion("Package", VersionRange.Parse("3.0.0"));
            pruning["Pruned"] = PrunePackageReference.Create("Pruned", "3.0.0");
            Assert.Null(framework.GetSerializedCentralPackageVersions(SerializeCentral, writer, out _));
            Assert.Null(framework.GetSerializedPackagesToPrune(SerializePruning, writer, out _));
            Assert.Null(framework.GetSerializedCentralPackageVersions(SerializeCentral, writer, out _));
            Assert.Null(framework.GetSerializedPackagesToPrune(SerializePruning, writer, out _));
            Assert.NotEqual(originalCentral, framework.GetSerializedCentralPackageVersions(SerializeCentral, writer, out _));
            Assert.NotEqual(originalPruning, framework.GetSerializedPackagesToPrune(SerializePruning, writer, out _));
            Assert.Equal(3, centralSerializations);
            Assert.Equal(3, pruningSerializations);

            SerializedPackageReferences<CentralPackageVersion> SerializeCentral(KeyValuePair<string, CentralPackageVersion>[] items, HashObjectWriter state)
            {
                return Assert.IsType<SerializedPackageReferences<CentralPackageVersion>>(state.GetOrSerializeCentralPackageVersions(items, WriteCentralValues));
            }

            SerializedPackageReferences<PrunePackageReference> SerializePruning(KeyValuePair<string, PrunePackageReference>[] items, HashObjectWriter state)
            {
                return Assert.IsType<SerializedPackageReferences<PrunePackageReference>>(state.GetOrSerializePackagesToPrune(items, WritePruningValues));
            }

            void WriteCentralValues(NuGet.RuntimeModel.IObjectWriter output, KeyValuePair<string, CentralPackageVersion>[] items)
            {
                centralSerializations++;
                foreach (var item in items)
                {
                    output.WriteNameValue(item.Value.Name, PackageSpecWriter.GetVersionString(item.Value.VersionRange));
                }
            }

            void WritePruningValues(NuGet.RuntimeModel.IObjectWriter output, KeyValuePair<string, PrunePackageReference>[] items)
            {
                pruningSerializations++;
                foreach (var item in items)
                {
                    output.WriteNameValue(item.Key, PackageSpecWriter.GetVersionString(item.Value.VersionRange));
                }
            }
        }

        [Fact]
        public void GetOrderedCentralPackageVersions_PreservesStableNameOrderingAndReusesUnchangedSnapshot()
        {
            var references = new Dictionary<string, CentralPackageVersion>(StringComparer.Ordinal)
            {
                ["First"] = new CentralPackageVersion("z.Package", VersionRange.All),
                ["Second"] = new CentralPackageVersion("a.Package", VersionRange.All),
                ["Third"] = new CentralPackageVersion("A.Package", VersionRange.All)
            };
            var framework = new TargetFrameworkInformation { CentralPackageVersions = references };

            KeyValuePair<string, CentralPackageVersion>[] ordered = framework.GetOrderedCentralPackageVersions();

            Assert.Equal(references.OrderBy(item => item.Value.Name, StringComparer.OrdinalIgnoreCase), ordered);
            Assert.Same(ordered, framework.GetOrderedCentralPackageVersions());
        }

        [Fact]
        public void GetOrderedPackagesToPrune_PreservesStableKeyOrderingAndReusesUnchangedSnapshot()
        {
            var references = new Dictionary<string, PrunePackageReference>(StringComparer.Ordinal)
            {
                ["z.Package"] = PrunePackageReference.Create("First", "1.0.0"),
                ["a.Package"] = PrunePackageReference.Create("Second", "1.0.0"),
                ["A.Package"] = PrunePackageReference.Create("Third", "1.0.0")
            };
            var framework = new TargetFrameworkInformation { PackagesToPrune = references };

            KeyValuePair<string, PrunePackageReference>[] ordered = framework.GetOrderedPackagesToPrune();

            Assert.Equal(references.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase), ordered);
            Assert.Same(ordered, framework.GetOrderedPackagesToPrune());
        }

        [Theory]
        [InlineData("add")]
        [InlineData("remove")]
        [InlineData("replace")]
        [InlineData("case")]
        [InlineData("reorder")]
        [InlineData("clear")]
        public void GetOrderedCentralPackageVersions_AfterDictionaryChanges_ReflectsCurrentEntries(string change)
        {
            var references = new Dictionary<string, CentralPackageVersion>(StringComparer.OrdinalIgnoreCase)
            {
                ["b"] = new CentralPackageVersion("b", VersionRange.All),
                ["A"] = new CentralPackageVersion("A", VersionRange.All)
            };
            var framework = new TargetFrameworkInformation { CentralPackageVersions = references };
            KeyValuePair<string, CentralPackageVersion>[] original = framework.GetOrderedCentralPackageVersions();

            switch (change)
            {
                case "add":
                    references.Add("c", new CentralPackageVersion("c", VersionRange.All));
                    break;
                case "remove":
                    references.Remove("A");
                    break;
                case "replace":
                    references["A"] = new CentralPackageVersion("z", VersionRange.Parse("2.0.0"));
                    break;
                case "case":
                    CentralPackageVersion value = references["A"];
                    references.Remove("A");
                    references.Add("a", value);
                    break;
                case "reorder":
                    KeyValuePair<string, CentralPackageVersion>[] entries = references.ToArray();
                    references.Clear();
                    for (int i = entries.Length - 1; i >= 0; i--)
                    {
                        KeyValuePair<string, CentralPackageVersion> entry = entries[i];
                        references.Add(entry.Key, entry.Value);
                    }
                    break;
                case "clear":
                    references.Clear();
                    break;
            }

            KeyValuePair<string, CentralPackageVersion>[] ordered = framework.GetOrderedCentralPackageVersions();
            Assert.NotSame(original, ordered);
            Assert.Equal(references.OrderBy(item => item.Value.Name, StringComparer.OrdinalIgnoreCase), ordered);
        }

        [Theory]
        [InlineData("add")]
        [InlineData("remove")]
        [InlineData("replace")]
        [InlineData("case")]
        [InlineData("reorder")]
        [InlineData("clear")]
        public void GetOrderedPackagesToPrune_AfterDictionaryChanges_ReflectsCurrentEntries(string change)
        {
            var references = new Dictionary<string, PrunePackageReference>(StringComparer.OrdinalIgnoreCase)
            {
                ["b"] = PrunePackageReference.Create("b", "1.0.0"),
                ["A"] = PrunePackageReference.Create("A", "1.0.0")
            };
            var framework = new TargetFrameworkInformation { PackagesToPrune = references };
            KeyValuePair<string, PrunePackageReference>[] original = framework.GetOrderedPackagesToPrune();

            switch (change)
            {
                case "add":
                    references.Add("c", PrunePackageReference.Create("c", "1.0.0"));
                    break;
                case "remove":
                    references.Remove("A");
                    break;
                case "replace":
                    references["A"] = PrunePackageReference.Create("A", "2.0.0");
                    break;
                case "case":
                    PrunePackageReference value = references["A"];
                    references.Remove("A");
                    references.Add("a", value);
                    break;
                case "reorder":
                    KeyValuePair<string, PrunePackageReference>[] entries = references.ToArray();
                    references.Clear();
                    for (int i = entries.Length - 1; i >= 0; i--)
                    {
                        KeyValuePair<string, PrunePackageReference> entry = entries[i];
                        references.Add(entry.Key, entry.Value);
                    }
                    break;
                case "clear":
                    references.Clear();
                    break;
            }

            KeyValuePair<string, PrunePackageReference>[] ordered = framework.GetOrderedPackagesToPrune();
            Assert.NotSame(original, ordered);
            Assert.Equal(references.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase), ordered);
        }

        [Fact]
        public void GetOrderedPackageReferences_WithImmutableDictionaries_ReusesSnapshots()
        {
            var framework = new TargetFrameworkInformation
            {
                CentralPackageVersions = ImmutableDictionary<string, CentralPackageVersion>.Empty
                    .Add("b", new CentralPackageVersion("b", VersionRange.All))
                    .Add("A", new CentralPackageVersion("A", VersionRange.All)),
                PackagesToPrune = ImmutableDictionary<string, PrunePackageReference>.Empty
                    .Add("b", PrunePackageReference.Create("b", "1.0.0"))
                    .Add("A", PrunePackageReference.Create("A", "1.0.0"))
            };

            Assert.Equal(new[] { "A", "b" }, framework.GetOrderedCentralPackageVersions().Select(item => item.Value.Name));
            Assert.Equal(new[] { "A", "b" }, framework.GetOrderedPackagesToPrune().Select(item => item.Key));
            Assert.Same(framework.GetOrderedCentralPackageVersions(), framework.GetOrderedCentralPackageVersions());
            Assert.Same(framework.GetOrderedPackagesToPrune(), framework.GetOrderedPackagesToPrune());
        }

        [Fact]
        public void GetOrderedPackageReferences_OnCloneWithOverrides_UsesNewCollections()
        {
            var framework = new TargetFrameworkInformation
            {
                CentralPackageVersions = new Dictionary<string, CentralPackageVersion>
                {
                    ["A"] = new CentralPackageVersion("A", VersionRange.All)
                },
                PackagesToPrune = new Dictionary<string, PrunePackageReference>
                {
                    ["A"] = PrunePackageReference.Create("A", "1.0.0")
                }
            };
            framework.GetOrderedCentralPackageVersions();
            framework.GetOrderedPackagesToPrune();
            var clone = new TargetFrameworkInformation(framework)
            {
                CentralPackageVersions = new Dictionary<string, CentralPackageVersion>
                {
                    ["b"] = new CentralPackageVersion("b", VersionRange.All)
                },
                PackagesToPrune = null!
            };

            Assert.Equal("b", Assert.Single(clone.GetOrderedCentralPackageVersions()).Value.Name);
            Assert.Empty(clone.GetOrderedPackagesToPrune());
            Assert.Equal("A", Assert.Single(framework.GetOrderedCentralPackageVersions()).Value.Name);
            Assert.Equal("A", Assert.Single(framework.GetOrderedPackagesToPrune()).Key);
        }

        [Fact]
        public void GetOrderedPackageReferences_WithConcurrentReaders_PublishesCompleteSnapshots()
        {
            var framework = new TargetFrameworkInformation
            {
                CentralPackageVersions = new Dictionary<string, CentralPackageVersion>
                {
                    ["b"] = new CentralPackageVersion("b", VersionRange.All),
                    ["A"] = new CentralPackageVersion("A", VersionRange.All)
                },
                PackagesToPrune = new Dictionary<string, PrunePackageReference>
                {
                    ["b"] = PrunePackageReference.Create("b", "1.0.0"),
                    ["A"] = PrunePackageReference.Create("A", "1.0.0")
                }
            };

            Parallel.For(0, 64, _ =>
            {
                Assert.Equal(new[] { "A", "b" }, framework.GetOrderedCentralPackageVersions().Select(item => item.Value.Name));
                Assert.Equal(new[] { "A", "b" }, framework.GetOrderedPackagesToPrune().Select(item => item.Key));
            });
        }

        [Fact]
        public void Equals_WithSameObject_ReturnsTrue()
        {
            // Arrange
            var tfi = CreateTargetFrameworkInformation();

            // Act & Assert
            tfi.Equals(tfi).Should().BeTrue();
        }

        [Fact]
        public void Equals_WithSameContent_ReturnsTrue()
        {
            // Arrange
            var tfi = CreateTargetFrameworkInformation();
            var tfiTwin = CreateTargetFrameworkInformation();

            // Act & Assert
            tfi.Equals(tfiTwin).Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentObject_CentralDependency_ReturnsFalse()
        {
            // Arrange
            var tfiFoo = CreateTargetFrameworkInformation(new List<CentralPackageVersion>() { new CentralPackageVersion("foo", VersionRange.All) });
            var tfiBar = CreateTargetFrameworkInformation(new List<CentralPackageVersion>() { new CentralPackageVersion("bar", VersionRange.All) });

            // Act & Assert
            tfiFoo.Equals(tfiBar).Should().BeFalse();
        }

        [Fact]
        public void Equals_OnClone_ReturnsTrue()
        {
            // Arrange
            var tfi = CreateTargetFrameworkInformation();
            var tfiClone = new TargetFrameworkInformation(tfi);

            // Act & Assert
            tfi.Equals(tfiClone).Should().BeTrue();
            Assert.NotSame(tfi, tfiClone);
            Assert.Same(tfi.CentralPackageVersions, tfiClone.CentralPackageVersions);
            Assert.True(tfi.Dependencies.Equals(tfiClone.Dependencies));
            Assert.True(tfi.Imports.Equals(tfiClone.Imports));
            Assert.True(tfi.DownloadDependencies.Equals(tfiClone.DownloadDependencies));
            Assert.Same(tfi.FrameworkReferences, tfiClone.FrameworkReferences);
        }

        [Theory]
        [InlineData("net461", "net461", true)]
        [InlineData("net461", "NET461", true)]
        [InlineData("net461", "", false)]
        public void Equals_WithTargetAlias(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                TargetAlias = left
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                TargetAlias = right
            };

            AssertEquality(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("net461", "net461", true)]
        [InlineData("net461", "NET461", true)]
        [InlineData("net461", "netstandard2.0", false)]
        public void Equals_WithFrameworkName(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.Parse(left)
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.Parse(right)
            };

            AssertEquality(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("net461", "net461", true)]
        [InlineData("net461", "NET461", true)]
        [InlineData("net472;net461", "net472;NET461", true)]
        [InlineData("net461", "", false)]
        [InlineData("net461;net472", "net472;net461", false)]
        public void Equals_WithImports(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                Imports = left.Split(';').Select(e => NuGetFramework.Parse(e)).ToImmutableArray()
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                Imports = right.Split(';').Select(e => NuGetFramework.Parse(e)).ToImmutableArray()
            };

            AssertEquality(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        public void Equals_WithAssetTargetFallback(bool left, bool right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                AssetTargetFallback = left
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                AssetTargetFallback = right
            };

            AssertEquality(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        public void Equals_WithWarn(bool left, bool right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                Warn = left
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                Warn = right
            };

            AssertEquality(expected, leftSide, rightSide);
        }

        [PlatformTheory(Platform = Platform.Windows)]
        [InlineData(@"C:\nugetLocation", @"C:\nugetLocation", true)]
        [InlineData(@"C:\nugetLocation", @"C:\NugetLocation", true)]
        [InlineData(@"C:\nugetLocation", @"C:\nugetLocation2", false)]
        [InlineData(@"C:\nugetLocation", "", false)]
        public void Equals_CaseInsensitive_WithRuntimeIdentifierGraphPath(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                RuntimeIdentifierGraphPath = left
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                RuntimeIdentifierGraphPath = right
            };

            AssertEquality(expected, leftSide, rightSide);
        }

        [PlatformTheory(Platform = Platform.Linux)]
        [InlineData(@"C:\nugetLocation", @"C:\nugetLocation", true)]
        [InlineData(@"C:\nugetLocation", @"C:\NugetLocation", false)]
        [InlineData(@"C:\nugetLocation", @"C:\nugetLocation2", false)]
        [InlineData(@"C:\nugetLocation", "", false)]
        public void Equals_CaseSensitive_WithRuntimeIdentifierGraphPath(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                RuntimeIdentifierGraphPath = left
            };

            var rightSide = new TargetFrameworkInformation()
            {
                RuntimeIdentifierGraphPath = right
            };

            AssertEquality(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("a;b", "a;b", true)]
        [InlineData("a;B", "A;b", true)]
        [InlineData("B;a", "A;b", true)]
        [InlineData("a;B", "b;a", true)]
        [InlineData("a;c;b", "c;B;a", true)]
        [InlineData("B;a;c", "A;b", false)]
        [InlineData("B;a", "A;b;c", false)]
        public void Equals_WithDependencies(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                Dependencies = left.Split(';').Select(
                    e => new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange()
                        {
                            Name = e,
                            VersionRange = VersionRange.Parse("1.0.0")
                        }
                    }).ToImmutableArray()
            };

            var rightSide = new TargetFrameworkInformation()
            {
                Dependencies = right.Split(';').Select(
                    e => new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange()
                        {
                            Name = e,
                            VersionRange = VersionRange.Parse("1.0.0")
                        }
                    }).ToImmutableArray()
            };

            AssertEquality(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("a;b", "a;b", true)]
        [InlineData("a;B", "A;b", true)]
        [InlineData("B;a", "A;b", true)]
        [InlineData("B;a;c", "A;b", false)]
        [InlineData("B;a", "A;b;c", false)]
        public void Equals_WithDownloadDependencies(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                DownloadDependencies = left.Split(';').Select(e => new DownloadDependency(e, VersionRange.Parse("1.0.0"))).ToImmutableArray()
            };

            var rightSide = new TargetFrameworkInformation()
            {
                DownloadDependencies = right.Split(';').Select(e => new DownloadDependency(e, VersionRange.Parse("1.0.0"))).ToImmutableArray()
            };

            AssertEquality(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("a;b", "a;b", true)]
        [InlineData("a;B", "A;b", true)]
        [InlineData("B;a", "A;b", true)]
        [InlineData("B;a;c", "A;b", false)]
        [InlineData("B;a", "A;b;c", false)]
        public void Equals_WithFrameworkReferences(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkReferences = left.Split(';')
                    .Select(e => new FrameworkDependency(e, FrameworkDependencyFlags.All))
                    .ToHashSet()
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkReferences = right.Split(';')
                    .Select(e => new FrameworkDependency(e, FrameworkDependencyFlags.All))
                    .ToHashSet()
            };

            AssertEquality(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("a;b", "a;b", true)]
        [InlineData("a;B", "A;b", true)]
        [InlineData("B;a", "A;b", true)]
        [InlineData("B;a;c", "A;b", false)]
        [InlineData("B;a", "A;b;c", false)]
        public void Equals_WithCentralDependencies(string left, string right, bool expected)
        {
            var leftVersions = left.Split(';').ToDictionary(entry => entry, entry => new CentralPackageVersion(entry, VersionRange.All), StringComparer.OrdinalIgnoreCase);

            var leftSide = new TargetFrameworkInformation()
            {
                CentralPackageVersions = leftVersions,
                FrameworkName = NuGetFramework.AnyFramework
            };

            var rightVersions = right.Split(';').ToDictionary(entry => entry, entry => new CentralPackageVersion(entry, VersionRange.All), StringComparer.OrdinalIgnoreCase);
            var rightSide = new TargetFrameworkInformation()
            {
                CentralPackageVersions = rightVersions,
                FrameworkName = NuGetFramework.AnyFramework
            };

            AssertEquality(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("a;(,1.0.0];b;(,2.0.0]", "a;(,1.0.0];b;(,2.0.0]", true)]
        [InlineData("a;(,1.0.0];B;(,2.0.0]", "A;(,1.0.0];b;(,2.0.0]", true)]
        [InlineData("B;(,1.0.0];a;(,2.0.0]", "A;(,2.0.0];b;(,1.0.0]", true)]
        [InlineData("B;(,1.0.0];a;(,2.0.0];c;(,2.0.0]", "A;(,2.0.0];b;(,1.0.0]", false)]
        [InlineData("B;(,1.0.0];a;(,2.0.0]", "A;(,2.0.0];b;(,1.0.0];c;(,2.0.0]", false)]
        [InlineData("a;(,2.0.0];b;(,2.0.0]", "a;(,1.0.0];b;(,2.0.0]", false)]
        [InlineData("a;(,1.0.0];B;(,1.0.0]", "A;(,1.0.0];b;(,2.0.0]", false)]
        [InlineData("B;(,2.0.0];a;(,2.0.0]", "A;(,2.0.0];b;(,1.0.0]", false)]
        public void Equals_WithPrunePackageReference(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                PackagesToPrune = GetPrunePackageReferences(left),
                FrameworkName = NuGetFramework.AnyFramework
            };

            var rightSide = new TargetFrameworkInformation()
            {
                PackagesToPrune = GetPrunePackageReferences(right),
                FrameworkName = NuGetFramework.AnyFramework
            };
            AssertEquality(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("net461", "net461", true)]
        [InlineData("net461", "NET461", true)]
        [InlineData("net461", "", false)]
        public void HashCode_WithTargetAlias(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                TargetAlias = left
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                TargetAlias = right
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("net461", "net461", true)]
        [InlineData("net461", "NET461", true)]
        [InlineData("net461", "netstandard2.0", false)]
        public void HashCode_WithFrameworkName(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.Parse(left)
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.Parse(right)
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("net461", "net461", true)]
        [InlineData("net461", "NET461", true)]
        [InlineData("net472;net461", "net472;NET461", true)]
        [InlineData("net461", "", false)]
        [InlineData("net461;net472", "net472;net461", false)]
        public void HashCode_WithImports(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                Imports = left.Split(';').Select(e => NuGetFramework.Parse(e)).ToImmutableArray()
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                Imports = right.Split(';').Select(e => NuGetFramework.Parse(e)).ToImmutableArray()
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        public void HashCode_WithAssetTargetFallback(bool left, bool right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                AssetTargetFallback = left
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                AssetTargetFallback = right
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        public void HashCode_WithWarn(bool left, bool right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                Warn = left
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                Warn = right
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        [PlatformTheory(Platform = Platform.Windows)]
        [InlineData(@"C:\nugetLocation", @"C:\nugetLocation", true)]
        [InlineData(@"C:\nugetLocation", @"C:\NugetLocation", true)]
        [InlineData(@"C:\nugetLocation", @"C:\nugetLocation2", false)]
        [InlineData(@"C:\nugetLocation", "", false)]
        public void HashCode_CaseInsensitive_WithRuntimeIdentifierGraphPath(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                RuntimeIdentifierGraphPath = left
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.AnyFramework,
                RuntimeIdentifierGraphPath = right
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        [PlatformTheory(Platform = Platform.Linux)]
        [InlineData(@"C:\nugetLocation", @"C:\nugetLocation", true)]
        [InlineData(@"C:\nugetLocation", @"C:\NugetLocation", false)]
        [InlineData(@"C:\nugetLocation", @"C:\nugetLocation2", false)]
        [InlineData(@"C:\nugetLocation", "", false)]
        public void HashCode_CaseSensitive_WithRuntimeIdentifierGraphPath(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                RuntimeIdentifierGraphPath = left
            };

            var rightSide = new TargetFrameworkInformation()
            {
                RuntimeIdentifierGraphPath = right
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("a;b", "a;b", true)]
        [InlineData("a;B", "A;b", true)]
        [InlineData("B;a", "A;b", true)]
        [InlineData("B;a;c", "A;b", false)]
        [InlineData("B;a", "A;b;c", false)]
        public void HashCode_WithDependencies(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                Dependencies = left.Split(';').Select(
                    e => new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange()
                        {
                            Name = e,
                            VersionRange = VersionRange.Parse("1.0.0")
                        }
                    }).ToImmutableArray()
            };

            var rightSide = new TargetFrameworkInformation()
            {
                Dependencies = right.Split(';').Select(
                    e => new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange()
                        {
                            Name = e,
                            VersionRange = VersionRange.Parse("1.0.0")
                        }
                    }).ToImmutableArray()
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("a;b", "a;b", true)]
        [InlineData("a;B", "A;b", true)]
        [InlineData("B;a", "A;b", true)]
        [InlineData("B;a;c", "A;b", false)]
        [InlineData("B;a", "A;b;c", false)]
        public void HashCode_WithDownloadDependencies(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                DownloadDependencies = left.Split(';').Select(e => new DownloadDependency(e, VersionRange.Parse("1.0.0"))).ToImmutableArray()
            };

            var rightSide = new TargetFrameworkInformation()
            {
                DownloadDependencies = right.Split(';').Select(e => new DownloadDependency(e, VersionRange.Parse("1.0.0"))).ToImmutableArray()
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("a;b", "a;b", true)]
        [InlineData("a;B", "A;b", true)]
        [InlineData("B;a", "A;b", true)]
        [InlineData("B;a;c", "A;b", false)]
        [InlineData("B;a", "A;b;c", false)]
        public void HashCode_WithFrameworkReferences(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                FrameworkReferences = left.Split(';')
                    .Select(e => new FrameworkDependency(e, FrameworkDependencyFlags.All))
                    .ToHashSet()
            };

            var rightSide = new TargetFrameworkInformation()
            {
                FrameworkReferences = right.Split(';')
                    .Select(e => new FrameworkDependency(e, FrameworkDependencyFlags.All))
                    .ToHashSet()
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("a;b", "a;b", true)]
        [InlineData("a;B", "A;b", true)]
        [InlineData("B;a", "A;b", true)]
        [InlineData("B;a;c", "A;b", false)]
        [InlineData("B;a", "A;b;c", false)]
        public void HashCode_WithCentralDependencies(string left, string right, bool expected)
        {
            var leftVersions = left.Split(';').ToDictionary(entry => entry, entry => new CentralPackageVersion(entry, VersionRange.All), StringComparer.OrdinalIgnoreCase);
            var leftSide = new TargetFrameworkInformation()
            {
                CentralPackageVersions = leftVersions,
                FrameworkName = NuGetFramework.AnyFramework
            };

            var rightVersions = right.Split(';').ToDictionary(entry => entry, entry => new CentralPackageVersion(entry, VersionRange.All), StringComparer.OrdinalIgnoreCase);
            var rightSide = new TargetFrameworkInformation()
            {
                CentralPackageVersions = rightVersions,
                FrameworkName = NuGetFramework.AnyFramework
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        [Theory]
        [InlineData("a;(,1.0.0];b;(,2.0.0]", "a;(,1.0.0];b;(,2.0.0]", true)]
        [InlineData("a;(,1.0.0];B;(,2.0.0]", "A;(,1.0.0];b;(,2.0.0]", true)]
        [InlineData("B;(,1.0.0];a;(,2.0.0]", "A;(,2.0.0];b;(,1.0.0]", true)]
        [InlineData("B;(,1.0.0];a;(,2.0.0];c;(,2.0.0]", "A;(,2.0.0];b;(,1.0.0]", false)]
        [InlineData("B;(,1.0.0];a;(,2.0.0]", "A;(,2.0.0];b;(,1.0.0];c;(,2.0.0]", false)]
        [InlineData("a;(,2.0.0];b;(,2.0.0]", "a;(,1.0.0];b;(,2.0.0]", false)]
        [InlineData("a;(,1.0.0];B;(,1.0.0]", "A;(,1.0.0];b;(,2.0.0]", false)]
        [InlineData("B;(,2.0.0];a;(,2.0.0]", "A;(,2.0.0];b;(,1.0.0]", false)]
        public void HashCode_PrunePackageReference(string left, string right, bool expected)
        {
            var leftSide = new TargetFrameworkInformation()
            {
                PackagesToPrune = GetPrunePackageReferences(left),
                FrameworkName = NuGetFramework.AnyFramework
            };

            var rightSide = new TargetFrameworkInformation()
            {
                PackagesToPrune = GetPrunePackageReferences(right),
                FrameworkName = NuGetFramework.AnyFramework
            };

            AssertHashCode(expected, leftSide, rightSide);
        }

        private static Dictionary<string, PrunePackageReference> GetPrunePackageReferences(string list)
        {
            Dictionary<string, PrunePackageReference> PrunePackageReferences = new(StringComparer.OrdinalIgnoreCase);
            var results = list.Split(';');
            for (int i = 0; i < results.Length; i = i + 2)
            {
                PrunePackageReferences.Add(results[i], new PrunePackageReference(results[i], VersionRange.Parse(results[i + 1])));
            }
            return PrunePackageReferences;
        }

        private TargetFrameworkInformation CreateTargetFrameworkInformation()
        {
            return CreateTargetFrameworkInformation(new List<CentralPackageVersion>() { new CentralPackageVersion("foo", VersionRange.All), new CentralPackageVersion("bar", VersionRange.AllStable) });
        }

        private TargetFrameworkInformation CreateTargetFrameworkInformation(List<CentralPackageVersion> centralVersionDependencies)
        {
            NuGetFramework nugetFramework = new NuGetFramework("net40");
            var dependencyFoo = new LibraryDependency(new LibraryRange("foo", VersionRange.All, LibraryDependencyTarget.All),
                LibraryIncludeFlags.All,
                LibraryIncludeFlags.All,
                noWarn: [],
                autoReferenced: true,
                generatePathProperty: true,
                versionCentrallyManaged: false,
                LibraryDependencyReferenceType.Direct,
                aliases: "Alias",
                versionOverride: null);

            var downloadDependency = new DownloadDependency("foo", VersionRange.All);
            var frameworkDependency = new FrameworkDependency("framework", FrameworkDependencyFlags.All);

            var assetTargetFallback = true;
            var warn = false;

            var versions = centralVersionDependencies.ToDictionary(cdep => cdep.Name, StringComparer.OrdinalIgnoreCase);
            TargetFrameworkInformation tfi = new TargetFrameworkInformation()
            {
                AssetTargetFallback = assetTargetFallback,
                CentralPackageVersions = versions,
                Dependencies = [dependencyFoo],
                DownloadDependencies = [downloadDependency],
                FrameworkName = nugetFramework,
                FrameworkReferences = [frameworkDependency],
                Warn = warn,
            };

            return tfi;
        }

        private static void AssertEquality(bool expected, TargetFrameworkInformation leftSide, TargetFrameworkInformation rightSide)
        {
            if (expected)
            {
                leftSide.Should().Be(rightSide);
            }
            else
            {
                leftSide.Should().NotBe(rightSide);
            }

            AssertClone(expected, leftSide, rightSide);
        }

        private static void AssertHashCode(bool expected, TargetFrameworkInformation leftSide, TargetFrameworkInformation rightSide)
        {
            if (expected)
            {
                leftSide.GetHashCode().Should().Be(rightSide.GetHashCode());
            }
            else
            {
                leftSide.GetHashCode().Should().NotBe(rightSide.GetHashCode());
            }

            AssertClone(expected, leftSide, rightSide);
        }

        private static void AssertClone(bool expected, TargetFrameworkInformation leftSide, TargetFrameworkInformation rightSide)
        {
            var leftClone = new TargetFrameworkInformation(leftSide);
            var rightClone = new TargetFrameworkInformation(rightSide);

            if (expected)
            {
                leftClone.GetHashCode().Should().Be(rightClone.GetHashCode());
                leftClone.Should().Be(rightClone);
            }
            else
            {
                leftClone.GetHashCode().Should().NotBe(rightClone.GetHashCode());
                leftClone.Should().NotBe(rightClone);
            }
        }
    }
}
