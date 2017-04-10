// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.XPlat.FuncTest;
using Xunit;


namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class PackCommandTests
    {
        private MsbuildIntegrationTestFixture msbuildFixture;

        public PackCommandTests(MsbuildIntegrationTestFixture fixture)
        {
            this.msbuildFixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackNewDefaultProject_NupkgExists()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetAuthors());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetOwners());
                    Assert.Equal("Package Description", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    Assert.Equal(1, dependencyGroups.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, dependencyGroups[0].TargetFramework);
                    var packages = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1, packages.Count);
                    Assert.Equal("NETStandard.Library", packages[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.6.1")), packages[0].VersionRange);
                    Assert.Equal(new List<string> {"Analyzers", "Build"}, packages[0].Exclude);
                    Assert.Empty(packages[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, libItems[0].TargetFramework);
                    Assert.Equal(new[] {"lib/netstandard1.4/ClassLibrary1.dll"}, libItems[0].Items);
                }

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(true)]
        [InlineData(false)]
        public void PackCommand_PackConsoleAppWithRID_NupkgValid(bool includeSymbols)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ConsoleApp1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " console");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "RuntimeIdentifier", "win7-x64");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var args = includeSymbols ? $"-o {workingDirectory} --include-symbols" : $"-o {workingDirectory}";
                msbuildFixture.PackProject(workingDirectory, projectName, args);

                var nupkgPath = includeSymbols
                    ? Path.Combine(workingDirectory, $"{projectName}.1.0.0.symbols.nupkg")
                    : Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp11, libItems[0].TargetFramework);
                    if (includeSymbols)
                    {
                        Assert.Equal(new[]
                        {
                            "lib/netcoreapp1.1/ConsoleApp1.dll",
                            "lib/netcoreapp1.1/ConsoleApp1.pdb",
                            "lib/netcoreapp1.1/ConsoleApp1.runtimeconfig.json"
                        }, libItems[0].Items);
                    }
                    else
                    {
                        Assert.Equal(
                            new[]
                            {"lib/netcoreapp1.1/ConsoleApp1.dll", "lib/netcoreapp1.1/ConsoleApp1.runtimeconfig.json"},
                            libItems[0].Items);
                    }
                }

            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProject_SupportMultipleFrameworks()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", "netcoreapp1.0;net45");
                    ProjectFileUtils.AddProperty(xml, "RuntimeIdentifier", "win7-x64");

                    var attributes = new Dictionary<string, string>();

                    attributes["Version"] = "1.0.1";
                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "Microsoft.NETCore.App",
                        "netcoreapp1.0",
                        new Dictionary<string, string>(),
                        attributes);

                    attributes["Version"] = "9.0.1";
                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "Newtonsoft.Json",
                        "net45",
                        new Dictionary<string, string>(),
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(2,
                        dependencyGroups.Count);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp10,
                        dependencyGroups[0].TargetFramework);
                    var packagesA = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1,
                        packagesA.Count);
                    Assert.Equal("Microsoft.NETCore.App", packagesA[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.0.1")), packagesA[0].VersionRange);
                    Assert.Equal(new List<string> {"Analyzers", "Build"}, packagesA[0].Exclude);
                    Assert.Empty(packagesA[0].Include);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, dependencyGroups[1].TargetFramework);
                    var packagesB = dependencyGroups[1].Packages.ToList();
                    Assert.Equal(1, packagesB.Count);
                    Assert.Equal("Newtonsoft.Json", packagesB[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("9.0.1")), packagesB[0].VersionRange);
                    Assert.Equal(new List<string> {"Analyzers", "Build"}, packagesB[0].Exclude);
                    Assert.Empty(packagesB[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(2, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp10, libItems[0].TargetFramework);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, libItems[1].TargetFramework);
                    Assert.Equal(
                        new[]
                        {"lib/netcoreapp1.0/ClassLibrary1.dll", "lib/netcoreapp1.0/ClassLibrary1.runtimeconfig.json"},
                        libItems[0].Items);
                    Assert.Equal(new[] {"lib/net45/ClassLibrary1.exe", "lib/net45/ClassLibrary1.runtimeconfig.json"},
                        libItems[1].Items);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(null, null, null, true, "", "Analyzers,Build")]
        [InlineData(null, "Native", null, true, "", "Analyzers,Build,Native")]
        [InlineData("Compile", null, null, true, "", "Analyzers,Build,Native,Runtime")]
        [InlineData("Compile;Runtime", null, null, true, "", "Analyzers,Build,Native")]
        [InlineData("All", null, "None", true, "All", "")]
        [InlineData("All", null, "Compile", true, "Analyzers,Build,ContentFiles,Native,Runtime", "")]
        [InlineData("All", null, "Compile;Build", true, "Analyzers,ContentFiles,Native,Runtime", "")]
        [InlineData("All", "Native", "Compile;Build", true, "Analyzers,ContentFiles,Runtime", "")]
        [InlineData("All", "Native", "Native;Build", true, "Analyzers,Compile,ContentFiles,Runtime", "")]
        [InlineData("Compile", "Native", "Native;Build", true, "", "Analyzers,Build,Native,Runtime")]
        [InlineData("All", "All", null, false, null, null)]
        [InlineData("Compile;Runtime", "All", null, false, null, null)]
        [InlineData(null, null, "All", false, null, null)]
        public void PackCommand_SupportsIncludeExcludePrivateAssets_OnPackages(
            string includeAssets,
            string excludeAssets,
            string privateAssets,
            bool hasPackage,
            string expectedInclude,
            string expectedExclude)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "net45");

                    var attributes = new Dictionary<string, string>();
                    attributes["Version"] = "9.0.1";

                    var properties = new Dictionary<string, string>();
                    if (!string.IsNullOrEmpty(includeAssets))
                    {
                        properties["IncludeAssets"] = includeAssets;
                    }
                    if (!string.IsNullOrEmpty(excludeAssets))
                    {
                        properties["ExcludeAssets"] = excludeAssets;
                    }
                    if (!string.IsNullOrEmpty(privateAssets))
                    {
                        properties["PrivateAssets"] = privateAssets;
                    }

                    ProjectFileUtils.AddItem(
                        xml,
                        "PackageReference",
                        "Newtonsoft.Json",
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    var package = nuspecReader
                        .GetDependencyGroups()
                        .SingleOrDefault()?
                        .Packages
                        .SingleOrDefault();

                    if (!hasPackage)
                    {
                        Assert.Null(package);
                    }
                    else
                    {
                        Assert.NotNull(package);
                        Assert.Equal(expectedInclude, string.Join(",", package.Include));
                        Assert.Equal(expectedExclude, string.Join(",", package.Exclude));
                    }
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProject_AddsProjectRefsAsPackageRefs()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var referencedProject = "ClassLibrary2";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, referencedProject, "classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    ProjectFileUtils.AddItem(
                        xml,
                        "ProjectReference",
                        @"..\ClassLibrary2\ClassLibrary2.csproj",
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    var dependencyGroups = nuspecReader
                        .GetDependencyGroups()
                        .OrderBy(x => x.TargetFramework,
                            new NuGetFrameworkSorter())
                        .ToList();

                    Assert.Equal(1,
                        dependencyGroups.Count);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14,
                        dependencyGroups[0].TargetFramework);
                    var packagesA = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(2,
                        packagesA.Count);
                    Assert.Equal("NETStandard.Library", packagesA[1].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.6.1")), packagesA[1].VersionRange);
                    Assert.Equal(new List<string> {"Analyzers", "Build"}, packagesA[1].Exclude);
                    Assert.Empty(packagesA[1].Include);

                    Assert.Equal("ClassLibrary2", packagesA[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.0.0")), packagesA[0].VersionRange);
                    Assert.Equal(new List<string> {"Analyzers", "Build"}, packagesA[0].Exclude);
                    Assert.Empty(packagesA[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, libItems[0].TargetFramework);
                    Assert.Equal(
                        new[]
                        {"lib/netstandard1.4/ClassLibrary1.dll", "lib/netstandard1.4/ClassLibrary1.runtimeconfig.json"},
                        libItems[0].Items);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProject_PacksFromNuspec()
        {
            var nuspecFileContent = @"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>PackedFromNuspec</id>
    <version>1.2.1</version>
    <authors>Microsoft</authors>
    <owners>NuGet</owners>
    <description>This was packed from nuspec</description>
  </metadata>
  <files>
    <file src=""abc.txt"" target=""CoreCLR/"" />
  </files>
</package>";
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                File.WriteAllText(Path.Combine(workingDirectory, "input.nuspec"), nuspecFileContent);
                File.WriteAllText(Path.Combine(workingDirectory, "abc.txt"), "sample text");

                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} /p:NuspecFile=input.nuspec");

                var nupkgPath = Path.Combine(workingDirectory, $"PackedFromNuspec.1.2.1.nupkg");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the .nuspec.
                    Assert.Equal("PackedFromNuspec", nuspecReader.GetId());
                    Assert.Equal("1.2.1", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("Microsoft", nuspecReader.GetAuthors());
                    Assert.Equal("NuGet", nuspecReader.GetOwners());
                    Assert.Equal("This was packed from nuspec", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    // Validate the assets.
                    var libItems = nupkgReader.GetFiles("CoreCLR").ToArray();
                    Assert.Equal("CoreCLR/abc.txt", libItems[0]);
                }

            }
        }

        [PlatformTheory(Platform.Windows)]
        // Command line : /p:NuspecProperties=\"id=MyPackage;version=1.2.3;description="hello world"\"
        [InlineData("/p:NuspecProperties=\\\"id=MyPackage;version=1.2.3;description=\"hello world\"\\\"", "MyPackage",
            "1.2.3", "hello world")]
        // Command line : /p:NuspecProperties=\"id=MyPackage;version=1.2.3;description="hello = world"\"
        [InlineData("/p:NuspecProperties=\\\"id=MyPackage;version=1.2.3;description=\"hello = world\"\\\"", "MyPackage",
            "1.2.3", "hello = world")]
        // Command line : /p:NuspecProperties=\"id=MyPackage;version=1.2.3;description="hello = world with a %3B"\"
        [InlineData("/p:NuspecProperties=\\\"id=MyPackage;version=1.2.3;description=\"hello = world with a %3B\"\\\"",
            "MyPackage", "1.2.3", "hello = world with a ;")]
        public void PackCommand_PackProject_PacksFromNuspecWithTokenSubstitution(
            string nuspecProperties,
            string expectedId,
            string expectedVersion,
            string expectedDescription
            )
        {
            var nuspecFileContent = @"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>$id$</id>
    <version>$version$</version>
    <authors>Microsoft</authors>
    <owners>NuGet</owners>
    <description>$description$</description>
  </metadata>
  <files>
    <file src=""abc.txt"" target=""CoreCLR/"" />
  </files>
</package>";
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                File.WriteAllText(Path.Combine(workingDirectory, "input.nuspec"), nuspecFileContent);
                File.WriteAllText(Path.Combine(workingDirectory, "abc.txt"), "sample text");

                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} /p:NuspecFile=input.nuspec " + nuspecProperties);

                var nupkgPath = Path.Combine(workingDirectory, $"{expectedId}.{expectedVersion}.nupkg");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the .nuspec.
                    Assert.Equal(expectedId, nuspecReader.GetId());
                    Assert.Equal(expectedVersion, nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("Microsoft", nuspecReader.GetAuthors());
                    Assert.Equal("NuGet", nuspecReader.GetOwners());
                    Assert.Equal(expectedDescription, nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    // Validate the assets.
                    var libItems = nupkgReader.GetFiles("CoreCLR").ToArray();
                    Assert.Equal("CoreCLR/abc.txt", libItems[0]);
                }

            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackProject_PacksFromNuspecWithBasePath()
        {
            var nuspecFileContent = @"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>PackedFromNuspec</id>
    <version>1.2.1</version>
    <authors>Microsoft</authors>
    <owners>NuGet</owners>
    <description>This was packed from nuspec</description>
  </metadata>
  <files>
    <file src=""abc.txt"" target=""CoreCLR/"" />
  </files>
</package>";
            using (var basePathDirectory = TestDirectory.Create())
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                File.WriteAllText(Path.Combine(workingDirectory, "input.nuspec"), nuspecFileContent);
                File.WriteAllText(Path.Combine(basePathDirectory, "abc.txt"), "sample text");

                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} /p:NuspecFile=input.nuspec /p:NuspecBasePath={basePathDirectory.Path}");

                var nupkgPath = Path.Combine(workingDirectory, $"PackedFromNuspec.1.2.1.nupkg");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    // Validate the assets.
                    var libItems = nupkgReader.GetFiles("CoreCLR").ToArray();
                    Assert.Equal("CoreCLR/abc.txt", libItems[0]);
                }

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("folderA/abc.txt", null, "content/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/folderB/abc.txt", null,
            "content/folderA/folderB/abc.txt;contentFiles/any/netstandard1.4/folderA/folderB/abc.txt")]
        [InlineData("../abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("C:/abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt", "folderA/", "folderA/abc.txt")]
        [InlineData("abc.txt", "folderA/xyz.txt", "folderA/xyz.txt")]
        [InlineData("abc.txt", "", "abc.txt")]
        [InlineData("abc.txt", "/", "abc.txt")]
        [InlineData("abc.txt", "folderA;folderB", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles/", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles\\", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("abc.txt", "folderA;contentFiles/folderA", "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("abc.txt", "folderA/xyz.txt", "folderA/xyz.txt")]
        [InlineData("folderA/abc.txt", "folderA/", "folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "", "abc.txt")]
        [InlineData("folderA/abc.txt", "/", "abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;folderB", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles\\", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles/", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA;contentFiles/folderA", "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "folderA/xyz.txt", "folderA/xyz.txt")]
        [InlineData("C:/abc.txt", "folderA/", "folderA/abc.txt")]
        [InlineData("C:/abc.txt", "folderA/xyz.txt", "folderA/xyz.txt")]
        [InlineData("C:/abc.txt", "", "abc.txt")]
        [InlineData("C:/abc.txt", "/", "abc.txt")]
        [InlineData("C:/abc.txt", "folderA;folderB", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("C:/abc.txt", "folderA;contentFiles", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("C:/abc.txt", "folderA;contentFiles\\", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("C:/abc.txt", "folderA;contentFiles/", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("C:/abc.txt", "folderA;contentFiles/folderA", "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("../abc.txt", "folderA/", "folderA/abc.txt")]
        [InlineData("../abc.txt", "folderA/xyz.txt", "folderA/xyz.txt")]
        [InlineData("../abc.txt", "", "abc.txt")]
        [InlineData("../abc.txt", "/", "abc.txt")]
        [InlineData("../abc.txt", "folderA;folderB", "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles/", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles\\", "folderA/abc.txt;contentFiles/abc.txt")]
        [InlineData("../abc.txt", "folderA;contentFiles/folderA", "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        // ## is a special syntax specifically for this test which means that ## should be replaced by the absolute path to the project directory.
        [InlineData("##/abc.txt", null, "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("##/folderA/abc.txt", null,
            "content/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("##/../abc.txt", null, "content/abc.txt")]
        [InlineData("##/abc.txt", "", "abc.txt")]
        [InlineData("##/abc.txt", "folderX;folderY", "folderX/abc.txt;folderY/abc.txt")]
        [InlineData("##/folderA/abc.txt", "folderX;folderY", "folderX/abc.txt;folderY/abc.txt")]
        [InlineData("##/../abc.txt", "folderX;folderY", "folderX/abc.txt;folderY/abc.txt")]

        public void PackCommand_PackProject_PackagePathPacksContentCorrectly(string sourcePath, string packagePath,
            string expectedTargetPaths)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                if (sourcePath.StartsWith("##"))
                {
                    sourcePath = sourcePath.Replace("##", workingDirectory);
                }

                // Create the subdirectory structure for testing possible source paths for the content file
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA"));
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA", "folderB"));
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    if (packagePath != null)
                    {
                        properties["PackagePath"] = packagePath;
                    }
                    ProjectFileUtils.AddItem(
                        xml,
                        "Content",
                        sourcePath,
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                var pathToContent = Path.Combine(workingDirectory, sourcePath);
                if (Path.IsPathRooted(sourcePath))
                {
                    pathToContent = sourcePath;
                }
                File.WriteAllText(pathToContent, "this is sample text in the content file");

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var items = new HashSet<string>(nupkgReader.GetFiles());
                    var expectedPaths = expectedTargetPaths.Split(';');
                    foreach (var path in expectedPaths)
                    {
                        Assert.Contains(path, items);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(null, null, null, "1.0.0")]
        [InlineData("1.2.3", null, null, "1.2.3")]
        [InlineData(null, "rtm-1234", null, "1.0.0-rtm-1234")]
        [InlineData("1.2.3", "rtm-1234", null, "1.2.3-rtm-1234")]
        [InlineData(null, null, "2.3.1", "2.3.1")]
        [InlineData("1.2.3", null, "2.3.1", "2.3.1")]
        [InlineData(null, "rtm-1234", "2.3.1", "2.3.1")]
        [InlineData("1.2.3", "rtm-1234", "2.3.1", "2.3.1")]
        public void PackCommand_PackProject_OutputsCorrectVersion(string versionPrefix, string versionSuffix,
            string packageVersion, string expectedVersion)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                var args = "" +
                           (versionPrefix != null ? $" /p:VersionPrefix={versionPrefix} " : string.Empty) +
                           (versionSuffix != null ? $" /p:VersionSuffix={versionSuffix} " : string.Empty) +
                           (packageVersion != null ? $" /p:PackageVersion={packageVersion} " : string.Empty);
                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory} {args}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.{expectedVersion}.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.{expectedVersion}.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Assert
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var intermediateNuspec = new NuspecReader(nuspecPath);
                    var nuspecReader = nupkgReader.NuspecReader;
                    Assert.Equal(expectedVersion, nuspecReader.GetVersion().ToFullString());
                    Assert.Equal(expectedVersion, intermediateNuspec.GetVersion().ToFullString());
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("abc.txt", null, "any/netstandard1.4/abc.txt")]
        [InlineData("folderA/abc.txt", null, "any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/folderB/abc.txt", null, "any/netstandard1.4/folderA/folderB/abc.txt")]
        [InlineData("../abc.txt", null, "any/netstandard1.4/abc.txt")]
        [InlineData("##/abc.txt", null, "any/netstandard1.4/abc.txt")]
        [InlineData("##/folderA/abc.txt", null, "any/netstandard1.4/folderA/abc.txt")]
        [InlineData("##/../abc.txt", null, "any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt", "contentFiles", "abc.txt")]
        [InlineData("folderA/abc.txt", "contentFiles", "abc.txt")]
        [InlineData("folderA/folderB/abc.txt", "contentFiles", "abc.txt")]
        [InlineData("../abc.txt", "contentFiles", "abc.txt")]
        [InlineData("##/abc.txt", "contentFiles", "abc.txt")]
        [InlineData("##/folderA/abc.txt", "contentFiles", "abc.txt")]
        [InlineData("##/../abc.txt", "contentFiles", "abc.txt")]
        [InlineData("abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("folderA/abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("folderA/folderB/abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("../abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("##/abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("##/folderA/abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("##/../abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("abc.txt", "contentFiles\\", "abc.txt")]
        [InlineData("folderA/abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("folderA/folderB/abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("../abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("##/abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("##/folderA/abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("##/../abc.txt", "contentFiles/", "abc.txt")]
        [InlineData("folderA/abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("folderA/folderB/abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("../abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("##/abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("##/folderA/abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("##/../abc.txt", "contentFiles/xyz.txt", "xyz.txt")]
        [InlineData("abc.txt", "folderA", null)]
        [InlineData("folderA/abc.txt", "folderA", null)]
        [InlineData("folderA/folderB/abc.txt", "folderA", null)]
        [InlineData("../abc.txt", "folderA", null)]
        [InlineData("##/abc.txt", "folderA", null)]
        [InlineData("##/folderA/abc.txt", "folderA", null)]
        [InlineData("##/../abc.txt", "folderA", null)]
        [InlineData("abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("folderA/abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("folderA/folderB/abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("../abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("##/abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("##/folderA/abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        [InlineData("##/../abc.txt", "contentFiles/folderA", "folderA/abc.txt")]
        public void PackCommand_PackProject_OutputsContentFilesInNuspecForSingleFramework(string sourcePath,
            string packagePath, string expectedIncludeString)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                if (sourcePath.StartsWith("##"))
                {
                    sourcePath = sourcePath.Replace("##", workingDirectory);
                }

                // Create the subdirectory structure for testing possible source paths for the content file
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA"));
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA", "folderB"));

                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                var pathToContent = Path.Combine(workingDirectory, sourcePath);
                if (Path.IsPathRooted(sourcePath))
                {
                    pathToContent = sourcePath;
                }
                File.WriteAllText(pathToContent, "this is sample text in the content file");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    if (packagePath != null)
                    {
                        properties["PackagePath"] = packagePath;
                    }
                    ProjectFileUtils.AddItem(
                        xml,
                        "Content",
                        sourcePath,
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    var contentFiles = nuspecReader.GetContentFiles().ToArray();

                    if (expectedIncludeString == null)
                    {
                        Assert.True(contentFiles.Count() == 0);
                    }
                    else
                    {
                        Assert.True(contentFiles.Count() == 1);
                        var contentFile = contentFiles[0];
                        Assert.Equal(expectedIncludeString, contentFile.Include);
                        Assert.Equal("Content", contentFile.BuildAction);

                        var files = nupkgReader.GetFiles("contentFiles");
                        Assert.Contains("contentFiles/" + expectedIncludeString, files);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("abc.txt", "any/net45/abc.txt;any/netstandard1.3/abc.txt")]
        [InlineData("folderA/abc.txt", "any/net45/folderA/abc.txt;any/netstandard1.3/folderA/abc.txt")]
        [InlineData("folderA/folderB/abc.txt",
            "any/net45/folderA/folderB/abc.txt;any/netstandard1.3/folderA/folderB/abc.txt")]
        [InlineData("../abc.txt", "any/net45/abc.txt;any/netstandard1.3/abc.txt")]
        [InlineData("##/abc.txt", "any/net45/abc.txt;any/netstandard1.3/abc.txt")]
        [InlineData("##/folderA/abc.txt", "any/net45/folderA/abc.txt;any/netstandard1.3/folderA/abc.txt")]
        [InlineData("##/../abc.txt", "any/net45/abc.txt;any/netstandard1.3/abc.txt")]
        public void PackCommand_PackProject_OutputsContentFilesInNuspecForMultipleFrameworks(string sourcePath,
            string expectedIncludeString)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                if (sourcePath.StartsWith("##"))
                {
                    sourcePath = sourcePath.Replace("##", workingDirectory);
                }

                // Create the subdirectory structure for testing possible source paths for the content file
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA"));
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA", "folderB"));

                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                var pathToContent = Path.Combine(workingDirectory, sourcePath);
                if (Path.IsPathRooted(sourcePath))
                {
                    pathToContent = sourcePath;
                }
                File.WriteAllText(pathToContent, "this is sample text in the content file");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", "net45;netstandard1.3");

                    var attributes = new Dictionary<string, string>();
                    var properties = new Dictionary<string, string>();

                    ProjectFileUtils.AddItem(
                        xml,
                        "Content",
                        sourcePath,
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    var contentFiles = nuspecReader.GetContentFiles().ToArray();

                    if (expectedIncludeString == null)
                    {
                        Assert.True(contentFiles.Count() == 0);
                    }
                    else
                    {
                        var expectedStrings = expectedIncludeString.Split(';');
                        Assert.True(contentFiles.Count() == 2);
                        var contentFileSet = contentFiles.Select(p => p.Include);
                        var files = nupkgReader.GetFiles("contentFiles");
                        foreach (var expected in expectedStrings)
                        {
                            Assert.Contains(expected, contentFileSet);
                            Assert.Contains("contentFiles/" + expected, files);
                        }
                    }
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_SingleFramework_GeneratesPackageOnBuild()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");
                    ProjectFileUtils.AddProperty(xml, "GeneratePackageOnBuild", "true");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.BuildProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetAuthors());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetOwners());
                    Assert.Equal("Package Description", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    Assert.Equal(1, dependencyGroups.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, dependencyGroups[0].TargetFramework);
                    var packages = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1, packages.Count);
                    Assert.Equal("NETStandard.Library", packages[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.6.1")), packages[0].VersionRange);
                    Assert.Equal(new List<string> {"Analyzers", "Build"}, packages[0].Exclude);
                    Assert.Empty(packages[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, libItems[0].TargetFramework);
                    Assert.Equal(new[] {"lib/netstandard1.4/ClassLibrary1.dll"}, libItems[0].Items);
                }

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("netstandard1.4")]
        [InlineData("netstandard1.4;net451")]
        [InlineData("netstandard1.4;net451;netcoreapp1.0")]
        public void PackCommand_MultipleFrameworks_GeneratesPackageOnBuild(string frameworks)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFrameworks", frameworks);
                    ProjectFileUtils.AddProperty(xml, "GeneratePackageOnBuild", "true");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.BuildProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");


                var frameworksArray = frameworks.Split(';');
                var count = frameworksArray.Length;

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetAuthors());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetOwners());
                    Assert.Equal("Package Description", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    Assert.Equal(count, dependencyGroups.Count);

                    // Validate the assets.
                    var libItems = nupkgReader.GetFiles("lib").ToList();
                    Assert.Equal(count, libItems.Count());

                    foreach (var framework in frameworksArray)
                    {
                        Assert.Contains($"lib/{framework}/ClassLibrary1.dll", libItems);
                    }
                }
            }
        }

        // This test checks to see that when IncludeBuildOutput=false, the generated nupkg does not contain lib folder
        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackNewDefaultProject_IncludeBuildOutputDoesNotCreateLibFolder()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} /p:IncludeBuildOutput=false");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetAuthors());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetOwners());
                    Assert.Equal("Package Description", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    Assert.Equal(1, dependencyGroups.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, dependencyGroups[0].TargetFramework);
                    var packages = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1, packages.Count);
                    Assert.Equal("NETStandard.Library", packages[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.6.1")), packages[0].VersionRange);
                    Assert.Equal(new List<string> {"Analyzers", "Build"}, packages[0].Exclude);
                    Assert.Empty(packages[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(0, libItems.Count);
                }
            }
        }

        // This test checks to see that when BuildOutputTargetFolder is specified, the generated nupkg has the DLLs in the specified output folder
        // instead of the default lib folder.
        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackNewDefaultProject_BuildOutputTargetFolderOutputsLibsToRightFolder()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var buildOutputTargetFolder = "build";
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"-o {workingDirectory} /p:BuildOutputTargetFolder={buildOutputTargetFolder}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(0, libItems.Count);
                    libItems = nupkgReader.GetLibItems(buildOutputTargetFolder).ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, libItems[0].TargetFramework);
                    Assert.Equal(new[] {$"{buildOutputTargetFolder}/netstandard1.4/ClassLibrary1.dll"},
                        libItems[0].Items);
                }
            }
        }

        // This test checks to see that when GeneratePackageOnBuild is set to true, the generated nupkg and the intermediate
        // nuspec is deleted when the clean target is invoked.
        [PlatformFact(Platform.Windows)]
        public void PackCommand_PackNewProject_CleanDeletesNupkgAndNuspec()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.AddProperty(xml, "GeneratePackageOnBuild", "true");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.BuildProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory} ");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                // Run the clean target
                msbuildFixture.BuildProject(workingDirectory, projectName,
                    $"/t:Clean /p:PackageOutputPath={workingDirectory}\\");

                Assert.True(!File.Exists(nupkgPath), "The output .nupkg was not deleted by the Clean target");
                Assert.True(!File.Exists(nuspecPath), "The intermediate nuspec file was not deleted by the Clean target");
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("abc.txt",                  null,                               "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("folderA/abc.txt",          null,                               "content/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/folderB/abc.txt",  null,                               "content/folderA/folderB/abc.txt;contentFiles/any/netstandard1.4/folderA/folderB/abc.txt")]
        [InlineData("../abc.txt",               null,                               "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("C:/abc.txt",               null,                               "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt",                  "folderA/",                         "folderA/abc.txt")]
        [InlineData("abc.txt",                  "folderA/xyz.txt",                  "folderA/xyz.txt/abc.txt")]
        [InlineData("abc.txt",                  "folderA;folderB",                  "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("abc.txt",                  "folderA;contentFiles",             "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt",                  "folderA;contentFiles/",            "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt",                  "folderA;contentFiles\\",           "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt",                  "folderA;contentFiles/folderA",     "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("abc.txt",                  "folderA/xyz.txt",                  "folderA/xyz.txt/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA/",                         "folderA/folderA/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA;folderB",                  "folderA/folderA/abc.txt;folderB/folderA/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA;contentFiles",             "folderA/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA;contentFiles\\",           "folderA/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA;contentFiles/",            "folderA/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA;contentFiles/folderA",     "folderA/folderA/abc.txt;contentFiles/folderA/folderA/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA/xyz.txt",                  "folderA/xyz.txt/folderA/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA/",                         "folderA/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA/xyz.txt",                  "folderA/xyz.txt/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA;folderB",                  "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA;contentFiles",             "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA;contentFiles\\",           "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA;contentFiles/",            "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA;contentFiles/folderA",     "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("../abc.txt",               "folderA/",                         "folderA/abc.txt")]
        [InlineData("../abc.txt",               "folderA/xyz.txt",                  "folderA/xyz.txt/abc.txt")]
        [InlineData("../abc.txt",               "folderA;folderB",                  "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("../abc.txt",               "folderA;contentFiles",             "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("../abc.txt",               "folderA;contentFiles/",            "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("../abc.txt",               "folderA;contentFiles\\",           "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("../abc.txt",               "folderA;contentFiles/folderA",     "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        // ## is a special syntax specifically for this test which means that ## should be replaced by the absolute path to the project directory.
        [InlineData("##/abc.txt",               null,                               "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("##/folderA/abc.txt",       null,                               "content/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("##/../abc.txt",            null,                               "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("##/abc.txt",               "folderX;folderY",                  "folderX/abc.txt;folderY/abc.txt")]
        [InlineData("##/folderA/abc.txt",       "folderX;folderY",                  "folderX/folderA/abc.txt;folderY/folderA/abc.txt")]
        [InlineData("##/../abc.txt",            "folderX;folderY",                  "folderX/abc.txt;folderY/abc.txt")]

        public void PackCommand_PackProject_ContentTargetFoldersPacksContentCorrectly(string sourcePath,
            string contentTargetFolders, string expectedTargetPaths)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {

                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                if (sourcePath.StartsWith("##"))
                {
                    sourcePath = sourcePath.Replace("##", workingDirectory);
                }

                // Create the subdirectory structure for testing possible source paths for the content file
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA"));
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA", "folderB"));
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");
                    ProjectFileUtils.AddProperty(xml, "ContentTargetFolders", contentTargetFolders);

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    ProjectFileUtils.AddItem(
                        xml,
                        "Content",
                        sourcePath,
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                var pathToContent = Path.Combine(workingDirectory, sourcePath);
                if (Path.IsPathRooted(sourcePath))
                {
                    pathToContent = sourcePath;
                }
                File.WriteAllText(pathToContent, "this is sample text in the content file");

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var items = new HashSet<string>(nupkgReader.GetFiles());
                    var expectedPaths = expectedTargetPaths.Split(';');
                    foreach (var path in expectedPaths)
                    {
                        Assert.Contains(path, items);
                    }
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public void PackCommand_NewProject_AddsTitleToNuspec()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");
                    ProjectFileUtils.AddProperty(xml, "Title", "MyPackageTitle");

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    var intermediateNuspec = new NuspecReader(nuspecPath);

                    // Validate the output .nuspec.
                    Assert.Equal("ClassLibrary1", nuspecReader.GetId());
                    Assert.Equal("1.0.0", nuspecReader.GetVersion().ToFullString());
                    Assert.Equal("MyPackageTitle", nuspecReader.GetTitle());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetAuthors());
                    Assert.Equal("ClassLibrary1", nuspecReader.GetOwners());
                    Assert.Equal("Package Description", nuspecReader.GetDescription());
                    Assert.False(nuspecReader.GetRequireLicenseAcceptance());

                    var dependencyGroups = nuspecReader.GetDependencyGroups().ToList();
                    Assert.Equal(1, dependencyGroups.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, dependencyGroups[0].TargetFramework);
                    var packages = dependencyGroups[0].Packages.ToList();
                    Assert.Equal(1, packages.Count);
                    Assert.Equal("NETStandard.Library", packages[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.6.1")), packages[0].VersionRange);
                    Assert.Equal(new List<string> {"Analyzers", "Build"}, packages[0].Exclude);
                    Assert.Empty(packages[0].Include);

                    // Validate title property in intermediate nuspec
                    Assert.Equal("MyPackageTitle", intermediateNuspec.GetTitle());
                }

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("TargetFramework", "netstandard1.4")]
        [InlineData("TargetFrameworks", "netstandard1.4;net46")]
        public void PackCommand_IncludeSource_AddsSourceFiles(string tfmProperty, string tfmValue)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var utilitySrcFileContent = @"using System;
namespace ClassLibrary
{
    public class UtilityMethods
    {
    }
}";
                var extensionSrcFileContent = @"using System;
namespace ClassLibrary
{
    public class ExtensionMethods
    {
    }
}";
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                Directory.CreateDirectory(Path.Combine(workingDirectory, "Utils"));
                Directory.CreateDirectory(Path.Combine(workingDirectory, "Extensions"));
                File.WriteAllText(Path.Combine(workingDirectory, "Utils", "Utility.cs"), utilitySrcFileContent);
                File.WriteAllText(Path.Combine(workingDirectory, "Extensions", "ExtensionMethods.cs"),
                    extensionSrcFileContent);

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, tfmProperty, tfmValue);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName,
                    $"--include-source /p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var symbolsNupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.symbols.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(symbolsNupkgPath), "The output symbols nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(symbolsNupkgPath))
                {
                    var nuspecReader = nupkgReader.NuspecReader;
                    // Validate the assets.
                    var srcItems = nupkgReader.GetFiles("src").ToArray();
                    Assert.True(srcItems.Length == 4);
                    Assert.Contains("src/ClassLibrary1/ClassLibrary1.csproj", srcItems);
                    Assert.Contains("src/ClassLibrary1/Class1.cs", srcItems);
                    Assert.Contains("src/ClassLibrary1/Extensions/ExtensionMethods.cs", srcItems);
                    Assert.Contains("src/ClassLibrary1/Utils/Utility.cs", srcItems);
                }

            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("TargetFramework", "netstandard1.4")]
        [InlineData("TargetFrameworks", "netstandard1.4;net46")]
        public void PackCommand_ContentInnerTargetExtension_AddsTfmSpecificContent(string tfmProperty, string tfmValue)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                Directory.CreateDirectory(Path.Combine(workingDirectory, "Extensions"));
                File.WriteAllText(Path.Combine(workingDirectory, "abc.txt"), "hello world");
                File.WriteAllText(Path.Combine(workingDirectory, "Extensions", "ext.txt"), "hello world again");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    var target = @"<Target Name=""CustomContentTarget"">
    <ItemGroup>
      <TfmSpecificPackageFile Include=""abc.txt"">
        <PackagePath>mycontent/$(TargetFramework)</PackagePath>
      </TfmSpecificPackageFile>
      <TfmSpecificPackageFile Include=""Extensions/ext.txt"" Condition=""'$(TargetFramework)' == 'net46'"">
        <PackagePath>net46content</PackagePath>
      </TfmSpecificPackageFile>  
    </ItemGroup>
  </Target>";
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, tfmProperty, tfmValue);
                    ProjectFileUtils.AddProperty(xml, "TargetsForTfmSpecificContentInPackage", "CustomContentTarget");
                    ProjectFileUtils.AddCustomXmlToProjectRoot(xml, target);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var files = nupkgReader.GetFiles("mycontent");
                    var tfms = tfmValue.Split(';');
                    Assert.Equal(tfms.Length, files.Count());

                    foreach (var tfm in tfms)
                    {
                        Assert.Contains($"mycontent/{tfm}/abc.txt", files);
                        var net46files = nupkgReader.GetFiles("net46content");
                        if (tfms.Length > 1)
                        {
                            Assert.Equal(1, net46files.Count());
                            Assert.Contains("net46content/ext.txt", net46files);
                        }
                        else
                        {
                            Assert.Equal(0, net46files.Count());
                        }
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("TargetFramework", "netstandard1.4")]
        [InlineData("TargetFrameworks", "netstandard1.4;net46")]
        public void PackCommand_BuildOutputInnerTargetExtension_AddsTfmSpecificBuildOuput(string tfmProperty,
    string tfmValue)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                Directory.CreateDirectory(workingDirectory);
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");
                File.WriteAllText(Path.Combine(workingDirectory, "abc.dll"), "hello world");
                var pathToDll = Path.Combine(workingDirectory, "abc.dll");
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    var target =
                        $@"<Target Name=""CustomBuildOutputTarget"">
    <ItemGroup>
      <BuildOutputInPackage Include=""abc.dll"">
        <FinalOutputPath>{pathToDll}</FinalOutputPath>
      </BuildOutputInPackage>
    </ItemGroup>
  </Target>";
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, tfmProperty, tfmValue);
                    ProjectFileUtils.AddProperty(xml, "TargetsForTfmSpecificBuildOutput", "CustomBuildOutputTarget");
                    ProjectFileUtils.AddCustomXmlToProjectRoot(xml, target);
                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                msbuildFixture.PackProject(workingDirectory, projectName, $"/p:PackageOutputPath={workingDirectory}");

                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");

                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var tfms = tfmValue.Split(';');
                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(tfms.Length, libItems.Count);

                    if (tfms.Length == 2)
                    {
                        Assert.Equal(FrameworkConstants.CommonFrameworks.Net46, libItems[0].TargetFramework);
                        Assert.Equal(new[] {"lib/net46/abc.dll", "lib/net46/ClassLibrary1.dll"},
                            libItems[0].Items);
                        Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, libItems[1].TargetFramework);
                        Assert.Equal(new[] { "lib/netstandard1.4/abc.dll", "lib/netstandard1.4/ClassLibrary1.dll" },
                            libItems[1].Items);
                    }
                    else
                    {
                        Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, libItems[0].TargetFramework);
                        Assert.Equal(new[] { "lib/netstandard1.4/abc.dll", "lib/netstandard1.4/ClassLibrary1.dll" },
                            libItems[0].Items);
                    }
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData("folderA\\**\\*",                           null,                           "content/folderA/folderA.txt;content/folderA/folderB/folderB.txt;" +
                                                                                                "contentFiles/any/netstandard1.4/folderA/folderA.txt;" +
                                                                                                "contentFiles/any/netstandard1.4/folderA/folderB/folderB.txt")]
        [InlineData("folderA\\**\\*",                           "pkgA",                         "pkgA/folderA.txt;pkgA/folderB/folderB.txt")]
        [InlineData("folderA\\**\\*",                           "pkgA/",                        "pkgA/folderA.txt;pkgA/folderB/folderB.txt")]
        [InlineData("folderA\\**\\*",                           "pkgA\\",                       "pkgA/folderA.txt;pkgA/folderB/folderB.txt")]
        [InlineData("folderA\\**",                              "pkgA",                         "pkgA/folderA.txt;pkgA/folderB/folderB.txt")]
        public void PackCommand_PackProject_GlobbingPathsPacksContentCorrectly(string sourcePath, string packagePath,
            string expectedTargetPaths)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                //XPlatTestUtils.WaitForDebugger();
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                
                // Create the subdirectory structure for testing possible source paths for the content file
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA", "folderB"));
                var projectFile = Path.Combine(workingDirectory, $"{projectName}.csproj");

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, " classlib");

                using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.ReadWrite))
                {
                    var xml = XDocument.Load(stream);
                    ProjectFileUtils.SetTargetFrameworkForProject(xml, "TargetFramework", "netstandard1.4");

                    var attributes = new Dictionary<string, string>();

                    var properties = new Dictionary<string, string>();
                    if (packagePath != null)
                    {
                        properties["PackagePath"] = packagePath;
                    }
                    ProjectFileUtils.AddItem(
                        xml,
                        "Content",
                        sourcePath,
                        string.Empty,
                        properties,
                        attributes);

                    ProjectFileUtils.WriteXmlToFile(xml, stream);
                }

                File.WriteAllText(Path.Combine(workingDirectory, "folderA", "folderA.txt"), "hello world from subfolder A directory");
                File.WriteAllText(Path.Combine(workingDirectory, "folderA", "folderB", "folderB.txt"), "hello world from subfolder B directory");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                var nupkgPath = Path.Combine(workingDirectory, $"{projectName}.1.0.0.nupkg");
                var nuspecPath = Path.Combine(workingDirectory, "obj", $"{projectName}.1.0.0.nuspec");

                // Act
                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory}");

                // Assert
                Assert.True(File.Exists(nupkgPath), "The output .nupkg is not in the expected place");
                Assert.True(File.Exists(nuspecPath), "The intermediate nuspec file is not in the expected place");
                using (var nupkgReader = new PackageArchiveReader(nupkgPath))
                {
                    var items = new HashSet<string>(nupkgReader.GetFiles());
                    var expectedPaths = expectedTargetPaths.Split(';');
                    // we add 5 because of the 5 standard files present in the nupkg that won't change.
                    Assert.Equal(items.Count(), expectedPaths.Length + 5);
                    foreach (var path in expectedPaths)
                    {
                        Assert.Contains(path, items);
                    }
                }
            }
        }
    }
}
