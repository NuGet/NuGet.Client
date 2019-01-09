// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;
using Xunit;
namespace NuGet.ProjectModel.Test
{
    public class PackageSpecCloningTests
    {
        private BuildOptions CreateBuildOptions()
        {
            var outputName = "OutputName";
            var originalBuildOptions = new BuildOptions();
            originalBuildOptions.OutputName = outputName;
            return originalBuildOptions;
        }

        [Fact]
        public void BuildOptionsCloneTest()
        {
            //Set up
            var originalBuildOptions = CreateBuildOptions();

            // Act
            var clonedBuildOptions = originalBuildOptions.Clone();

            //Assert
            Assert.Equal(originalBuildOptions.OutputName, clonedBuildOptions.OutputName);
            Assert.False(object.ReferenceEquals(originalBuildOptions, clonedBuildOptions));

        }

        internal static LibraryDependency CreateLibraryDependency()
        {
            var dependency = new LibraryDependency(
                libraryRange: new LibraryRange(Guid.NewGuid().ToString(), LibraryDependencyTarget.Package),
                type: LibraryDependencyType.Default,
                includeType: LibraryIncludeFlags.None,
                suppressParent: LibraryIncludeFlags.ContentFiles,
                noWarn: new List<NuGetLogCode>() { NuGetLogCode.NU1000, NuGetLogCode.NU1001, NuGetLogCode.NU1002 },
                autoReferenced: false,
                generatePathProperty: false
                );

            return dependency;
        }

        private PackageSpec CreatePackageSpec()
        {
            var originalTargetFrameworkInformation = CreateTargetFrameworkInformation();
            var PackageSpec = new PackageSpec(new List<TargetFrameworkInformation>() { originalTargetFrameworkInformation });
            PackageSpec.RestoreMetadata = CreateProjectRestoreMetadata();
            PackageSpec.FilePath = "FilePath";
            PackageSpec.Name = "Name";
            PackageSpec.Title = "Title";
            PackageSpec.Version = new Versioning.NuGetVersion("1.0.0");
            PackageSpec.HasVersionSnapshot = true;
            PackageSpec.Description = "Description";
            PackageSpec.Summary = "Summary";
            PackageSpec.ReleaseNotes = "ReleaseNotes";
            PackageSpec.Authors = new string[] { "Author1" };
            PackageSpec.Owners = new string[] { "Owner1" };
            PackageSpec.ProjectUrl = "ProjectUrl";
            PackageSpec.IconUrl = "IconUrl";
            PackageSpec.LicenseUrl = "LicenseUrl";
            PackageSpec.Copyright = "Copyright";
            PackageSpec.Language = "Language";
            PackageSpec.RequireLicenseAcceptance = true;
            PackageSpec.Tags = new string[] { "Tags" };
            PackageSpec.BuildOptions = CreateBuildOptions();
            PackageSpec.ContentFiles = new List<string>() { "contentFile1", "contentFile2" };
            PackageSpec.Dependencies = new List<LibraryDependency>() { CreateLibraryDependency(), CreateLibraryDependency() };

            PackageSpec.Scripts.Add(Guid.NewGuid().ToString(), new List<string>() { Guid.NewGuid().ToString() });
            PackageSpec.Scripts.Add(Guid.NewGuid().ToString(), new List<string>() { Guid.NewGuid().ToString() });

            PackageSpec.RuntimeGraph = CreateRuntimeGraph();
            PackageSpec.RestoreSettings = CreateProjectRestoreSettings();
            return PackageSpec;
        }

        public static RuntimeGraph CreateRuntimeGraph()
        {
            var runtimeDescription = new RuntimeDescription(Guid.NewGuid().ToString());
            var compatibilityProfile = CreateCompatibilityProfile("CompatibilityProfile");
            return new RuntimeGraph(new RuntimeDescription[] { runtimeDescription }, new CompatibilityProfile[] { compatibilityProfile });
        }

