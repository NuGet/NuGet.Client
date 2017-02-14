// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.RuntimeModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class PackageSpecWriterTests
    {
        private static readonly PackageSpec _emptyPackageSpec = JsonPackageSpecReader.GetPackageSpec(new JObject());

        [Fact]
        public void RoundTripAutoReferencedProperty()
        {
            // Arrange
            var json = @"{
                    ""dependencies"": {
                        ""b"": {
                            ""version"": ""1.0.0"",
                            ""autoReferenced"": true
                        }
                    },
                  ""frameworks"": {
                    ""net46"": {
                        ""dependencies"": {
                            ""a"": {
                                ""version"": ""1.0.0"",
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
        public void Write_ThrowsForNullPackageSpec()
        {
            var writer = new JsonObjectWriter();

            Assert.Throws<ArgumentNullException>(() => PackageSpecWriter.Write(packageSpec: null, writer: writer));
        }

        [Fact]
        public void Write_ThrowsForNullWriter()
        {
            Assert.Throws<ArgumentNullException>(() => PackageSpecWriter.Write(_emptyPackageSpec, writer: null));
        }

        [Fact]
        public void Write_ReadWriteDependencies()
        {
            // Arrange
            var json = @"{
  ""title"": ""My Title"",
  ""version"": ""1.2.3"",
  ""description"": ""test"",
  ""authors"": [
    ""author1"",
    ""author2""
  ],
  ""copyright"": ""2016"",
  ""language"": ""en-US"",
  ""packInclude"": {
    ""file"": ""file.txt""
  },
  ""packOptions"": {
    ""owners"": [
      ""owner1"",
      ""owner2""
    ],
    ""tags"": [
      ""tag1"",
      ""tag2""
    ],
    ""projectUrl"": ""http://my.url.com"",
    ""iconUrl"": ""http://my.url.com"",
    ""summary"": ""Sum"",
    ""releaseNotes"": ""release noted"",
    ""licenseUrl"": ""http://my.url.com""
  },
  ""scripts"": {
    ""script1"": [
      ""script.js""
    ]
  },
  ""dependencies"": {
    ""packageA"": {
      ""suppressParent"": ""All"",
      ""target"": ""Project""
    }
  },
  ""frameworks"": {
    ""net46"": {}
  }
}";
            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_ReadWriteSinglePackageType()
        {
            // Arrange
            var json = @"{
  ""packOptions"": {
    ""packageType"": ""DotNetTool""
  }
}";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void Write_ReadWriteMultiplePackageType()
        {
            // Arrange
            var json = @"{
  ""packOptions"": {
    ""packageType"": [
      ""Dependency"",
      ""DotNetTool""
    ]
  }
}";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void WriteToFile_ThrowsForNullPackageSpec()
        {
            Assert.Throws<ArgumentNullException>(() => PackageSpecWriter.WriteToFile(packageSpec: null, filePath: @"C:\a.json"));
        }

        [Fact]
        public void WriteToFile_ThrowsForNullFilePath()
        {
            Assert.Throws<ArgumentException>(() => PackageSpecWriter.WriteToFile(_emptyPackageSpec, filePath: null));
        }

        [Fact]
        public void WriteToFile_ThrowsForEmptyFilePath()
        {
            Assert.Throws<ArgumentException>(() => PackageSpecWriter.WriteToFile(_emptyPackageSpec, filePath: null));
        }

        [Fact]
        public void Write_SerializesMembersAsJson()
        {
            var expectedJson = ResourceTestUtility.GetResource("NuGet.ProjectModel.Test.compiler.resources.PackageSpecWriter_Write_SerializesMembersAsJson.json", typeof(PackageSpecWriterTests));
            var packageSpec = CreatePackageSpec();
            var actualJson = GetJson(packageSpec);

            Assert.Equal(expectedJson, actualJson);
        }

        private static string GetJson(PackageSpec packageSpec)
        {
            var writer = new JsonObjectWriter();

            PackageSpecWriter.Write(packageSpec, writer);

            return writer.GetJson();
        }

        private static PackageSpec CreatePackageSpec()
        {
            var unsortedArray = new[] { "b", "a", "c" };
            var unsortedReadOnlyList = new List<string>(unsortedArray).AsReadOnly();
            var libraryRange = new LibraryRange("range", new VersionRange(new NuGetVersion("1.2.3")), LibraryDependencyTarget.Package);
            var libraryDependency = new LibraryDependency() { IncludeType = LibraryIncludeFlags.Build, LibraryRange = libraryRange };
            var nugetFramework = new NuGetFramework("frameworkIdentifier", new Version("1.2.3"), "frameworkProfile");

            var packageSpec = new PackageSpec()
            {
                Authors = unsortedArray,
                BuildOptions = new BuildOptions() { OutputName = "outputName" },
                ContentFiles = new List<string>(unsortedArray),
                Copyright = "copyright",
                Dependencies = new List<LibraryDependency>() { libraryDependency },
                Description = "description",
                FilePath = "filePath",
                HasVersionSnapshot = true,
                IconUrl = "iconUrl",
                IsDefaultVersion = false,
                Language = "language",
                LicenseUrl = "licenseUrl",
                Name = "name",
                Owners = unsortedArray,
                PackOptions = new PackOptions()
                {
                    IncludeExcludeFiles = new IncludeExcludeFiles()
                    {
                        Exclude = unsortedReadOnlyList,
                        ExcludeFiles = unsortedReadOnlyList,
                        Include = unsortedReadOnlyList,
                        IncludeFiles = unsortedReadOnlyList
                    }
                },
                ProjectUrl = "projectUrl",
                ReleaseNotes = "releaseNotes",
                RequireLicenseAcceptance = true,
                RestoreMetadata = new ProjectRestoreMetadata()
                {
                    CrossTargeting = true,
                    FallbackFolders = unsortedReadOnlyList,
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
                Summary = "summary",
                Tags = unsortedArray,
                Title = "title",
                Version = new NuGetVersion("1.2.3")
            };

            packageSpec.PackInclude.Add("b", "d");
            packageSpec.PackInclude.Add("a", "e");
            packageSpec.PackInclude.Add("c", "f");

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

            packageSpec.Scripts.Add("b", unsortedArray);
            packageSpec.Scripts.Add("a", unsortedArray);
            packageSpec.Scripts.Add("c", unsortedArray);

            packageSpec.TargetFrameworks.Add(new TargetFrameworkInformation()
            {
                Dependencies = new List<LibraryDependency>() { libraryDependency },
                FrameworkName = nugetFramework,
                Imports = new List<NuGetFramework>() { nugetFramework },
                Warn = true
            });

            return packageSpec;
        }

        private static void VerifyJsonPackageSpecRoundTrip(string json)
        {
            // Arrange & Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "testName", @"C:\fake\path");

            var writer = new JsonObjectWriter();
            PackageSpecWriter.Write(spec, writer);

            var actualResult = writer.GetJson();

            var expected = JObject.Parse(json).ToString();

            // Assert
            Assert.Equal(expected, actualResult);
        }
    }
}