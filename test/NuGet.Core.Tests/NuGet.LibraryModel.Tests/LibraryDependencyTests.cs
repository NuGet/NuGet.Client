// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;
using Xunit;

namespace NuGet.LibraryModel.Tests
{
    public class LibraryDependencyTests
    {
        [Fact]
        public void LibraryDependency_Clone_Equals()
        {
            // Arrange
            var target = GetTarget();

            // Act
            var clone = target.Clone();

            // Assert
            Assert.NotSame(target, clone);
            Assert.Equal(target, clone);
        }

        [Fact]
        public void LibraryDependency_Clone_ClonesLibraryRange()
        {
            // Arrange
            var target = GetTarget();

            // Act
            var clone = target.Clone();
            clone.LibraryRange.Name = "SomethingElse";

            // Assert
            Assert.NotSame(target.LibraryRange, clone.LibraryRange);
            Assert.NotEqual(target.LibraryRange.Name, clone.LibraryRange.Name);
        }

        public LibraryDependency GetTarget()
        {
            return new LibraryDependency
            {
                IncludeType = LibraryIncludeFlags.Build | LibraryIncludeFlags.Compile,
                LibraryRange = new LibraryRange
                {
                    Name = "SomeLibrary",
                    TypeConstraint = LibraryDependencyTarget.ExternalProject | LibraryDependencyTarget.WinMD,
                    VersionRange = new VersionRange(new NuGetVersion("4.0.0-rc2"))
                },
                SuppressParent = LibraryIncludeFlags.Analyzers | LibraryIncludeFlags.ContentFiles,
                Type = LibraryDependencyType.Platform
            };
        }
    }
}
