// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
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
        }

        [Fact]
        public void IncludeExcludeFilesCloneTest()
        {
            //Set up
            var exclude = new List<string>() { "Exlclude0" };
            var include = new List<string>() { "Include0" };
            var includeFiles = new List<string>() { "IncludeFiles0" };
            var excludeFiles = new List<string>() { "ExlcludeFiles0" };

            var files = new IncludeExcludeFiles();
            files.Exclude = exclude;
            files.Include = include;
            files.IncludeFiles = includeFiles;
            files.ExcludeFiles = excludeFiles;

            // Act
            var clone = files.Clone();
            //Assert

            Assert.Equal(files.Exclude, clone.Exclude);
            Assert.Equal(files.Include, clone.Include);
            Assert.Equal(files.IncludeFiles, clone.IncludeFiles);
            Assert.Equal(files.ExcludeFiles, clone.ExcludeFiles);

            // Act again
            exclude.Add("Extra Exclude");

            //Assert
            Assert.Equal(2, files.Exclude.Count);
            Assert.NotEqual(files.Exclude, clone.Exclude);
        }

        [Fact]
        public void PackOptionsCloneTest()
        {
            //Set up
            var originalPackOptions = new PackOptions();
            var originalPackageName = "PackageA";
            var packageTypes = new List<NuGet.Packaging.Core.PackageType>() { new Packaging.Core.PackageType(originalPackageName, new System.Version("1.0.0")) };

            var exclude = new List<string>() { "Exlclude0" };
            var include = new List<string>() { "Include0" };
            var includeFiles = new List<string>() { "IncludeFiles0" };
            var excludeFiles = new List<string>() { "ExlcludeFiles0" };

            var files = new IncludeExcludeFiles();
            files.Exclude = exclude;
            files.Include = include;
            files.IncludeFiles = includeFiles;
            files.ExcludeFiles = excludeFiles;

            originalPackOptions.PackageType = packageTypes;
            originalPackOptions.IncludeExcludeFiles = files;

            // Act
            var clone = originalPackOptions.Clone();

            // Assert
            Assert.Equal(originalPackOptions, clone);

            // Act again
            packageTypes.Clear();

            // Assert
            Assert.NotEqual(originalPackOptions, clone);
            Assert.Equal(originalPackageName, clone.PackageType[0].Name);

            // Set Up again
            originalPackOptions.Mappings.Add("randomString", files);

            // Act again
            var cloneWithMappings = originalPackOptions.Clone();

            // Assert
            Assert.Equal(originalPackOptions, cloneWithMappings);

            // Act again
            originalPackOptions.Mappings.Clear();

            // Assert
            Assert.NotEqual(originalPackOptions, cloneWithMappings);
            Assert.Equal(1, cloneWithMappings.Mappings.Count);
        }

        private LibraryDependency CreateLibraryDependency()
        {
            var dependency = new LibraryDependency();
            dependency.LibraryRange = new LibraryRange(Guid.NewGuid().ToString(), LibraryDependencyTarget.Package);
            dependency.Type = LibraryDependencyType.Default;
            dependency.IncludeType = LibraryIncludeFlags.None;
            dependency.SuppressParent = LibraryIncludeFlags.ContentFiles;
            return dependency;
        }

        private IncludeExcludeFiles CreateIncludeExcludeFiles()
        {
            var files = new IncludeExcludeFiles();
            files.Exclude = new List<string>() { "Exlclude0" };
            files.Include = new List<string>() { "Include0" };
            files.IncludeFiles = new List<string>() { "IncludeFiles0" };
            files.ExcludeFiles = new List<string>() { "ExlcludeFiles0" };
            return files;
        }
        private PackOptions CreatePackOptions()
        {
            var originalPackOptions = new PackOptions();
            originalPackOptions.PackageType = new List<NuGet.Packaging.Core.PackageType>() { new Packaging.Core.PackageType("PackageA", new System.Version("1.0.0")) };
            originalPackOptions.IncludeExcludeFiles = CreateIncludeExcludeFiles();
            return originalPackOptions;
        }

        [Fact]
        public void PackageSpecCloneTest()
        {
            // Set up
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

            PackageSpec.Scripts.Add(Guid.NewGuid().ToString(), new List<string>() { Guid.NewGuid().ToString() }); // Test this changing
            PackageSpec.Scripts.Add(Guid.NewGuid().ToString(), new List<string>() { Guid.NewGuid().ToString() });

            PackageSpec.PackInclude.Add(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

            PackageSpec.PackOptions = CreatePackOptions();

            PackageSpec.RuntimeGraph = new RuntimeModel.RuntimeGraph(); // TODO - Add logic
            PackageSpec.RestoreSettings = CreateProjectRestoreSettings();

            // Act
            var clonedPackageSpec = PackageSpec.Clone();

            // TODO NK - Add more tests
            //Assert
            Assert.Equal(PackageSpec, clonedPackageSpec);
            Assert.False(object.ReferenceEquals(PackageSpec, clonedPackageSpec));
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
            var originalFrameworkName = "net461";
            var originalPRMFI = new ProjectRestoreMetadataFrameworkInfo(nugetFramework);
            originalPRMFI.OriginalFrameworkName = originalFrameworkName;
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
            var originalFrameworkName = "net461";

            var originalPRMFI = new ProjectRestoreMetadataFrameworkInfo(nugetFramework);
            originalPRMFI.OriginalFrameworkName = originalFrameworkName;
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
            var prs =  new ProjectRestoreSettings();
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

        private TargetFrameworkInformation CreateTargetFrameworkInformation()
        {
            var framework = NuGetFramework.Parse("net461");
            var dependency = new LibraryDependency();
            dependency.LibraryRange = new LibraryRange("Dependency", LibraryDependencyTarget.Package);
            dependency.Type = LibraryDependencyType.Default;
            dependency.IncludeType = LibraryIncludeFlags.None;
            dependency.SuppressParent = LibraryIncludeFlags.ContentFiles;
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
            //Assert.True(EqualityUtility.SetEqualsWithNullCheck(noWarn, clone.NoWarn)); TODO get the equality utility here
            Assert.False(object.ReferenceEquals(noWarn, clone.NoWarn));

            // Act again
            noWarn.Clear();

            //Assert again
            Assert.NotEqual(originalWarningProperties, clone);
            Assert.Equal(2, clone.NoWarn.Count);
        }

    }
}
