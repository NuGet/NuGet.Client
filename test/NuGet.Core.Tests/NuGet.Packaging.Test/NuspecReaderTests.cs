// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using NuGet.Frameworks;
using Xunit;
using NuGet.Packaging.Core;
using System.Collections.Generic;
using NuGet.Versioning;
using FluentAssertions;

namespace NuGet.Packaging.Test
{
    public class NuspecReaderTests
    {
        private const string DuplicateGroups = @"<?xml version=""1.0""?>
                    <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                      <metadata>
                        <id>packageA</id>
                        <version>1.0.1-alpha</version>
                        <title>Package A</title>
                        <authors>ownera, ownerb</authors>
                        <owners>ownera, ownerb</owners>
                        <requireLicenseAcceptance>false</requireLicenseAcceptance>
                        <description>package A description.</description>
                        <language>en-US</language>
                        <dependencies>
                          <group targetFramework="".NETPortable0.0-net403+sl5+netcore45+wp8+MonoAndroid1+MonoTouch1"">
                            <dependency id=""Microsoft.Bcl.Async"" />
                            <dependency id=""Microsoft.Net.Http"" />
                            <dependency id=""Microsoft.Bcl.Build"" />
                          </group>
                          <group targetFramework="".NETPortable0.0-net403+sl5+netcore45+wp8"">
                            <dependency id=""Microsoft.Bcl.Async"" />
                            <dependency id=""Microsoft.Net.Http"" />
                            <dependency id=""Microsoft.Bcl.Build"" />
                          </group>
                        </dependencies>
                      </metadata>
                    </package>";

        private const string BasicNuspec = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                      <reference file=""a.dll"" />
                    </references>
                    <dependencies>
                        <group targetFramework=""net40"">
                          <dependency id=""jQuery"" />
                          <dependency id=""WebActivator"" version=""1.1.0"" />
                          <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                        </group>
                        <group targetFramework=""wp8"">
                          <dependency id=""jQuery"" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        private const string EmptyGroups = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                        <group>
                            <reference file=""a.dll"" />
                        </group>
                        <group targetFramework=""net45"" />
                    </references>
                    <dependencies>
                        <group targetFramework=""net40"">
                          <dependency id=""jQuery"" />
                          <dependency id=""WebActivator"" version=""1.1.0"" />
                          <dependency id=""PackageC"" version=""[1.1.0, 2.0.1)"" />
                        </group>
                        <group targetFramework=""net45"" />
                    </dependencies>
                  </metadata>
                </package>";

        // from: https://nuget.codeplex.com/wikipage?title=.nuspec%20v1.2%20Format
        private const string CommaDelimitedFrameworksNuspec = @"<?xml version=""1.0""?>
                <package>
                  <metadata>
                    <id>PackageWithGacReferences</id>
                    <version>1.0</version>
                    <authors>Author here</authors>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>A package that has framework assemblyReferences depending on the target framework.</description>
                    <frameworkAssemblies>
                      <frameworkAssembly assemblyName=""System.Web"" targetFramework=""net40"" />
                      <frameworkAssembly assemblyName=""System.Net"" targetFramework=""net40-client, net40"" />
                      <frameworkAssembly assemblyName=""Microsoft.Devices.Sensors"" targetFramework=""sl4-wp"" />
                      <frameworkAssembly assemblyName=""System.Json"" targetFramework=""sl3"" />
                      <frameworkAssembly assemblyName=""System.Windows.Controls.DomainServices"" targetFramework=""sl4"" />
                    </frameworkAssemblies>
                  </metadata>
                </package>";

        private const string NamespaceOnMetadataNuspec = @"<?xml version=""1.0""?>
                <package xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                    <metadata xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                    <id>packageB</id>
                    <version>1.0</version>
                    <authors>nuget</authors>
                    <owners>nuget</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>test</description>
                    </metadata>
                </package>";

