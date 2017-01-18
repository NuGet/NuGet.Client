using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        MsbuilldIntegrationTestFixture msbuildFixture;

        public PackCommandTests(MsbuilldIntegrationTestFixture fixture)
        {
            this.msbuildFixture = fixture;
        }

        [Platform(Platform.Windows)]
        [Fact]
        public void PackCommand_PackNewDefaultProject_NupkgExists()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, "-t lib");
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
                    Assert.Equal(new VersionRange(new NuGetVersion("1.6.0")), packages[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packages[0].Exclude);
                    Assert.Empty(packages[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/netstandard1.4/ClassLibrary1.dll" }, libItems[0].Items);
                }

            }
        }

        [Platform(Platform.Windows)]
        [Fact]
        public void PackCommand_PackProject_SupportMultipleFrameworks()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectFileContents =
                    @"<Project Sdk=""Microsoft.NET.Sdk"" ToolsVersion=""15.0"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFrameworks>netcoreapp1.0;net45</TargetFrameworks>
    <RuntimeIdentifier>win7-x64</RuntimeIdentifier>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include=""**\*.cs"" />
    <EmbeddedResource Include=""**\*.resx"" />
  </ItemGroup>

  <ItemGroup Condition=""'$(TargetFramework)' == 'netcoreapp1.0'"">
    <PackageReference Include=""Microsoft.NETCore.App"" Version=""1.0.1"" />
  </ItemGroup>
  <ItemGroup Condition=""'$(TargetFramework)' == 'net45'"">
    <PackageReference Include=""Newtonsoft.Json"" Version=""9.0.1"" />
  </ItemGroup>

</Project>";
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);
                File.WriteAllText(Path.Combine(workingDirectory, $"{projectName}.csproj"), projectFileContents);

                // This is a hack to make restore work with <TargetFrameworks> . Once we update the CLI_TEST in our repo, we can remove this.
                msbuildFixture.RestoreProject(workingDirectory, projectName, "/p:RestoreProjectStyle=PackageReference");

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
                    Assert.Equal(new List<string> { "Analyzers","Build"}, packagesA[0].Exclude);
                    Assert.Empty(packagesA[0].Include);

                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, dependencyGroups[1].TargetFramework);
                    var packagesB = dependencyGroups[1].Packages.ToList();
                    Assert.Equal(1, packagesB.Count);
                    Assert.Equal("Newtonsoft.Json", packagesB[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("9.0.1")), packagesB[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesB[0].Exclude);
                    Assert.Empty(packagesB[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(2, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp10, libItems[0].TargetFramework);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.Net45, libItems[1].TargetFramework);
                    Assert.Equal(new[] { "lib/netcoreapp1.0/ClassLibrary1.dll" }, libItems[0].Items);
                    Assert.Equal(new[] { "lib/net45/ClassLibrary1.exe" }, libItems[1].Items);
                }
            }
        }

        [Platform(Platform.Windows)]
        [Theory]
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
                var projectFileContents1 =
                    @"<Project Sdk=""Microsoft.NET.Sdk"" ToolsVersion=""15.0"">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net45</TargetFramework>
    <RuntimeIdentifier>win7-x64</RuntimeIdentifier>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include=""**\*.cs"" />
    <EmbeddedResource Include=""**\*.resx"" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""9.0.1"">";

                var projectFileContents2 = (!string.IsNullOrEmpty(includeAssets) ? $"<IncludeAssets>{includeAssets}</IncludeAssets>\n" : string.Empty)
                    + (!string.IsNullOrEmpty(excludeAssets) ? $"<ExcludeAssets>{excludeAssets}</ExcludeAssets>\n" : string.Empty) 
                    + (!string.IsNullOrEmpty(privateAssets) ? $"<PrivateAssets>{privateAssets}</PrivateAssets>\n" : string.Empty);
            var projectFileContents3 = @"
    </PackageReference>
  </ItemGroup>

</Project>";
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);
                File.WriteAllLines(Path.Combine(workingDirectory, $"{projectName}.csproj"), new string[] {projectFileContents1, projectFileContents2, projectFileContents3});
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

        [Platform(Platform.Windows)]
        [Fact]
        public void PackCommand_PackProject_AddsProjectRefsAsPackageRefs()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectFileContents =
                    @"<Project Sdk=""Microsoft.NET.Sdk"" ToolsVersion=""15.0"">

  <PropertyGroup>
    <TargetFrameworks>netstandard1.4</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include=""**\*.cs"" />
    <EmbeddedResource Include=""**\*.resx"" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include=""NETStandard.Library"" Version=""1.6"" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include=""..\ClassLibrary2\ClassLibrary2.csproj"" />
  </ItemGroup>

