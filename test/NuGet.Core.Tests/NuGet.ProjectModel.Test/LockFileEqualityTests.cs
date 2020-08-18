// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class LockFileEqualityTests
    {
        [Fact]
        public void Equals_WithProjectFileDependencyGroupOutOfOrder_ReturnsTrue()
        {
            var leftSide = new LockFile
            {
                ProjectFileDependencyGroups = new List<ProjectFileDependencyGroup>
                {
                    new ProjectFileDependencyGroup("net462", new string[] { "a", "b", "c" }),
                    new ProjectFileDependencyGroup("net472", new string[] { "a", "c", "b" }),
                }
            };
            var rightSide = new LockFile
            {
                ProjectFileDependencyGroups = new List<ProjectFileDependencyGroup>
                {
                    new ProjectFileDependencyGroup("net472", new string[] { "a", "c", "b" }),
                    new ProjectFileDependencyGroup("net462", new string[] { "a", "b", "c" }),
                }
            };

            leftSide.Should().Be(rightSide);
        }

        [Fact]
        public void Equals_WithDifferentProjectFileDependencyGroup_ReturnsFalse()
        {
            var leftSide = new LockFile
            {
                ProjectFileDependencyGroups = new List<ProjectFileDependencyGroup>
                {
                    new ProjectFileDependencyGroup("net461", new string[] { "a", "b", "c" }),
                    new ProjectFileDependencyGroup("net462", new string[] { "a", "b", "c" }),
                    new ProjectFileDependencyGroup("net463", new string[] { "a", "b", "c" }),
                }
            };
            var rightSide = new LockFile
            {
                ProjectFileDependencyGroups = new List<ProjectFileDependencyGroup>
                {
                    new ProjectFileDependencyGroup("net461", new string[] { "a", "b", "c" }),
                    new ProjectFileDependencyGroup("net462", new string[] { "a", "b", "c" }),
                }
            };

            leftSide.Should().NotBe(rightSide);
        }

        [Fact]
        public void Equals_WithLockFileLibraryOutOfOrder_ReturnsTrue()
        {
            var leftSide = new LockFile
            {
                Libraries = new List<LockFileLibrary>
                {
                    new LockFileLibrary()
                    {
                        Name = "first"
                    },
                    new LockFileLibrary()
                    {
                        Name = "second"
                    },
                }
            };
            var rightSide = new LockFile
            {
                Libraries = new List<LockFileLibrary>
                {
                    new LockFileLibrary()
                    {
                        Name = "second"
                    },
                    new LockFileLibrary()
                    {
                        Name = "first"
                    },
                }
            };

            leftSide.Should().Be(rightSide);
        }

        [Fact]
        public void Equals_WithDifferentLockFileLibrary_ReturnsFalse()
        {
            var leftSide = new LockFile
            {
                Libraries = new List<LockFileLibrary>
                {
                    new LockFileLibrary()
                    {
                        Name = "first"
                    },
                    new LockFileLibrary()
                    {
                        Name = "second"
                    },
                    new LockFileLibrary()
                    {
                        Name = "extra"
                    },
                }
            };
            var rightSide = new LockFile
            {
                Libraries = new List<LockFileLibrary>
                {
                    new LockFileLibrary()
                    {
                        Name = "second"
                    },
                    new LockFileLibrary()
                    {
                        Name = "first"
                    },
                }
            };

            leftSide.Should().NotBe(rightSide);
        }
    }
}