        [Theory]
        [InlineData("ModifyAuthors", true)]
        [InlineData("ModifyOriginalTargetFrameworkInformationAdd", true)]
        [InlineData("ModifyOriginalTargetFrameworkInformationEdit", true)]
        [InlineData("ModifyRestoreMetadata", true)]
        [InlineData("ModifyVersion", true)]
        [InlineData("ModifyOwners", true)]
        [InlineData("ModifyTags", true)]
        [InlineData("ModifyBuildOptions", false)]
        [InlineData("ModifyContentFiles", true)]
        [InlineData("ModifyDependencies", true)]
        [InlineData("ModifyScriptsAdd", true)]
        [InlineData("ModifyScriptsEdit", true)]
        [InlineData("ModifyPackInclude", true)]
        [InlineData("ModifyPackOptions", true)]
        [InlineData("ModifyRuntimeGraph", true)]
        //[InlineData("ModifyRestoreSettings", true)] = Not really included in the equals and hash code comparisons
        public void PackageSpecCloneTest(string methodName, bool validateJson)
        {
            // Set up
            var PackageSpec = CreatePackageSpec();
            var clonedPackageSpec = PackageSpec.Clone();

            //Preconditions
            Assert.Equal(PackageSpec, clonedPackageSpec);
            var originalPackageSpecWriter = new JsonObjectWriter();
            var clonedPackageSpecWriter = new JsonObjectWriter();
            PackageSpecWriter.Write(PackageSpec, originalPackageSpecWriter);
            PackageSpecWriter.Write(clonedPackageSpec, clonedPackageSpecWriter);
            Assert.Equal(originalPackageSpecWriter.GetJson().ToString(), clonedPackageSpecWriter.GetJson().ToString());
            Assert.False(object.ReferenceEquals(PackageSpec, clonedPackageSpec));

            // Act
            var methodInfo = typeof(PackageSpecModify).GetMethod(methodName);
            methodInfo.Invoke(null, new object[] { PackageSpec });

            // Assert

            Assert.NotEqual(PackageSpec, clonedPackageSpec);
            if (validateJson)
            {
                var oPackageSpecWriter = new JsonObjectWriter();
                var cPackageSpecWriter = new JsonObjectWriter();
                PackageSpecWriter.Write(PackageSpec, oPackageSpecWriter);
                PackageSpecWriter.Write(clonedPackageSpec, cPackageSpecWriter);
                Assert.NotEqual(oPackageSpecWriter.GetJson().ToString(), cPackageSpecWriter.GetJson().ToString());
            }
            Assert.False(object.ReferenceEquals(PackageSpec, clonedPackageSpec));
        }

        public class PackageSpecModify
        {

            public static void ModifyAuthors(PackageSpec packageSpec)
            {
                packageSpec.Authors[0] = "NewAuthor";
            }

            public static void ModifyOriginalTargetFrameworkInformationAdd(PackageSpec packageSpec)
            {
                packageSpec.TargetFrameworks.Add(CreateTargetFrameworkInformation("net40"));
            }

            public static void ModifyOriginalTargetFrameworkInformationEdit(PackageSpec packageSpec)
            {
                packageSpec.TargetFrameworks[0].Imports.Add(NuGetFramework.Parse("net461"));
            }

            public static void ModifyRestoreMetadata(PackageSpec packageSpec)
            {
                packageSpec.TargetFrameworks[0].Imports.Add(NuGetFramework.Parse("net461"));
            }

            public static void ModifyVersion(PackageSpec packageSpec)
            {
                packageSpec.Version = new Versioning.NuGetVersion("2.0.0");
            }

            public static void ModifyOwners(PackageSpec packageSpec)
            {
                packageSpec.Owners[0] = "BetterOwner";
            }

            public static void ModifyTags(PackageSpec packageSpec)
            {
                packageSpec.Tags[0] = "better tag!";
            }

            public static void ModifyBuildOptions(PackageSpec packageSpec)
            {
                packageSpec.BuildOptions.OutputName = Guid.NewGuid().ToString();
            }

            public static void ModifyContentFiles(PackageSpec packageSpec)
            {
                packageSpec.ContentFiles.Add("New fnacy content file");
            }

            public static void ModifyDependencies(PackageSpec packageSpec)
            {
                packageSpec.Dependencies.Add(CreateLibraryDependency());
            }

            public static void ModifyScriptsAdd(PackageSpec packageSpec)
            {
                packageSpec.Scripts.Add(Guid.NewGuid().ToString(), new List<string>() { Guid.NewGuid().ToString() });
            }