        private const string UnknownDependencyGroupsNuspec = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                      <reference file=""a.dll"" />
                    </references>
                    <dependencies>
                        <group targetFramework=""net45"">
                          <dependency id=""jQuery"" />
                        </group>
                        <group targetFramework=""future51"">
                          <dependency id=""jQuery"" />
                        </group>
                        <group targetFramework=""future50"">
                          <dependency id=""jQuery"" />
                        </group>
                        <group targetFramework=""futurevnext10.0"">
                          <dependency id=""jQuery"" />
                        </group>
                        <group targetFramework=""some4~new5^conventions10"">
                          <dependency id=""jQuery"" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        private const string IncludeExcludeNuspec = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <requireLicenseAcceptance>false</requireLicenseAcceptance>
                    <description>package A description.</description>
                    <language>en-US</language>
                    <references>
                      <reference file=""a.dll"" />
                    </references>
                    <dependencies>
                        <group>
                          <dependency id=""emptyValues"" version=""1.0.0"" include="""" exclude="""" />
                          <dependency id=""noAttributes"" version=""1.0.0"" />
                          <dependency id=""packageA"" version=""1.0.0"" include=""all"" exclude=""none"" />
                          <dependency id=""packageB"" version=""1.0.0"" include=""runtime,compile,unknown"" />
                          <dependency id=""packageC"" version=""1.0.0"" exclude=""compile,runtime"" />
                          <dependency id=""packageD"" version=""1.0.0"" exclude=""a,,b"" />
                          <dependency id=""packageE"" version=""1.0.0"" include=""a , b ,c "" />
                        </group>
                    </dependencies>
                  </metadata>
                </package>";

        private const string PackageTypes = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <description>package A description.</description>
                    <packageTypes>
                      <packageType name=""foo"" />
                      <packageType name=""bar"" version=""2.0.0"" />
                    </packageTypes>
                  </metadata>
                </package>";

        private const string NoContainerPackageTypesElement = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <description>package A description.</description>
                    <packageType name=""foo"" />
                    <packageType name=""bar"" version=""2.0.0"" />
                  </metadata>
                </package>";

        private const string EmptyPackageTypesElement = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <description>package A description.</description>
                    <packageTypes>
                    </packageTypes>
                  </metadata>
                </package>";

        private const string NoPackageTypesElement = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <description>package A description.</description>
                  </metadata>
                </package>";

        private const string ServiceablePackageTypesElement = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2016/06/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <description>package A description.</description>
                    <serviceable>true</serviceable>
                  </metadata>
                </package>";

        private const string VersionRangeInDependency = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd"">
                  <metadata>
                    <id>PackageA</id>
                    <version>2.0.1.0</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <description>Package A description</description>
                    <tags>ServiceStack Serilog</tags>
                    <dependencies>
                      <dependency id=""PackageB"" version=""{0}"" />
                    </dependencies>
                  </metadata>
                </package>";

        private const string RepositoryBasic = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2016/06/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <description>package A description.</description>
                    <repository type=""git"" url=""https://github.com/NuGet/NuGet.Client.git"" />
                  </metadata>
                </package>";

        private const string RepositoryComplete = @"<?xml version=""1.0""?>
                <package xmlns=""http://schemas.microsoft.com/packaging/2016/06/nuspec.xsd"">
                  <metadata>
                    <id>packageA</id>
                    <version>1.0.1-alpha</version>
                    <title>Package A</title>
                    <authors>ownera, ownerb</authors>
                    <owners>ownera, ownerb</owners>
                    <description>package A description.</description>
                    <repository type=""git"" url=""https://github.com/NuGet/NuGet.Client.git"" branch=""dev"" commit=""e1c65e4524cd70ee6e22abe33e6cb6ec73938cb3"" />
                  </metadata>
                </package>";

        public static IEnumerable<object[]> GetValidVersions()
        {
            return GetVersionRange(validVersions: true);
        }

        public static IEnumerable<object[]> GetInValidVersions()
        {
            return GetVersionRange(validVersions: false);
        }

        private static IEnumerable<object[]> GetVersionRange(bool validVersions)
        {
            var range = validVersions
                ? ValidVersionRange()
                : InvalidVersionRange();

            foreach (var s in range)
            {
                yield return new object[] { s };
            }
        }

