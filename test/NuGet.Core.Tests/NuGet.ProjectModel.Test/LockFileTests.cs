// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class LockFileTests
    {
        [Fact]
        public void LockFile_ConsidersEquivalentPackageSpec()
        {
            // Arrange
            Func<LockFile> getLockFile = () =>
            {
                var lockFile = new LockFile();
                lockFile.Version = 2;

                lockFile.PackageSpec = new PackageSpec(new[]
                {
                    new TargetFrameworkInformation
                    {
                        FrameworkName = FrameworkConstants.CommonFrameworks.NetCoreApp10,
                        Dependencies = new[]
                        {
                            new LibraryDependency
                            {
                                LibraryRange = new LibraryRange(
                                    "Microsoft.NETCore.App",
                                    new VersionRange(
                                        minVersion: new NuGetVersion("1.0.1"),
                                        originalString: "1.0.1"),
                                    LibraryDependencyTarget.Package)
                            },
                            new LibraryDependency
                            {
                                LibraryRange = new LibraryRange(
                                    "Microsoft.NET.Sdk",
                                    new VersionRange(
                                        minVersion: new NuGetVersion("1.0.0-alpha-20161104-2"),
                                        originalString: "1.0.0-alpha-20161104-2"),
                                    LibraryDependencyTarget.Package),
                                SuppressParent = LibraryIncludeFlags.All
                            }
                        }
                    }
                })
                {
                    Version = new NuGetVersion("1.0.0"),
                    RestoreMetadata = new ProjectRestoreMetadata
                    {
                        ProjectUniqueName = @"X:\ProjectPath\ProjectPath.csproj",
                        ProjectName = "ProjectPath",
                        ProjectPath = @"X:\ProjectPath\ProjectPath.csproj",
                        OutputPath = @"X:\ProjectPath\obj\",
                        ProjectStyle = ProjectStyle.PackageReference,
                        OriginalTargetFrameworks = new[] { "netcoreapp1.0" },
                        TargetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>
                    {
                        new ProjectRestoreMetadataFrameworkInfo(NuGetFramework.Parse("netcoreapp1.0"))
                    }
                    }
                };

                return lockFile;
            };

            var self = getLockFile();
            var other = getLockFile();

            // Act & Assert
            Assert.NotSame(self, other);
            Assert.Equal(self, other);
        }
    }
}