            public static void ModifyScriptsEdit(PackageSpec packageSpec)
            {
                var enumerator = packageSpec.Scripts.Keys.GetEnumerator();
                enumerator.MoveNext();
                var key = enumerator.Current;
                ((List<string>)packageSpec.Scripts[key]).Add(Guid.NewGuid().ToString());
            }

            public static void ModifyRuntimeGraph(PackageSpec packageSpec)
            {
                packageSpec.RuntimeGraph.Supports["CompatibilityProfile"].RestoreContexts.Add(CreateFrameworkRuntimePair(rid: "win10-x64"));
            }

            public static void ModifyRestoreSettings(PackageSpec packageSpec)
            {
                packageSpec.RestoreSettings.HideWarningsAndErrors = false;
            }

        }

        private ProjectRestoreMetadata CreateProjectRestoreMetadata()
        {
            var projectReference = new ProjectRestoreReference();
            projectReference.ProjectPath = "Path";
            projectReference.ProjectUniqueName = "ProjectUniqueName";
            projectReference.IncludeAssets = LibraryIncludeFlags.All;
            projectReference.ExcludeAssets = LibraryIncludeFlags.Analyzers;
            projectReference.PrivateAssets = LibraryIncludeFlags.Build;
            var nugetFramework = NuGetFramework.Parse("net461");
            var originalPRMFI = new ProjectRestoreMetadataFrameworkInfo(nugetFramework);
            originalPRMFI.ProjectReferences = new List<ProjectRestoreReference>() { projectReference };
            var targetframeworks = new List<ProjectRestoreMetadataFrameworkInfo>() { originalPRMFI };

            var allWarningsAsErrors = true;
            var noWarn = new HashSet<NuGetLogCode>() { NuGetLogCode.NU1000, NuGetLogCode.NU1500 };
            var warningsAsErrors = new HashSet<NuGetLogCode>() { NuGetLogCode.NU1001, NuGetLogCode.NU1501 };
            var warningProperties = new WarningProperties(allWarningsAsErrors: allWarningsAsErrors, warningsAsErrors: warningsAsErrors, noWarn: noWarn);

            var originalProjectRestoreMetadata = new ProjectRestoreMetadata();
            originalProjectRestoreMetadata.ProjectStyle = ProjectStyle.PackageReference;
            originalProjectRestoreMetadata.ProjectPath = "ProjectPath";
            originalProjectRestoreMetadata.ProjectJsonPath = "ProjectJsonPath";
            originalProjectRestoreMetadata.OutputPath = "OutputPath";
            originalProjectRestoreMetadata.ProjectName = "ProjectName";
            originalProjectRestoreMetadata.ProjectUniqueName = "ProjectUniqueName";
            originalProjectRestoreMetadata.PackagesPath = "PackagesPath";
            originalProjectRestoreMetadata.CacheFilePath = "CacheFilePath";
            originalProjectRestoreMetadata.CrossTargeting = true;
            originalProjectRestoreMetadata.LegacyPackagesDirectory = true;
            originalProjectRestoreMetadata.ValidateRuntimeAssets = true;
            originalProjectRestoreMetadata.SkipContentFileWrite = true;
            originalProjectRestoreMetadata.TargetFrameworks = targetframeworks;
            originalProjectRestoreMetadata.Sources = new List<PackageSource>() { new PackageSource("http://api.nuget.org/v3/index.json") }; ;
            originalProjectRestoreMetadata.FallbackFolders = new List<string>() { "fallback1" };
            originalProjectRestoreMetadata.ConfigFilePaths = new List<string>() { "config1" };
            originalProjectRestoreMetadata.OriginalTargetFrameworks = new List<string>() { "net45" };
            originalProjectRestoreMetadata.Files = new List<ProjectRestoreMetadataFile>() { new ProjectRestoreMetadataFile("packagePath", "absolutePath") };
            originalProjectRestoreMetadata.ProjectWideWarningProperties = warningProperties;

            return originalProjectRestoreMetadata;
        }

        [Fact]
        public void ProjectRestoreMetadataCloneTest()
        {
            // Set up
            var originalProjectRestoreMetadata = CreateProjectRestoreMetadata();
            // Act

            var happyClone = originalProjectRestoreMetadata.Clone();

            // Assert
            Assert.Equal(originalProjectRestoreMetadata, happyClone);
            Assert.False(object.ReferenceEquals(originalProjectRestoreMetadata, happyClone));
        }

