// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Test.Utility.Commands;
using Xunit;

namespace NuGet.Commands.Test;

public sealed class CreateNuGetLockFileTests
{
    [Fact]
    public void EnsureNugetLockFileDependenciesOrderedAlphabetically()
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
        List<string> testNames = new List<string> { "C.Package", "A.Package", "B.Package" };

        // Create LockFileTargetLibrary
        foreach (var name in testNames)
        {
            var targetLib = new LockFileTargetLibrary()
            {
                Name = name,
                Version = NuGetVersion.Parse("1.0.0"),
                Type = LibraryType.Package
            };
            targetLib.CompileTimeAssemblies.Add(new LockFileItem("lib/netcoreapp3.0/a.dll"));
            targetLib.FrameworkReferences.Add("Microsoft.Windows.Desktop|WindowsForms");
            targetLib.FrameworkReferences.Add("Microsoft.Windows.Desktop|WPF");
            target.Libraries.Add(targetLib);
        }

        lockFile.Targets.Add(target);

        //Create LockFileLibrary
        foreach (var name in testNames)
        {
            var lib = new LockFileLibrary()
            {
                Name = name,
                Version = NuGetVersion.Parse("1.0.0"),
                Type = LibraryType.Package
            };
            lib.Files.Add("lib/netcoreapp3.0/a.dll");
            lib.Files.Add(name + ".nuspec");
            lockFile.Libraries.Add(lib);
        }

        lockFile.PackageSpec = PackageReferenceSpecBuilder.Create("Project", @"X:\Path\Project.csproj")
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


        var builder = new PackagesLockFileBuilder();
        var nuGetLockFile = builder.CreateNuGetLockFile(lockFile);

        // Assert that the dependencies in the lockfile are ordered alphabetically by name/id
        Assert.Equal("A.Package", nuGetLockFile.Targets[0].Dependencies[0].Id);
        Assert.Equal("B.Package", nuGetLockFile.Targets[0].Dependencies[1].Id);
        Assert.Equal("C.Package", nuGetLockFile.Targets[0].Dependencies[2].Id);

    }
}
