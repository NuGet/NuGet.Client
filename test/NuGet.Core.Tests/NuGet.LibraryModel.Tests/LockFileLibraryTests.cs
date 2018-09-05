// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.ProjectModel;
using Xunit;

namespace NuGet.LibraryModel.Tests
{
    public class LockFileLibraryTests
    {
        public void LockFileLibrary_EqualityEmpty()
        {
            // Arrange
            var library1 = new LockFileLibrary();
            var library2 = new LockFileLibrary();

            // Act & Assert
            Assert.True(library1.Equals(library2));
        }

        public void LockFileLibrary_EqualityDiffersOnMSBuildPath()
        {
            // Arrange
            var library1 = new LockFileLibrary()
            {
                MSBuildProject = "a"
            };

            var library2 = new LockFileLibrary()
            {
                MSBuildProject = "b"
            };

            // Act & Assert
            Assert.False(library1.Equals(library2));
        }

        public void LockFileLibrary_EqualitySameMSBuildPath()
        {
            // Arrange
            var library1 = new LockFileLibrary()
            {
                MSBuildProject = "b"
            };

            var library2 = new LockFileLibrary()
            {
                MSBuildProject = "b"
            };

            // Act & Assert
            Assert.True(library1.Equals(library2));
        }
    }
}