        [Fact]
        public void ProjectRestoreMetadataCloneChangeSourcesTest()
        {
            // Set up
            var originalProjectRestoreMetadata = CreateProjectRestoreMetadata();

            // Preconditions
            var happyClone = originalProjectRestoreMetadata.Clone();
            Assert.Equal(originalProjectRestoreMetadata, happyClone);
            Assert.False(object.ReferenceEquals(originalProjectRestoreMetadata, happyClone));

            // Act
            originalProjectRestoreMetadata.Sources.Clear();

            // Assert
            Assert.NotEqual(originalProjectRestoreMetadata, happyClone);
            Assert.Equal(1, happyClone.Sources.Count);
        }

        [Fact]
        public void ProjectRestoreMetadataCloneChangeFallbackFoldersTest()
        {
            // Set up
            var originalProjectRestoreMetadata = CreateProjectRestoreMetadata();

            // Preconditions
            var happyClone = originalProjectRestoreMetadata.Clone();
            Assert.Equal(originalProjectRestoreMetadata, happyClone);
            Assert.False(object.ReferenceEquals(originalProjectRestoreMetadata, happyClone));

            // Act
            originalProjectRestoreMetadata.FallbackFolders.Clear();

            // Assert
            Assert.NotEqual(originalProjectRestoreMetadata, happyClone);
            Assert.Equal(1, happyClone.FallbackFolders.Count);
        }

        [Fact]
        public void ProjectRestoreMetadataCloneChangeConfigFilePathsTest()
        {
            // Set up
            var originalProjectRestoreMetadata = CreateProjectRestoreMetadata();

            // Preconditions
            var happyClone = originalProjectRestoreMetadata.Clone();
            Assert.Equal(originalProjectRestoreMetadata, happyClone);
            Assert.False(object.ReferenceEquals(originalProjectRestoreMetadata, happyClone));

            // Act
            originalProjectRestoreMetadata.ConfigFilePaths.Clear();

            // Assert
            Assert.NotEqual(originalProjectRestoreMetadata, happyClone);
            Assert.Equal(1, happyClone.ConfigFilePaths.Count);
        }

        [Fact]
        public void ProjectRestoreMetadataCloneChangeOriginalTargetFrameworksTest()
        {
            // Set up
            var originalProjectRestoreMetadata = CreateProjectRestoreMetadata();

            // Preconditions
            var happyClone = originalProjectRestoreMetadata.Clone();
            Assert.Equal(originalProjectRestoreMetadata, happyClone);
            Assert.False(object.ReferenceEquals(originalProjectRestoreMetadata, happyClone));

            // Act
            originalProjectRestoreMetadata.OriginalTargetFrameworks.Clear();

            // Assert
            Assert.NotEqual(originalProjectRestoreMetadata, happyClone);
            Assert.Equal(1, happyClone.OriginalTargetFrameworks.Count);
        }

        [Fact]
        public void ProjectRestoreMetadataCloneChangeFilesTest()
        {
            // Set up
            var originalProjectRestoreMetadata = CreateProjectRestoreMetadata();

            // Preconditions
            var happyClone = originalProjectRestoreMetadata.Clone();
            Assert.Equal(originalProjectRestoreMetadata, happyClone);
            Assert.False(object.ReferenceEquals(originalProjectRestoreMetadata, happyClone));

            // Act
            originalProjectRestoreMetadata.Files.Clear();

            // Assert
            Assert.NotEqual(originalProjectRestoreMetadata, happyClone);
            Assert.Equal(1, happyClone.Files.Count);
        }

        [Fact]
        public void ProjectRestoreMetadataCloneChangeProjectWideWarningPropertiesTest()
        {
            // Set up
            var originalProjectRestoreMetadata = CreateProjectRestoreMetadata();

            // Preconditions
            var happyClone = originalProjectRestoreMetadata.Clone();
            Assert.Equal(originalProjectRestoreMetadata, happyClone);
            Assert.False(object.ReferenceEquals(originalProjectRestoreMetadata, happyClone));

            // Act
            originalProjectRestoreMetadata.ProjectWideWarningProperties.AllWarningsAsErrors = false; ;

            // Assert
            Assert.NotEqual(originalProjectRestoreMetadata, happyClone);
            Assert.Equal(true, happyClone.ProjectWideWarningProperties.AllWarningsAsErrors);
        }

