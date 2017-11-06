// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using Xunit;
namespace NuGet.ProjectModel.Test
{
    public class PackageSpecCloningTests
    {
        [Fact]
        public void BuildOptionsCloneTest()
        {
            //Set up
            var outputName = "OutputName";
            var originalBuildOptions = new BuildOptions();
            originalBuildOptions.OutputName = outputName;

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

        [Fact]
        public void PackageSpecCloneTest()
        {
            // TODO NK
        }

        [Fact]
        public void ProjectRestoreMetadataCloneTest()
        {
            // TODO NK
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
            // TODO NK
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

        [Fact]
        public void ProjectRestoreSettingsCloneTest()
        {
            // Set up
            var originalProjectRestoreSettings = new ProjectRestoreSettings();
            originalProjectRestoreSettings.HideWarningsAndErrors = false;

            // Act
            var clone = originalProjectRestoreSettings.Clone();

            // Assert
            Assert.Equal(originalProjectRestoreSettings, clone);
            Assert.False(object.ReferenceEquals(originalProjectRestoreSettings, clone));
        }

        [Fact]
        public void TargetFrameworkInformationCloneTest()
        {
            // TODO Nk
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