        private static IEnumerable<string> ValidVersionRange()
        {
            yield return "0.0.0";
            yield return "1.0.0-beta";
            yield return "1.0.1-alpha.1.2.3";
            yield return "1.0.1-alpha.1.2.3+a.b.c.d";
            yield return "1.0.1+metadata";
            yield return "1.0.1--";
            yield return "1.0.1-a.really.long.version.release.label";
            yield return "00.00.00.00-alpha";
            yield return "0.0-alpha.1";
            yield return "(1.0.0-alpha.1, )";
            yield return "[1.0.0-alpha.1+metadata]";
            yield return "[1.0, 2.0.0+metadata)";
            yield return "[1.0+metadata, 2.0.0+metadata)";
        }

        private static IEnumerable<string> InvalidVersionRange()
        {
            yield return null;
            yield return string.Empty;
            yield return " ";
            yield return "\t";
            yield return "~1";
            yield return "~1.0.0";
            yield return "0.0.0-~4";
            yield return "$version$";
            yield return "Invalid";
            yield return "[15.106.0.preview]";
            yield return "15.106.0-preview.01"; // no leading zeros in numeric identifiers of release label
        }

        [Fact]
        public void NuspecReaderTests_NamespaceOnMetadata()
        {
            var reader = GetReader(NamespaceOnMetadataNuspec);

            var id = reader.GetId();

            Assert.Equal("packageB", id);
        }

        [Fact]
        public void NuspecReaderTests_Id()
        {
            var reader = GetReader(BasicNuspec);

            var id = reader.GetId();

            Assert.Equal("packageA", id);
        }

        [Fact]
        public void NuspecReaderTests_EmptyGroups()
        {
            var reader = GetReader(EmptyGroups);

            var dependencies = reader.GetDependencyGroups().ToList();
            var references = reader.GetReferenceGroups().ToList();

            Assert.Equal(2, dependencies.Count);
            Assert.Equal(2, references.Count);
        }

        [Fact]
        public void NuspecReaderTests_DependencyGroups()
        {
            var reader = GetReader(BasicNuspec);

            var dependencies = reader.GetDependencyGroups().ToList();

            Assert.Equal(2, dependencies.Count);
        }

        [Fact]
        public void NuspecReaderTests_DuplicateDependencyGroups()
        {
            var reader = GetReader(DuplicateGroups);

            var dependencies = reader.GetDependencyGroups().ToList();

            Assert.Equal(2, dependencies.Count);
        }

        [Theory]
        [MemberData(nameof(GetValidVersions))]
        [MemberData(nameof(GetInValidVersions))]
        public void NuspecReaderTests_NonStrictCheckInDependencyShouldNotThrowException(string versionRange)
        {
            // Arrange
            var formattedNuspec = string.Format(VersionRangeInDependency, versionRange);
            var nuspecReader = GetReader(formattedNuspec);

            // Act
            var dependencies = nuspecReader.GetDependencyGroups().ToList();

            // Assert
            Assert.Equal(1, dependencies.Count);
        }

        [Theory]
        [MemberData(nameof(GetInValidVersions))]
        public void NuspecReaderTests_NonStrictCheckInDependencyShouldFallbackToAllRangeForInvalidVersions(
            string versionRange)
        {
            // Arrange
            var formattedNuspec = string.Format(VersionRangeInDependency, versionRange);
            var nuspecReader = GetReader(formattedNuspec);

            // Act
            var dependencies = nuspecReader.GetDependencyGroups().ToList();

            // Assert
            Assert.Equal(VersionRange.All, dependencies.First().Packages.First().VersionRange);
        }

        [Theory]
        [MemberData(nameof(GetInValidVersions))]
        public void NuspecReaderTests_InvalidVersionRangeInDependencyThrowsExceptionForStrictCheck(string versionRange)
        {
            // Arrange
            var formattedNuspec = string.Format(VersionRangeInDependency, versionRange);
            var nuspecReader = GetReader(formattedNuspec);
            Action action = () => nuspecReader.GetDependencyGroups(useStrictVersionCheck: true).ToList();

            // Act & Assert
            Assert.Throws<PackagingException>(action);
        }