</Project>";
                var projectName = "ClassLibrary1";
                var referencedProject = "ClassLibrary2";
                var workingDirectory = Path.Combine(testDirectory, projectName);

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, referencedProject, "-t lib");
                File.WriteAllText(Path.Combine(workingDirectory, $"{projectName}.csproj"), projectFileContents);
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
                    Assert.Equal(new VersionRange(new NuGetVersion("1.6")), packagesA[1].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesA[1].Exclude);
                    Assert.Empty(packagesA[1].Include);
                    
                    Assert.Equal("ClassLibrary2", packagesA[0].Id);
                    Assert.Equal(new VersionRange(new NuGetVersion("1.0.0")), packagesA[0].VersionRange);
                    Assert.Equal(new List<string> { "Analyzers", "Build" }, packagesA[0].Exclude);
                    Assert.Empty(packagesA[0].Include);

                    // Validate the assets.
                    var libItems = nupkgReader.GetLibItems().ToList();
                    Assert.Equal(1, libItems.Count);
                    Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, libItems[0].TargetFramework);
                    Assert.Equal(new[] { "lib/netstandard1.4/ClassLibrary1.dll" }, libItems[0].Items);
                }
            }
        }

        [Platform(Platform.Windows)]
        [Fact]
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
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, "-t lib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                File.WriteAllText(Path.Combine(workingDirectory, "input.nuspec"), nuspecFileContent);
                File.WriteAllText(Path.Combine(workingDirectory, "abc.txt"), "sample text");

                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory} /p:NuspecFile=input.nuspec");

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

        [Platform(Platform.Windows)]
        [Theory]
        // Command line : /p:NuspecProperties=\"id=MyPackage;version=1.2.3;description="hello world"\"
        [InlineData("/p:NuspecProperties=\\\"id=MyPackage;version=1.2.3;description=\"hello world\"\\\"", "MyPackage", "1.2.3", "hello world")]
        // Command line : /p:NuspecProperties=\"id=MyPackage;version=1.2.3;description="hello = world"\"
        [InlineData("/p:NuspecProperties=\\\"id=MyPackage;version=1.2.3;description=\"hello = world\"\\\"", "MyPackage", "1.2.3", "hello = world")]
        // Command line : /p:NuspecProperties=\"id=MyPackage;version=1.2.3;description="hello = world with a %3B"\"
        [InlineData("/p:NuspecProperties=\\\"id=MyPackage;version=1.2.3;description=\"hello = world with a %3B\"\\\"", "MyPackage", "1.2.3", "hello = world with a ;")]
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
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, "-t lib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                File.WriteAllText(Path.Combine(workingDirectory, "input.nuspec"), nuspecFileContent);
                File.WriteAllText(Path.Combine(workingDirectory, "abc.txt"), "sample text");

                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory} /p:NuspecFile=input.nuspec " + nuspecProperties);

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

        [Platform(Platform.Windows)]
        [Fact]
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
            using(var basePathDirectory = TestDirectory.Create())
            using (var testDirectory = TestDirectory.Create())
            {
                var projectName = "ClassLibrary1";
                var workingDirectory = Path.Combine(testDirectory, projectName);
                // Act
                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName, "-t lib");
                msbuildFixture.RestoreProject(workingDirectory, projectName, string.Empty);
                File.WriteAllText(Path.Combine(workingDirectory, "input.nuspec"), nuspecFileContent);
                File.WriteAllText(Path.Combine(basePathDirectory, "abc.txt"), "sample text");

                msbuildFixture.PackProject(workingDirectory, projectName, $"-o {workingDirectory} /p:NuspecFile=input.nuspec /p:NuspecBasePath={basePathDirectory.Path}");

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

        [Platform(Platform.Windows)]
        [Theory]
        [InlineData("abc.txt",                  null,                                       "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("folderA/abc.txt",          null,                                       "content/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("folderA/folderB/abc.txt",  null,                                       "content/folderA/folderB/abc.txt;contentFiles/any/netstandard1.4/folderA/folderB/abc.txt")]
        [InlineData("../abc.txt",               null,                                       "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("C:/abc.txt",               null,                                       "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt",                  "folderA/",                                 "folderA/abc.txt")]
        [InlineData("abc.txt",                  "folderA/xyz.txt",                          "folderA/xyz.txt")]
        [InlineData("abc.txt",                  "",                                         "abc.txt")]
        [InlineData("abc.txt",                  "/",                                        "abc.txt")]
        [InlineData("abc.txt",                  "folderA;folderB",                          "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("abc.txt",                  "folderA;contentFiles",                     "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt",                  "folderA;contentFiles/",                    "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt",                  "folderA;contentFiles\\",                   "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("abc.txt",                  "folderA;contentFiles/folderA",             "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("abc.txt",                  "folderA/xyz.txt",                          "folderA/xyz.txt")]
        [InlineData("folderA/abc.txt",          "folderA/",                                 "folderA/abc.txt")]
        [InlineData("folderA/abc.txt",          "",                                         "abc.txt")]
        [InlineData("folderA/abc.txt",          "/",                                        "abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA;folderB",                          "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA;contentFiles",                     "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA;contentFiles\\",                     "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA;contentFiles/",                     "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA;contentFiles/folderA",             "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("folderA/abc.txt",          "folderA/xyz.txt",                          "folderA/xyz.txt")]
        [InlineData("C:/abc.txt",               "folderA/",                                 "folderA/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA/xyz.txt",                          "folderA/xyz.txt")]
        [InlineData("C:/abc.txt",               "",                                         "abc.txt")]
        [InlineData("C:/abc.txt",               "/",                                        "abc.txt")]
        [InlineData("C:/abc.txt",               "folderA;folderB",                          "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA;contentFiles",                     "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA;contentFiles\\",                     "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA;contentFiles/",                     "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("C:/abc.txt",               "folderA;contentFiles/folderA",             "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        [InlineData("../abc.txt",               "folderA/",                                 "folderA/abc.txt")]
        [InlineData("../abc.txt",               "folderA/xyz.txt",                          "folderA/xyz.txt")]
        [InlineData("../abc.txt",               "",                                         "abc.txt")]
        [InlineData("../abc.txt",               "/",                                        "abc.txt")]
        [InlineData("../abc.txt",               "folderA;folderB",                          "folderA/abc.txt;folderB/abc.txt")]
        [InlineData("../abc.txt",               "folderA;contentFiles",                     "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("../abc.txt",               "folderA;contentFiles/",                    "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("../abc.txt",               "folderA;contentFiles\\",                   "folderA/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("../abc.txt",               "folderA;contentFiles/folderA",             "folderA/abc.txt;contentFiles/folderA/abc.txt")]
        // ## is a special syntax specifically for this test which means that ## should be replaced by the absolute path to the project directory.
        [InlineData("##/abc.txt",               null,                                       "content/abc.txt;contentFiles/any/netstandard1.4/abc.txt")]
        [InlineData("##/folderA/abc.txt",       null,                                       "content/folderA/abc.txt;contentFiles/any/netstandard1.4/folderA/abc.txt")]
        [InlineData("##/../abc.txt",            null,                                       "content/abc.txt")]
        [InlineData("##/abc.txt",               "",                                         "abc.txt")]
        [InlineData("##/abc.txt",               "folderX;folderY",                          "folderX/abc.txt;folderY/abc.txt")]
        [InlineData("##/folderA/abc.txt",       "folderX;folderY",                          "folderX/abc.txt;folderY/abc.txt")]
        [InlineData("##/../abc.txt",            "folderX;folderY",                          "folderX/abc.txt;folderY/abc.txt")]
        
        public void PackCommand_PackProject_PackagePathPacksContentCorrectly(string sourcePath, string packagePath, string expectedTargetPaths)
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
                var projectFileContents = packagePath != null ? 
                                    $@"<Project Sdk=""Microsoft.NET.Sdk"" ToolsVersion=""15.0"">

                  <PropertyGroup>
                    <TargetFrameworks>netstandard1.4</TargetFrameworks>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include=""**\*.cs"" />
                    <EmbeddedResource Include=""**\*.resx"" />
                    <Content Include=""{sourcePath}"">
                      <PackagePath>{packagePath}</PackagePath>
                    </Content>
                  </ItemGroup>
                  <ItemGroup>
                    <PackageReference Include=""NETStandard.Library"" Version=""1.6"" />
                  </ItemGroup>
                </Project>" 

                : $@"<Project Sdk=""Microsoft.NET.Sdk"" ToolsVersion=""15.0"">

                  <PropertyGroup>
                    <TargetFrameworks>netstandard1.4</TargetFrameworks>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include=""**\*.cs"" />
                    <EmbeddedResource Include=""**\*.resx"" />
                    <Content Include=""{sourcePath}""/>
                  </ItemGroup>
                  <ItemGroup>
                    <PackageReference Include=""NETStandard.Library"" Version=""1.6"" />
                  </ItemGroup>
                </Project>";

                // Create the subdirectory structure for testing possible source paths for the content file
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA"));
                Directory.CreateDirectory(Path.Combine(workingDirectory, "folderA", "folderB"));

                msbuildFixture.CreateDotnetNewProject(testDirectory.Path, projectName);
                File.WriteAllText(Path.Combine(workingDirectory, $"{projectName}.csproj"), projectFileContents);
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

    }
}



