// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class LockFileLibraryTests
    {
        [Fact]
        public void LockFileLibrary_ComparesEqualPaths()
        {
            // Arrange
            var libraryA = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = "SomeLibrary/1.0.0"
            };

            // same thing
            var libraryB = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = "SomeLibrary/1.0.0"
            };

            // Act & Assert
            Assert.True(libraryA.Equals(libraryB), "The two libraries should be equal.");
        }

        [Fact]
        public void LockFileLibrary_ComparesDifferentCasePaths()
        {
            // Arrange
            var libraryA = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = "SomeLibrary/1.0.0"
            };

            // different case
            var libraryB = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = "somelibrary/1.0.0"
            };

            // Act & Assert
            Assert.False(libraryA.Equals(libraryB), "The two libraries should not be equal.");
        }

        [Fact]
        public void LockFileLibrary_ComparesDifferentPaths()
        {
            // Arrange
            var libraryA = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = "SomeLibrary/1.0.0"
            };

            // different thing
            var libraryB = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = null
            };

            // Act & Assert
            Assert.False(libraryA.Equals(libraryB), "The two libraries should not be equal.");
        }

        [Fact]
        public void LockFileLibrary_ComparesDifferentCaseFiles()
        {
            // Arrange
            var libraryA = new LockFileLibrary
            {
                Files = new List<string>
                {
                    "path/a.txt",
                    "path/b.txt"
                }
            };

            // different case
            var libraryB = new LockFileLibrary
            {
                Files = new List<string>
                {
                    "path/a.txt",
                    "PATH/b.txt"
                }
            };

            // Act & Assert
            Assert.False(libraryA.Equals(libraryB), "The two libraries should not be equal.");
        }

        [Fact]
        public void LockFileLibrary_CloneIncludesAllProperties()
        {
            // Arrange
            var original = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = "SomeLibrary/1.0.0",
                IsServiceable = true,
                MSBuildProject = "MSBuildProject",
                Sha512 = "FAKE-HASH",
                Type = LibraryType.Package,
                Files = new List<string>
                {
                    "file/a.txt",
                    "file/b.txt"
                }
            };
            
            // Use Newtonsoft.Json to enumerate all properties.
            var originalSerialized = JsonConvert.SerializeObject(original, Formatting.Indented);

            // Act
            var clone = original.Clone();

            // Assert
            var cloneSerialized = JsonConvert.SerializeObject(original, Formatting.Indented);
            Assert.Equal(originalSerialized, cloneSerialized);
        }

        [Fact]
        public void LockFileLibrary_CloneIsEqual()
        {
            // Arrange
            var original = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = "SomeLibrary/1.0.0",
                IsServiceable = true,
                MSBuildProject = "MSBuildProject",
                Sha512 = "FAKE-HASH",
                Type = LibraryType.Package,
                Files = new List<string>
                {
                    "file/a.txt",
                    "file/b.txt"
                }
            };

            // Act
            var clone = original.Clone();

            // Assert
            Assert.Equal(original, clone);
        }

        [Fact]
        public void LockFileLibrary_CloneReturnsDifferentInstance()
        {
            // Arrange
            var original = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = "SomeLibrary/1.0.0",
                IsServiceable = true,
                MSBuildProject = "MSBuildProject",
                Sha512 = "FAKE-HASH",
                Type = LibraryType.Package,
                Files = new List<string>
                {
                    "file/a.txt",
                    "file/b.txt"
                }
            };

            // Use Newtonsoft.Json to take a snapshot of all properties.
            var originalSerializedBefore = JsonConvert.SerializeObject(original, Formatting.Indented);

            // Act
            var clone = original.Clone();

            // Assert
            Assert.NotSame(original, clone);

            // Ensure that the clone is deep. Technically the properties that are read-only or value
            // types do not need to be mutated, but this protects against future refactorings.
            clone.Name += "Different";
            clone.Version = new NuGetVersion("2.0.0");
            clone.Path += "Different";
            clone.IsServiceable = !clone.IsServiceable;
            clone.MSBuildProject += "Different";
            clone.Sha512 += "Different";
            clone.Type += "Different";
            clone.Files.Add("Different");

            var originalSerializedAfter = JsonConvert.SerializeObject(original, Formatting.Indented);
            Assert.Equal(originalSerializedBefore, originalSerializedAfter);
        }
    }
}