        [Theory]
        [MemberData(nameof(GetValidVersions))]
        public void NuspecReaderTests_ValidVersionRangeInDependencyReturnsResultForStrictCheck(string versionRange)
        {
            // Arrange
            var formattedNuspec = string.Format(VersionRangeInDependency, versionRange);
            var nuspecReader = GetReader(formattedNuspec);
            var expectedVersionRange = string.IsNullOrEmpty(versionRange) ? VersionRange.All : VersionRange.Parse(versionRange);

            // Act
            var dependencyGroups = nuspecReader.GetDependencyGroups(useStrictVersionCheck: true).ToList();

            // Assert
            Assert.Equal(1, dependencyGroups.Count);
            Assert.Equal(expectedVersionRange, dependencyGroups.First().Packages.First().VersionRange);
        }

        [Fact]
        public void NuspecReaderTests_FrameworkGroups()
        {
            var reader = GetReader(CommaDelimitedFrameworksNuspec);

            var dependencies = reader.GetFrameworkReferenceGroups().ToList();

            Assert.Equal(5, dependencies.Count);
        }

        [Fact]
        public void NuspecReaderTests_FrameworkSplitGroup()
        {
            var reader = GetReader(CommaDelimitedFrameworksNuspec);

            var groups = reader.GetFrameworkReferenceGroups();

            var group = groups.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("net40")));

            Assert.Equal(2, group.Items.Count());

