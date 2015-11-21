using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetPackCommandTest
    {
        [Fact]
        public void PackCommand_IncludeExcludePackageFromNuspec()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(workingDirectory);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles/any/any"),
                    "image.jpg",
                    "");

                Util.CreateFile(
                    workingDirectory,
                    "packageA.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>packageA</id>
    <version>1.0.0</version>
    <title>packageA</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
    <dependencies>
        <group>
            <dependency id=""packageB"" version=""1.0.0"" include=""a,b,c"" exclude=""b,c"" />
            <dependency id=""packageC"" version=""1.0.0"" include=""a,b,c"" />
            <dependency id=""packageD"" version=""1.0.0"" exclude=""a,b,c"" />
            <dependency id=""packageE"" version=""1.0.0"" exclude=""z"" />
        </group>
    </dependencies>
  </metadata>
</package>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack packageA.nuspec",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var path = Path.Combine(workingDirectory, "packageA.1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                using (var zip = new ZipArchive(File.OpenRead(path)))
                {
                    var manifestReader
                        = new StreamReader(zip.Entries.Single(file => file.FullName == "packageA.nuspec").Open());
                    var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());

                    var node = nuspecXml.Descendants().Single(e => e.Name.LocalName == "dependencies");

                    var actual = node.ToString().Replace("\r\n", "\n");

                    Assert.Equal(
                        @"<dependencies xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <group>
    <dependency id=""packageB"" version=""1.0.0"" include=""a,b,c"" exclude=""b,c"" />
    <dependency id=""packageC"" version=""1.0.0"" include=""a,b,c"" />
    <dependency id=""packageD"" version=""1.0.0"" exclude=""a,b,c"" />
    <dependency id=""packageE"" version=""1.0.0"" exclude=""z"" />
  </group>
</dependencies>".Replace("\r\n", "\n"), actual);
                }
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        [Fact]
        public void PackCommand_PackRuntimesRefNativeNoWarnings()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(workingDirectory);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "ref/uap10.0"),
                    "a.dll",
                    string.Empty);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "native"),
                    "a.dll",
                    string.Empty);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "runtimes/win-x86/lib/uap10.0"),
                    "a.dll",
                    string.Empty);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "lib/uap10.0"),
                    "a.dll",
                    string.Empty);

                Util.CreateFile(
                    workingDirectory,
                    "packageA.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>packageA</id>
    <version>1.0.0</version>
    <title>packageA</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
  </metadata>
</package>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack packageA.nuspec",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var path = Path.Combine(workingDirectory, "packageA.1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                var files = package.GetFiles().Select(f => f.Path).OrderBy(s => s).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                            @"lib\uap10.0\a.dll",
                            @"native\a.dll",
                            @"ref\uap10.0\a.dll",
                            @"runtimes\win-x86\lib\uap10.0\a.dll",
                    });

                Assert.False(r.Item2.Contains("Assembly outside lib folder"));
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        [Fact]
        public void PackCommand_PackAnalyzers()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(workingDirectory);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "analyzers/cs/code"),
                    "a.dll",
                    "");

                Util.CreateFile(
                    workingDirectory,
                    "packageA.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>packageA</id>
    <version>1.0.0</version>
    <title>packageA</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
  </metadata>
</package>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack packageA.nuspec",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var path = Path.Combine(workingDirectory, "packageA.1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                            @"analyzers\cs\code\a.dll",
                    });

                Assert.False(r.Item2.Contains("Assembly outside lib folder"));
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        [Fact]
        public void PackCommand_ContentV2PackageFromNuspec()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(workingDirectory);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles/any/any"),
                    "image.jpg",
                    "");

                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles/cs/net45"),
                    "code.cs",
                    "");

                Util.CreateFile(
                    workingDirectory,
                    "packageA.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>packageA</id>
    <version>1.0.0</version>
    <title>packageA</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
    <contentFiles>
        <files include=""**/*"" exclude=""**/*.cs"" buildAction=""None"" copyToOutput=""true"" flatten=""true"" />
        <files include=""**/*.cs"" buildAction=""Compile"" />
    </contentFiles>
  </metadata>
</package>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack packageA.nuspec",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var path = Path.Combine(workingDirectory, "packageA.1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                using (var zip = new ZipArchive(File.OpenRead(path)))
                {
                    var manifestReader
                        = new StreamReader(zip.Entries.Single(file => file.FullName == "packageA.nuspec").Open());
                    var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());

                    var node = nuspecXml.Descendants().Single(e => e.Name.LocalName == "contentFiles");

                    var files = package.GetFiles().Select(f => f.Path).ToArray();
                    Array.Sort(files);

                    Assert.Equal(
                        files,
                        new string[]
                        {
                        @"contentFiles\any\any\image.jpg",
                        @"contentFiles\cs\net45\code.cs",
                        });

                    Assert.Equal(
                        @"<contentFiles xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <files include=""**/*"" exclude=""**/*.cs"" buildAction=""None"" copyToOutput=""true"" flatten=""true"" />
  <files include=""**/*.cs"" buildAction=""Compile"" />
</contentFiles>".Replace("\r\n", "\n"),
                        node.ToString().Replace("\r\n", "\n"));
                }
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test that when creating a package from project file, referenced projects
        // are also included in the package.
        [Fact]
        public void PackCommand_WithProjectReferences()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Util.CreateDirectory(workingDirectory);
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");
                Util.CreateDirectory(proj1Directory);
                Util.CreateDirectory(proj2Directory);

                // create project 1
                Util.CreateFile(
                    proj1Directory,
                    "proj1.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file1.cs",
@"using System;

namespace Proj1
{
    public class Class1
    {
        public int A { get; set; }
    }
}");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file2.txt",
                    "file2");

                // Create project 2, which references project 1
                Util.CreateFile(
                    proj2Directory,
                    "proj2.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include='..\proj1\proj1.csproj' />
  </ItemGroup>
  <ItemGroup>
    <Compile Include='proj2_file1.cs' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj2Directory,
                    "proj2_file1.cs",
@"using System;

namespace Proj2
{
    public class Class1
    {
        public int A { get; set; }
    }
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj2Directory,
                    "pack proj2.csproj -build -IncludeReferencedProjects",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);
                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"content\proj1_file2.txt",
                        @"lib\net40\proj1.dll",
                        @"lib\net40\proj2.dll"
                    });
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test creating symbol package with -IncludeReferencedProject.
        [Fact]
        public void PackCommand_WithProjectReferencesSymbols()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Util.CreateDirectory(workingDirectory);
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");
                Util.CreateDirectory(proj1Directory);
                Util.CreateDirectory(proj2Directory);

                // create project 1
                Util.CreateFile(
                    proj1Directory,
                    "proj1.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file1.cs",
@"using System;

namespace Proj1
{
    public class Class1
    {
        public int A { get; set; }
    }
}");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file2.txt",
                    "file2");

                // Create project 2, which references project 1
                Util.CreateFile(
                    proj2Directory,
                    "proj2.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include='..\proj1\proj1.csproj' />
  </ItemGroup>
  <ItemGroup>
    <Compile Include='proj2_file1.cs' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj2Directory,
                    "proj2_file1.cs",
@"using System;

namespace Proj2
{
    public class Class1
    {
        public int A { get; set; }
    }
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj2Directory,
                    "pack proj2.csproj -build -IncludeReferencedProjects -symbols",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.0.symbols.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);
                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"content\proj1_file2.txt",
                        @"lib\net40\proj1.dll",
                        @"lib\net40\proj1.pdb",
                        @"lib\net40\proj2.dll",
                        @"lib\net40\proj2.pdb",
                        @"src\proj1\proj1_file1.cs",
                        @"src\proj2\proj2_file1.cs",
                    });
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test that when creating a package from project file, a referenced project that
        // has a nuspec file is added as dependency.
        [Fact]
        public void PackCommand_ReferencedProjectWithNuspecFile()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(workingDirectory);

                // create test projects. There are 7 projects, with the following
                // dependency relationships:
                // proj1 depends on proj2 & proj3
                // proj2 depends on proj4 & proj5
                // proj3 depends on proj5 & proj7
                //
                // proj2 and proj6 have nuspec files.
                CreateTestProject(workingDirectory, "proj1",
                    new string[] {
                        @"..\proj2\proj2.csproj",
                        @"..\proj3\proj3.csproj"
                    });
                CreateTestProject(workingDirectory, "proj2",
                    new string[] {
                        @"..\proj4\proj4.csproj",
                        @"..\proj5\proj5.csproj"
                    });
                CreateTestProject(workingDirectory, "proj3",
                    new string[] {
                        @"..\proj6\proj6.csproj",
                        @"..\proj7\proj7.csproj"
                    });
                CreateTestProject(workingDirectory, "proj4", null);
                CreateTestProject(workingDirectory, "proj5", null);
                CreateTestProject(workingDirectory, "proj6", null);
                CreateTestProject(workingDirectory, "proj7", null);
                Util.CreateFile(
                    Path.Combine(workingDirectory, "proj2"),
                    "proj2.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>proj2</id>
    <version>1.0.0.0</version>
    <title>Proj2</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
    <dependencies>
      <dependency id='p1' version='1.5.11' />
    </dependencies>
  </metadata>
</package>");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "proj6"),
                    "proj6.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>proj6</id>
    <version>2.0.0.0</version>
    <title>Proj6</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
    <dependencies>
      <dependency id='p2' version='1.5.11' />
    </dependencies>
  </metadata>
</package>");

                // Act
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.csproj -build -IncludeReferencedProjects",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                // proj3 and proj7 are included in the package.
                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\net40\proj1.dll",
                        @"lib\net40\proj3.dll",
                        @"lib\net40\proj7.dll"
                    });

                // proj2 and proj6 are added as dependencies.
                var dependencies = package.DependencySets.First().Dependencies.OrderBy(d => d.Id);
                Assert.Equal(
                    dependencies,
                    new PackageDependency[]
                    {
                        new PackageDependency("proj2", VersionUtility.ParseVersionSpec("1.0.0.0")),
                        new PackageDependency("proj6", VersionUtility.ParseVersionSpec("2.0.0.0"))
                    },
                    new PackageDepencyComparer());
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Same test as PackCommand_ReferencedProjectWithNuspecFile, but with -MSBuidVersion
        // set to 14
        [Fact]
        public void PackCommand_ReferencedProjectWithNuspecFileWithMsbuild14()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(workingDirectory);

                // create test projects. There are 7 projects, with the following
                // dependency relationships:
                // proj1 depends on proj2 & proj3
                // proj2 depends on proj4 & proj5
                // proj3 depends on proj5 & proj7
                //
                // proj2 and proj6 have nuspec files.
                CreateTestProject(workingDirectory, "proj1",
                    new string[] {
                        @"..\proj2\proj2.csproj",
                        @"..\proj3\proj3.csproj"
                    });
                CreateTestProject(workingDirectory, "proj2",
                    new string[] {
                        @"..\proj4\proj4.csproj",
                        @"..\proj5\proj5.csproj"
                    });
                CreateTestProject(workingDirectory, "proj3",
                    new string[] {
                        @"..\proj6\proj6.csproj",
                        @"..\proj7\proj7.csproj"
                    });
                CreateTestProject(workingDirectory, "proj4", null);
                CreateTestProject(workingDirectory, "proj5", null);
                CreateTestProject(workingDirectory, "proj6", null);
                CreateTestProject(workingDirectory, "proj7", null);
                Util.CreateFile(
                    Path.Combine(workingDirectory, "proj2"),
                    "proj2.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>proj2</id>
    <version>1.0.0.0</version>
    <title>Proj2</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
    <dependencies>
      <dependency id='p1' version='1.5.11' />
    </dependencies>
  </metadata>
</package>");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "proj6"),
                    "proj6.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>proj6</id>
    <version>2.0.0.0</version>
    <title>Proj6</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
    <dependencies>
      <dependency id='p2' version='1.5.11' />
    </dependencies>
  </metadata>
</package>");

                // Act
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    @"pack proj1.csproj -build -IncludeReferencedProjects  -MSBuildVersion 14",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                // proj3 and proj7 are included in the package.
                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\net40\proj1.dll",
                        @"lib\net40\proj3.dll",
                        @"lib\net40\proj7.dll"
                    });

                // proj2 and proj6 are added as dependencies.
                var dependencies = package.DependencySets.First().Dependencies.OrderBy(d => d.Id);
                Assert.Equal(
                    dependencies,
                    new PackageDependency[]
                    {
                        new PackageDependency("proj2", VersionUtility.ParseVersionSpec("1.0.0.0")),
                        new PackageDependency("proj6", VersionUtility.ParseVersionSpec("2.0.0.0"))
                    },
                    new PackageDepencyComparer());
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test that when creating a package from project file, a referenced project that
        // has a nuspec file is added as dependency. withCustomReplacementTokens = true
        // adds the token $prefix$ to the referenced nuspec's id field (see Issue #3536)
        [Fact]
        public void PackCommand_ReferencedProjectWithCustomTokensInNuspec()
        {
            const string prefixTokenValue = "fooBar";

            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(workingDirectory);

                // create test projects. There are 7 projects, with the following
                // dependency relationships:
                // proj1 depends on proj2 & proj3
                // proj2 depends on proj4 & proj5
                // proj3 depends on proj5 & proj7
                //
                // proj2 and proj6 have nuspec files.
                CreateTestProject(workingDirectory, "proj1",
                    new string[] {
                        @"..\proj2\proj2.csproj",
                        @"..\proj3\proj3.csproj"
                    });
                CreateTestProject(workingDirectory, "proj2",
                    new string[] {
                        @"..\proj4\proj4.csproj",
                        @"..\proj5\proj5.csproj"
                    });
                CreateTestProject(workingDirectory, "proj3",
                    new string[] {
                        @"..\proj6\proj6.csproj",
                        @"..\proj7\proj7.csproj"
                    });
                CreateTestProject(workingDirectory, "proj4", null);
                CreateTestProject(workingDirectory, "proj5", null);
                CreateTestProject(workingDirectory, "proj6", null);
                CreateTestProject(workingDirectory, "proj7", null);
                Util.CreateFile(
                    Path.Combine(workingDirectory, "proj2"),
                    "proj2.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>proj2</id>
    <version>1.0.0.0</version>
    <title>Proj2</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
    <dependencies>
      <dependency id='p1' version='1.5.11' />
    </dependencies>
  </metadata>
</package>");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "proj6"),
                    "proj6.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>$prefix$proj6</id>
    <version>2.0.0.0</version>
    <title>Proj6</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
    <dependencies>
      <dependency id='p2' version='1.5.11' />
    </dependencies>
  </metadata>
</package>");

                // Act
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.csproj -build -IncludeReferencedProjects -Properties prefix=" + prefixTokenValue,
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.0.nupkg"));

                // proj2 and proj6 are added as dependencies.
                var dependencies = package.DependencySets.First().Dependencies.OrderBy(d => d.Id);
                Assert.Equal(
                    dependencies.OrderBy(d => d.ToString()),
                    new PackageDependency[]
                    {
                        new PackageDependency("proj2", VersionUtility.ParseVersionSpec("1.0.0.0")),
                        new PackageDependency(prefixTokenValue + "proj6", VersionUtility.ParseVersionSpec("2.0.0.0"))
                    }.OrderBy(d => d.ToString()),
                    new PackageDepencyComparer());
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test that recognized tokens such as $id$ in the nuspec file of the
        // referenced project are replaced.
        [Fact]
        public void PackCommand_NuspecFileWithTokens()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(workingDirectory);

                CreateTestProject(workingDirectory, "proj1",
                    new string[] {
                        @"..\proj2\proj2.csproj"
                    });
                CreateTestProject(workingDirectory, "proj2", null, "v4.0", "1.2");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "proj2"),
                    "proj2.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>$id$</id>
    <version>$version$</version>
    <title>Proj2</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
    <dependencies>
      <dependency id='p1' version='1.5.11' />
    </dependencies>
  </metadata>
</package>");

                // Act
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.csproj -build -IncludeReferencedProjects",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\net40\proj1.dll"
                    });

                // proj2 is added as dependencies.
                var dependencies = package.DependencySets.First().Dependencies.OrderBy(d => d.Id);
                Assert.Equal(
                    dependencies,
                    new PackageDependency[]
                    {
                        new PackageDependency("proj2", VersionUtility.ParseVersionSpec("1.2.0.0"))
                    },
                    new PackageDepencyComparer());
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test that option -IncludeReferencedProjects works correctly for the case
        // where the same project is referenced by multiple projects in the
        // reference hierarchy.
        [Fact]
        public void PackCommand_ProjectReferencedByMultipleProjects()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(workingDirectory);

                // create test project, with the following dependency relationships:
                // proj1 depends on proj2, proj3
                // proj2 depends on proj3
                CreateTestProject(workingDirectory, "proj1",
                    new string[] {
                        @"..\proj2\proj2.csproj",
                        @"..\proj3\proj3.csproj"
                    });
                CreateTestProject(workingDirectory, "proj2",
                    new string[] {
                        @"..\proj3\proj3.csproj"
                    });
                CreateTestProject(workingDirectory, "proj3", null);

                // Act
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.csproj -build -IncludeReferencedProjects",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\net40\proj1.dll",
                        @"lib\net40\proj2.dll",
                        @"lib\net40\proj3.dll"
                    });
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test that when creating a package from project A, the output of a referenced project
        // will be added to the same target framework folder as A, regardless of the target
        // framework of the referenced project.
        [Fact]
        public void PackCommand_ReferencedProjectWithDifferentTarget()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Arrange
                Util.CreateDirectory(workingDirectory);

                CreateTestProject(workingDirectory, "proj1",
                    new string[] {
                        @"..\proj2\proj2.csproj"
                    });
                CreateTestProject(workingDirectory, "proj2", null, "v3.0");

                // Act
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.csproj -build -IncludeReferencedProjects",
                    waitForExit: true);
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\net40\proj1.dll",
                        @"lib\net40\proj2.dll"
                    });
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test that when -IncludeReferencedProjects is not specified,
        // pack command will not try to look for the output files of the
        // referenced projects.
        [Fact(Skip = "This test failed on dev10 build with mysterious errors. Will reenable it once the cause is figured out")]
        public void PackCommand_IncludeReferencedProjectsOff()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var oldCurrentDirectory = Directory.GetCurrentDirectory();

            try
            {
                Util.CreateDirectory(workingDirectory);
                Directory.SetCurrentDirectory(workingDirectory);
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");
                Util.CreateDirectory(proj1Directory);
                Util.CreateDirectory(proj2Directory);

                // create project 1
                Util.CreateFile(
                    proj1Directory,
                    "proj1.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <Config Condition=" + "\" '$(Config)' == ''\"" + @">Debug</Config>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Debug'\"" + @">
    <OutputPath>debug_out</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Release'\"" + @">
    <OutputPath>release_out</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file1.cs",
@"using System;

namespace Proj1
{
    public class Class1
    {
        public int A { get; set; }
    }
}");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file2.txt",
                    "file2");

                // Create project 2, which references project 1
                Util.CreateFile(
                    proj2Directory,
                    "proj2.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <Config Condition=" + "\" '$(Config)' == ''\"" + @">Debug</Config>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Debug'\"" + @">
    <OutputPath>debug_out</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Release'\"" + @">
    <OutputPath>release_out</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include='..\proj1\proj1.csproj' />
  </ItemGroup>
  <ItemGroup>
    <Compile Include='proj2_file1.cs' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj2Directory,
                    "proj2_file1.cs",
@"using System;

namespace Proj2
{
    public class Class1
    {
        public int A { get; set; }
    }
}");

                var msbuild = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"Microsoft.NET\Framework\v4.0.30319\msbuild.exe");
                var r = CommandRunner.Run(
                    msbuild,
                    proj2Directory,
                    "proj2.csproj /p:Config=Release",
                    waitForExit: true);

                // Act
                r = CommandRunner.Run(
                    nugetexe,
                    proj2Directory,
                    "pack proj2.csproj -p Config=Release",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);
                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\net40\proj2.dll"
                    });
            }
            finally
            {
                Directory.SetCurrentDirectory(oldCurrentDirectory);
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test that when -IncludeReferencedProjects is specified, the properties
        // passed thru command line will be applied if a referenced project
        // needs to be built.
        [Fact]
        public void PackCommand_PropertiesAppliedToReferencedProjects()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Util.CreateDirectory(workingDirectory);
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");
                Util.CreateDirectory(proj1Directory);
                Util.CreateDirectory(proj2Directory);

                // create project 1
                Util.CreateFile(
                    proj1Directory,
                    "proj1.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <Config Condition=" + "\" '$(Config)' == ''\"" + @">Debug</Config>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Debug'\"" + @">
    <OutputPath>debug_out</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Release'\"" + @">
    <OutputPath>release_out</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file1.cs",
@"using System;

namespace Proj1
{
    public class Class1
    {
        public int A { get; set; }
    }
}");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file2.txt",
                    "file2");

                // Create project 2, which references project 1
                Util.CreateFile(
                    proj2Directory,
                    "proj2.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <Config Condition=" + "\" '$(Config)' == ''\"" + @">Debug</Config>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Debug'\"" + @">
    <OutputPath>debug_out</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Release'\"" + @">
    <OutputPath>release_out</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include='..\proj1\proj1.csproj' />
  </ItemGroup>
  <ItemGroup>
    <Compile Include='proj2_file1.cs' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj2Directory,
                    "proj2_file1.cs",
@"using System;

namespace Proj2
{
    public class Class1
    {
        public int A { get; set; }
    }
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj2Directory,
                    "pack proj2.csproj -build -IncludeReferencedProjects -p Config=Release",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert

                // Verify that proj1 was not built using the default config "Debug".
                Assert.False(Directory.Exists(Path.Combine(proj1Directory, "debug_out")));

                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);
                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"content\proj1_file2.txt",
                        @"lib\net40\proj1.dll",
                        @"lib\net40\proj2.dll"
                    });
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test that exclude masks starting with '**' work also
        // for files outside of the package/project root.
        [Fact]
        public void PackCommand_ExcludesFilesOutsideRoot()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var projDirectory = Path.Combine(workingDirectory, "package");
            var otherDirectory = Path.Combine(workingDirectory, "other");

            try
            {
                // Arrange
                Util.CreateDirectory(workingDirectory);
                Util.CreateDirectory(projDirectory);
                Util.CreateDirectory(otherDirectory);

                Util.CreateFile(projDirectory, "include.me", "some text");
                Util.CreateFile(projDirectory, "exclude.me", "some text");
                Util.CreateFile(otherDirectory, "exclude.me", "some text");

                Util.CreateFile(
                    projDirectory,
                    "package.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>ExcludeBug</id>
    <version>0.1.0.0</version>
    <authors>test</authors>
    <description>Sample package for reproducing bug in file/@exclude matching.</description>
  </metadata>
  <files>
    <file src=""**"" exclude=""**\exclude.me"" target=""Content\package"" />
    <file src=""..\other\**"" exclude=""**\exclude.me"" target=""Content\other"" />
  </files>
</package>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    projDirectory,
                    "pack package.nuspec",
                    waitForExit: true);

                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(projDirectory, "ExcludeBug.0.1.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"Content\package\include.me"
                    });
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test that NuGet packages of the project are added as dependencies
        [Theory]
        [InlineData("packages.config")]
        [InlineData("packages.proj1.config")]
        public void PackCommand_PackagesAddedAsDependencies(string packagesConfigFileName)
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Util.CreateDirectory(workingDirectory);
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var packagesFolder = Path.Combine(proj1Directory, "packages");
                Util.CreateDirectory(proj1Directory);
                Util.CreateDirectory(packagesFolder);

                // create project 1
                Util.CreateFile(
                    proj1Directory,
                    "proj1.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file1.cs",
@"using System;

namespace Proj1
{
    public class Class1
    {
        public int A { get; set; }
    }
}");
                Util.CreateFile(
                    proj1Directory,
                    packagesConfigFileName,
@"<?xml version=""1.0"" encoding=""utf-8""?>
<packages>
  <package id=""testPackage1"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""testPackage2"" version=""1.1.0"" targetFramework=""net45"" developmentDependency=""true"" />
</packages>");
                Util.CreateTestPackage("testPackage1", "1.1.0", packagesFolder);
                Util.CreateTestPackage("testPackage2", "1.1.0", packagesFolder);

                Util.CreateFile(
                    proj1Directory,
                    "proj1_file2.txt",
                    "file2");

                Util.CreateFile(
                    proj1Directory,
                    "test.sln",
                    "# test solution");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.csproj -build",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.0.nupkg"));
                Assert.Equal(1, package.DependencySets.Count());
                var dependencySet = package.DependencySets.First();

                // verify that only testPackage1 is added as dependency. testPackage2 is not adde
                // as dependency because its developmentDependency is true.
                Assert.Equal(1, dependencySet.Dependencies.Count);
                var dependency = dependencySet.Dependencies.First();
                Assert.Equal("testPackage1", dependency.Id);
                Assert.Equal("1.1.0", dependency.VersionSpec.ToString());
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Test that nuget displays warnings when dependency version is not specified
        // in nuspec.
        [Fact]
        public void PackCommand_WarningDependencyVersionNotSpecified()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Util.CreateDirectory(workingDirectory);
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                Util.CreateDirectory(proj1Directory);

                // create project 1
                Util.CreateFile(
                    proj1Directory,
                    "proj1.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <Config Condition=" + "\" '$(Config)' == ''\"" + @">Debug</Config>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Debug'\"" + @">
    <OutputPath>debug_out</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Release'\"" + @">
    <OutputPath>release_out</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file1.cs",
@"using System;

namespace Proj1
{
    public class Class1
    {
        public int A { get; set; }
    }
}");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file2.txt",
                    "file2");

                Util.CreateFile(
                    proj1Directory,
                    "proj1.nuspec",
@"<?xml version=""1.0""?>
<package >
  <metadata>
    <id>Package</id>
    <version>1.0.0</version>
    <authors>feiling</authors>
    <owners>feiling</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>description</description>
    <releaseNotes>release notes</releaseNotes>
    <copyright>Copyright 2013</copyright>
    <dependencies>
      <dependency id=""json"" />
    </dependencies>
  </metadata>
  <files>
    <file src=""release_out\"" target=""lib\net40"" />
  </files>
</package>");
                var msbuild = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"Microsoft.NET\Framework\v4.0.30319\msbuild.exe");
                var r = CommandRunner.Run(
                    msbuild,
                    proj1Directory,
                    "proj1.csproj /p:Config=Release",
                    waitForExit: true);

                // Act
                r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.nuspec",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                Assert.Contains("Issue: Specify version of dependencies.", r.Item2);
                Assert.Contains("Description: The version of dependency 'json' is not specified.", r.Item2);
                Assert.Contains("Solution: Specifiy the version of dependency and rebuild your package.", r.Item2);
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        // Tests that with -MSBuildVersion set to 14, a projec using C# 6.0 features (nameof in this test)
        // can be built successfully.
        [Fact]
        public void PackCommand_WithMsBuild14()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Util.CreateDirectory(workingDirectory);
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");
                Util.CreateDirectory(proj1Directory);
                Util.CreateDirectory(proj2Directory);

                // create project 1
                Util.CreateFile(
                    proj1Directory,
                    "proj1.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <Config Condition=" + "\" '$(Config)' == ''\"" + @">Debug</Config>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Debug'\"" + @">
    <OutputPath>debug_out</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Release'\"" + @">
    <OutputPath>release_out</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file1.cs",
@"using System;

namespace Proj1
{
    public class Class1
    {
        public string F(int a)
        {
            return nameof(a);
        }
    }
}");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file2.txt",
                    "file2");

                // Create project 2, which references project 1
                Util.CreateFile(
                    proj2Directory,
                    "proj2.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <Config Condition=" + "\" '$(Config)' == ''\"" + @">Debug</Config>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Debug'\"" + @">
    <OutputPath>debug_out</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Release'\"" + @">
    <OutputPath>release_out</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include='..\proj1\proj1.csproj' />
  </ItemGroup>
  <ItemGroup>
    <Compile Include='proj2_file1.cs' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj2Directory,
                    "proj2_file1.cs",
@"using System;

namespace Proj2
{
    public class Class1
    {
        public string F2(int a)
        {
            return nameof(a);
        }
    }
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj2Directory,
                    @"pack proj2.csproj -build -IncludeReferencedProjects -p Config=Release -msbuildversion 14",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert

                // Verify that proj1 was not built using the default config "Debug".
                Assert.False(Directory.Exists(Path.Combine(proj1Directory, "debug_out")));

                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);
                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"content\proj1_file2.txt",
                        @"lib\net40\proj1.dll",
                        @"lib\net40\proj2.dll"
                    });
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }        

        // Tests that pack works with -MSBuildVersion set to 12
        [Fact]
        public void PackCommand_WithMsBuild12()
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Util.CreateDirectory(workingDirectory);
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");
                Util.CreateDirectory(proj1Directory);
                Util.CreateDirectory(proj2Directory);

                // create project 1
                Util.CreateFile(
                    proj1Directory,
                    "proj1.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <Config Condition=" + "\" '$(Config)' == ''\"" + @">Debug</Config>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Debug'\"" + @">
    <OutputPath>debug_out</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Release'\"" + @">
    <OutputPath>release_out</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs' />
  </ItemGroup>
  <ItemGroup>
    <Content Include='proj1_file2.txt' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file1.cs",
@"using System;

namespace Proj1
{
    public class Class1
    {
        public string F(int a)
        {
            return a.ToString();
        }
    }
}");
                Util.CreateFile(
                    proj1Directory,
                    "proj1_file2.txt",
                    "file2");

                // Create project 2, which references project 1
                Util.CreateFile(
                    proj2Directory,
                    "proj2.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <Config Condition=" + "\" '$(Config)' == ''\"" + @">Debug</Config>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Debug'\"" + @">
    <OutputPath>debug_out</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition=" + "\"'$(Config)'=='Release'\"" + @">
    <OutputPath>release_out</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include='..\proj1\proj1.csproj' />
  </ItemGroup>
  <ItemGroup>
    <Compile Include='proj2_file1.cs' />
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");
                Util.CreateFile(
                    proj2Directory,
                    "proj2_file1.cs",
@"using System;

namespace Proj2
{
    public class Class1
    {
        public string F2(int a)
        {
            return a.ToString();
        }
    }
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj2Directory,
                    @"pack proj2.csproj -build -IncludeReferencedProjects -p Config=Release -MSBuildVersion 12",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert

                // Verify that proj1 was not built using the default config "Debug".
                Assert.False(Directory.Exists(Path.Combine(proj1Directory, "debug_out")));

                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);
                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"content\proj1_file2.txt",
                        @"lib\net40\proj1.dll",
                        @"lib\net40\proj2.dll"
                    });
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }

        /// <summary>
        /// Creates a simple project.
        /// </summary>
        /// <remarks>
        /// The project is created under directory baseDirectory\projectName.
        /// The project contains just one file called file1.cs.
        /// </remarks>
        /// <param name="baseDirectory">The base directory.</param>
        /// <param name="projectName">The name of the project.</param>
        /// <param name="referencedProject">The list of projects referenced by this project. Can be null.</param>
        /// <param name="targetFrameworkVersion">The target framework version of the project.</param>
        /// <param name="version">The version of the assembly.</param>
        private void CreateTestProject(
            string baseDirectory,
            string projectName,
            string[] referencedProject,
            string targetFrameworkVersion = "v4.0",
            string version = "0.0.0.0")
        {
            var projectDirectory = Path.Combine(baseDirectory, projectName);
            Util.CreateDirectory(projectDirectory);

            string reference = string.Empty;
            if (referencedProject != null && referencedProject.Length > 0)
            {
                var sb = new StringBuilder();
                sb.Append("<ItemGroup>");
                foreach (var r in referencedProject)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "<ProjectReference Include='{0}' />", r);
                }
                sb.Append("</ItemGroup>");

                reference = sb.ToString();
            }

            Util.CreateFile(
                projectDirectory,
                projectName + ".csproj",
                string.Format(CultureInfo.InvariantCulture,
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>{0}</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='file1.cs' />
  </ItemGroup>
{1}
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>", targetFrameworkVersion, reference));

            Util.CreateFile(
                projectDirectory,
                "file1.cs",
@"using System;
using System.Reflection;

[assembly: AssemblyVersion(" + "\"" + version + "\"" + @")]
namespace " + projectName + @"
{
    public class Class1
    {
        public int A { get; set; }
    }
}");
        }

        private class PackageDepencyComparer : IEqualityComparer<PackageDependency>
        {
            public bool Equals(PackageDependency x, PackageDependency y)
            {
                return string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        x.VersionSpec.ToString(),
                        y.VersionSpec.ToString(),
                        StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(PackageDependency obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}