        [Fact]
        public void ProjectRestoreMetadataFileCloneTest()
        {
            // Set up
            var originalProjectRestoreMetadataFile = new ProjectRestoreMetadataFile("packagePath", "absolutePath");

            // Act
            var clone = originalProjectRestoreMetadataFile.Clone();

            // Assert
            Assert.Equal(originalProjectRestoreMetadataFile, clone);
            Assert.False(object.ReferenceEquals(originalProjectRestoreMetadataFile, clone));
        }

        [Fact]
        public void ProjectRestoreMetadataFrameworkInfoCloneTest()
        {
            //Set up
            var projectReference = new ProjectRestoreReference();
            projectReference.ProjectPath = "Path";
            projectReference.ProjectUniqueName = "ProjectUniqueName";
            projectReference.IncludeAssets = LibraryModel.LibraryIncludeFlags.All;
            projectReference.ExcludeAssets = LibraryModel.LibraryIncludeFlags.Analyzers;
            projectReference.PrivateAssets = LibraryModel.LibraryIncludeFlags.Build;

            var nugetFramework = NuGetFramework.Parse("net461");

            var originalPRMFI = new ProjectRestoreMetadataFrameworkInfo(nugetFramework);
            originalPRMFI.ProjectReferences = new List<ProjectRestoreReference>() { projectReference };

            // Act
            var clone = originalPRMFI.Clone();

            // Assert
            Assert.Equal(clone, originalPRMFI);
            Assert.False(object.ReferenceEquals(originalPRMFI, clone));

            // Act
            projectReference.ProjectPath = "NewPath";

            // Assert
            Assert.NotEqual(clone, originalPRMFI);
        }

        [Fact]
        public void ProjectRestoreReferenceCloneTest()
        {
            // Set up
            var originalProjectRestoreReference = new ProjectRestoreReference();
            originalProjectRestoreReference.ProjectPath = "Path";
            originalProjectRestoreReference.ProjectUniqueName = "ProjectUniqueName";
            originalProjectRestoreReference.IncludeAssets = LibraryModel.LibraryIncludeFlags.All;
            originalProjectRestoreReference.ExcludeAssets = LibraryModel.LibraryIncludeFlags.Analyzers;
            originalProjectRestoreReference.PrivateAssets = LibraryModel.LibraryIncludeFlags.Build;

            // Act
            var clone = originalProjectRestoreReference.Clone();

            // Assert
            Assert.Equal(originalProjectRestoreReference, clone);
            Assert.False(object.ReferenceEquals(originalProjectRestoreReference, clone));
        }

        private ProjectRestoreSettings CreateProjectRestoreSettings()
        {
            var prs = new ProjectRestoreSettings();
            prs.HideWarningsAndErrors = true;
            return prs;
        }

        [Fact]
        public void ProjectRestoreSettingsCloneTest()
        {
            // Set up
            var originalProjectRestoreSettings = CreateProjectRestoreSettings();

            // Act
            var clone = originalProjectRestoreSettings.Clone();

            // Assert
            Assert.Equal(originalProjectRestoreSettings, clone);
            Assert.False(object.ReferenceEquals(originalProjectRestoreSettings, clone));
        }

        internal static TargetFrameworkInformation CreateTargetFrameworkInformation(string tfm = "net461")
        {
            var framework = NuGetFramework.Parse(tfm);
            var dependency = new LibraryDependency(
                libraryRange: new LibraryRange("Dependency", LibraryDependencyTarget.Package),
                type: LibraryDependencyType.Default,
                includeType: LibraryIncludeFlags.None,
                suppressParent: LibraryIncludeFlags.ContentFiles,
                noWarn: new List<NuGetLogCode>() { NuGetLogCode.NU1000, NuGetLogCode.NU1001 },
                autoReferenced: false,
                generatePathProperty: false);
            var imports = NuGetFramework.Parse("net45"); // This makes no sense in the context of fallback, just for testing :)

            var originalTargetFrameworkInformation = new TargetFrameworkInformation();
            originalTargetFrameworkInformation.FrameworkName = framework;
            originalTargetFrameworkInformation.Dependencies = new List<LibraryDependency>() { dependency };
            originalTargetFrameworkInformation.AssetTargetFallback = false;
            originalTargetFrameworkInformation.Imports = new List<NuGetFramework>() { imports };
            return originalTargetFrameworkInformation;
        }