            Assert.Equal("System.Net", group.Items.ToArray()[0]);
            Assert.Equal("System.Web", group.Items.ToArray()[1]);
        }

        [Fact]
        public void NuspecReaderTests_Language()
        {
            var reader = GetReader(BasicNuspec);

            var language = reader.GetLanguage();

            Assert.Equal("en-US", language);
        }

        [Fact]
        public void NuspecReaderTests_UnsupportedDependencyGroups()
        {
            var reader = GetReader(UnknownDependencyGroupsNuspec);

            // verify we can handle multiple unsupported dependency groups gracefully
            var dependencies = reader.GetDependencyGroups().ToList();

            // unsupported frameworks remain ungrouped
            Assert.Equal(5, dependencies.Count);

            Assert.Equal(4, dependencies.Count(g => g.TargetFramework == NuGetFramework.UnsupportedFramework));
        }

        [Fact]
        public void NuspecReaderTests_DependencyWithSingleIncludeExclude()
        {
            // Arrange
            var reader = GetReader(IncludeExcludeNuspec);

            // Act
            var group = reader.GetDependencyGroups().Single();
            var dependency = group.Packages.Single(package => package.Id == "packageA");

            // Assert
            Assert.Equal("all", string.Join("|", dependency.Include.OrderBy(s => s)));
            Assert.Equal("none", string.Join("|", dependency.Exclude.OrderBy(s => s)));
        }

        [Fact]
        public void NuspecReaderTests_DependencyWithMultipleInclude()
        {
            // Arrange
            var reader = GetReader(IncludeExcludeNuspec);

            // Act
            var group = reader.GetDependencyGroups().Single();
            var dependency = group.Packages.Single(package => package.Id == "packageB");

            // Assert
            Assert.Equal("compile|runtime|unknown", string.Join("|", dependency.Include.OrderBy(s => s)));
            Assert.Equal(0, dependency.Exclude.Count);
        }

        [Fact]
        public void NuspecReaderTests_DependencyWithWhiteSpace()
        {
            // Arrange
            var reader = GetReader(IncludeExcludeNuspec);

            // Act
            var group = reader.GetDependencyGroups().Single();
            var dependency = group.Packages.Single(package => package.Id == "packageE");

            // Assert
            Assert.Equal("a|b|c", string.Join("|", dependency.Include.OrderBy(s => s)));
            Assert.Equal(0, dependency.Exclude.Count);
        }

        [Fact]
        public void NuspecReaderTests_DependencyWithMultipleExclude()
        {
            // Arrange
            var reader = GetReader(IncludeExcludeNuspec);

            // Act
            var group = reader.GetDependencyGroups().Single();
            var dependency = group.Packages.Single(package => package.Id == "packageC");

            // Assert
            Assert.Equal(0, dependency.Include.Count);
            Assert.Equal("compile|runtime", string.Join("|", dependency.Exclude.OrderBy(s => s)));
        }

        [Fact]
        public void NuspecReaderTests_DependencyWithBlankExclude()
        {
            // Arrange
            var reader = GetReader(IncludeExcludeNuspec);

            // Act
            var group = reader.GetDependencyGroups().Single();
            var dependency = group.Packages.Single(package => package.Id == "packageD");

            // Assert bad flags stay as is
            Assert.Equal(0, dependency.Include.Count);
            Assert.Equal("a|b", string.Join("|", dependency.Exclude.OrderBy(s => s)));
        }

        [Fact]
        public void NuspecReaderTests_DependencyNoAttributesForIncludeExclude()
        {
            // Arrange
            var reader = GetReader(IncludeExcludeNuspec);

            // Act
            var group = reader.GetDependencyGroups().Single();
            var dependency = group.Packages.Single(package => package.Id == "noAttributes");

            // Assert
            Assert.Equal(0, dependency.Include.Count);
            Assert.Equal(0, dependency.Exclude.Count);
        }

        [Fact]
        public void NuspecReaderTests_DependencyEmptyAttributesForIncludeExclude()
        {
            // Arrange
            var reader = GetReader(IncludeExcludeNuspec);

            // Act
            var group = reader.GetDependencyGroups().Single();
            var dependency = group.Packages.Single(package => package.Id == "emptyValues");

            // Assert
            Assert.Equal(0, dependency.Include.Count);
            Assert.Equal(0, dependency.Exclude.Count);
        }

        [Fact]
        public void NuspecReaderTests_PackageTypes()
        {
            // Arrange
            var reader = GetReader(PackageTypes);

            // Act
            var actual = reader.GetPackageTypes();

            // Assert
            Assert.Equal(2, actual.Count);
            Assert.Equal("foo", actual[0].Name);
            Assert.Equal(new Version(0, 0), actual[0].Version);
            Assert.Equal("bar", actual[1].Name);
            Assert.Equal(new Version(2, 0, 0), actual[1].Version);
        }

        [Theory]
        [InlineData(NoContainerPackageTypesElement)]
        [InlineData(EmptyPackageTypesElement)]
        [InlineData(NoPackageTypesElement)]
        public void NuspecReaderTests_NoPackageTypes(string nuspec)
        {
            // Arrange
            var reader = GetReader(nuspec);

            // Act
            var actual = reader.GetPackageTypes();

            // Assert
            Assert.Equal(0, actual.Count);
        }

        [Fact]
        public void NuspecReaderTests_ServiceablePackage()
        {
            // Arrange
            var reader = GetReader(ServiceablePackageTypesElement);

            // Act
            var actual = reader.IsServiceable();

            // Assert
            Assert.True(actual);
        }

        [Fact]
        public void NuspecReaderTests_RepositoryVerifyBlank()
        {
            // Arrange
            var reader = GetReader(ServiceablePackageTypesElement);

            // Act
            var repo = reader.GetRepositoryMetadata();

            // Assert
            repo.Branch.Should().BeEmpty();
            repo.Type.Should().BeEmpty();
            repo.Url.Should().BeEmpty();
            repo.Commit.Should().BeEmpty();
        }

        [Fact]
        public void NuspecReaderTests_RepositoryComplete()
        {
            // Arrange
            var reader = GetReader(RepositoryComplete);

            // Act
            var repo = reader.GetRepositoryMetadata();

            // Assert
            repo.Branch.Should().Be("dev");
            repo.Type.Should().Be("git");
            repo.Url.Should().Be("https://github.com/NuGet/NuGet.Client.git");
            repo.Commit.Should().Be("e1c65e4524cd70ee6e22abe33e6cb6ec73938cb3");
        }

        [Fact]
        public void NuspecReaderTests_RepositoryBasic()
        {
            // Arrange
            var reader = GetReader(RepositoryBasic);

            // Act
            var repo = reader.GetRepositoryMetadata();

            // Assert
            repo.Type.Should().Be("git");
            repo.Url.Should().Be("https://github.com/NuGet/NuGet.Client.git");
            repo.Branch.Should().BeEmpty();
            repo.Commit.Should().BeEmpty();
        }

        private static NuspecReader GetReader(string nuspec)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(nuspec)))
            {
                return new NuspecReader(stream);
            }
        }
    }
}
