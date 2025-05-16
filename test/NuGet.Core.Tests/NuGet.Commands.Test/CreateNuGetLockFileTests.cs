// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility.Commands;
using Xunit;

namespace NuGet.Commands.Test;

public sealed class CreateNuGetLockFileTests
{
    [Fact]
    public void EnsureNugetLockFileDependenciesOrderedAlphabetically()
    {
        // Arrange
        using (var pathContext = new SimpleTestPathContext())
        {
            var lockFile = new LockFile()
            {
                Version = 3
            };

            var target = new LockFileTarget()
            {
                TargetFramework = FrameworkConstants.CommonFrameworks.NetCoreApp30
            };

            // Ordered non-alphabetically
            var testNames = new List<string> { "C.Package", "A.Package", "B.Package" };

            // Create LockFileTargetLibrary
            foreach (var name in testNames)
            {
                var targetLib = new LockFileTargetLibrary()
                {
                    Name = name,
                    Version = NuGetVersion.Parse("1.0.0"),
                    Type = LibraryType.Package
                };
                target.Libraries.Add(targetLib);
            }

            lockFile.Targets.Add(target);

            // Create LockFileLibrary
            foreach (var name in testNames)
            {
                var lib = new LockFileLibrary()
                {
                    Name = name,
                    Version = NuGetVersion.Parse("1.0.0"),
                    Type = LibraryType.Package
                };
                lockFile.Libraries.Add(lib);
            }

            var projectName = "TestProject";
            var projectDirectory = Path.Combine(pathContext.SolutionRoot, projectName);
            lockFile.PackageSpec = PackageReferenceSpecBuilder.Create(projectName, projectDirectory)
                .WithTargetFrameworks(new[]
                        {
                            new TargetFrameworkInformation
                            {
                                FrameworkName = FrameworkConstants.CommonFrameworks.NetCoreApp30,
                                Dependencies = [
                                    new LibraryDependency
                                    {
                                        LibraryRange = new LibraryRange(
                                            "Microsoft.NETCore.App",
                                            new VersionRange(
                                                minVersion: new NuGetVersion("1.0.1"),
                                                originalString: "1.0.1"),
                                            LibraryDependencyTarget.Package)
                                    }
                                ],
                                CentralPackageVersions =  new Dictionary<string, CentralPackageVersion>(StringComparer.OrdinalIgnoreCase)
                                {
                                    { "B.Package", new CentralPackageVersion("B.Package", VersionRange.Parse("1.0.0")) }
                                }
                            }
                        })
                        .WithCentralPackageVersionsEnabled()
                        .WithCentralPackageTransitivePinningEnabled()
                        .Build()
                        .WithTestRestoreMetadata();

            // Act
            var builder = new PackagesLockFileBuilder();
            var nuGetLockFile = builder.CreateNuGetLockFile(lockFile);

            // Assert
            Assert.Equal(["A.Package", "B.Package", "C.Package"], nuGetLockFile.Targets.Single().Dependencies.Select(d => d.Id));
        }
    }
}