        [Fact]
        public void TargetFrameworkInformationCloneTest()
        {
            // Set up
            var originalTargetFrameworkInformation = CreateTargetFrameworkInformation();

            // Act
            var clone = originalTargetFrameworkInformation.Clone();

            // Assert
            Assert.Equal(originalTargetFrameworkInformation, clone);
            Assert.False(object.ReferenceEquals(originalTargetFrameworkInformation, clone));

            // Act
            originalTargetFrameworkInformation.Imports.Clear();

            // Assert
            Assert.NotEqual(originalTargetFrameworkInformation, clone);
            Assert.Equal(1, clone.Imports.Count);

            //Act
            var cloneToTestDependencies = originalTargetFrameworkInformation.Clone();

            // Assert
            Assert.Equal(originalTargetFrameworkInformation, cloneToTestDependencies);
            Assert.False(object.ReferenceEquals(originalTargetFrameworkInformation, cloneToTestDependencies));

            // Act
            originalTargetFrameworkInformation.Dependencies.Clear();

            // Assert
            Assert.NotEqual(originalTargetFrameworkInformation, cloneToTestDependencies);
            Assert.Equal(1, cloneToTestDependencies.Dependencies.Count);
        }

        [Fact]
        public void WarningPropertiesCloneTest()
        {
            // Set up
            var allWarningsAsErrors = false;
            var noWarn = new HashSet<NuGetLogCode>() { NuGetLogCode.NU1000, NuGetLogCode.NU1500 };
            var warningsAsErrors = new HashSet<NuGetLogCode>() { NuGetLogCode.NU1001, NuGetLogCode.NU1501 };
            var originalWarningProperties = new WarningProperties(allWarningsAsErrors: allWarningsAsErrors, warningsAsErrors: warningsAsErrors, noWarn: noWarn);

            //Act
            var clone = originalWarningProperties.Clone();

            // Assert
            Assert.Equal(originalWarningProperties, clone);
            Assert.False(object.ReferenceEquals(originalWarningProperties, clone));
            Assert.False(object.ReferenceEquals(noWarn, clone.NoWarn));

            // Act again
            noWarn.Clear();

            //Assert again
            Assert.NotEqual(originalWarningProperties, clone);
            Assert.Equal(2, clone.NoWarn.Count);
        }

        private static FrameworkRuntimePair CreateFrameworkRuntimePair(string tfm = "net461", string rid = "win-x64")
        {
            return new FrameworkRuntimePair(NuGetFramework.Parse(tfm), rid);
        }

        [Fact]
        public void FrameworkRuntimePairCloneTest()
        {
            //Setup
            var frp = CreateFrameworkRuntimePair();
            //Act
            var clone = frp.Clone();
            //Assert
            Assert.Equal(frp, clone);
            Assert.False(object.ReferenceEquals(frp, clone));
        }

        private static CompatibilityProfile CreateCompatibilityProfile(string name, string tfm = "net461")
        {
            return new CompatibilityProfile(name, new FrameworkRuntimePair[] { CreateFrameworkRuntimePair(tfm, "win-x64"), CreateFrameworkRuntimePair(tfm, "win-x86") });
        }

        [Fact]
        public void CompatibilityProfileCloneTest()
        {
            // Setup
            var compat = CreateCompatibilityProfile("bla");
            //Act
            var clone = compat.Clone();
            //Assert
            Assert.Equal(compat, clone);
            Assert.False(object.ReferenceEquals(compat, clone));
            Assert.False(object.ReferenceEquals(compat.RestoreContexts[0], clone.RestoreContexts[0]));

            // Act - Change the list of compat
            compat.RestoreContexts.Add(CreateFrameworkRuntimePair(rid: "win10-x64"));

            // Assert
            Assert.NotEqual(compat, clone);
            Assert.Equal(3, compat.RestoreContexts.Count);
            Assert.Equal(2, clone.RestoreContexts.Count);
        }

    }
}
