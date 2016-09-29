using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetPackCommandTest
    {
        [Fact]
        public void PackCommand_IncludeExcludePackageFromNuspec()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles/any/any"),
                    "image.jpg",
                    "");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles/any/any"),
                    "image2.jpg",
                    "");

                Util.CreateFile(
                    workingDirectory,
                    "packageA.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>packageA</id>
    <version>1.0.0.2</version>
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
  <files>
    <file src=""contentFiles/any/any/image.jpg"" target=""\Content\image.jpg"" />
    <file src=""contentFiles/any/any/image2.jpg"" target=""Content\other\image2.jpg"" />
  </files>
</package>");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack packageA.nuspec",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var path = Path.Combine(workingDirectory, "packageA.1.0.0.2.nupkg");
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

                    var files = package.GetFiles().Select(f => f.Path).ToArray();
                    Assert.Contains(@"Content\image.jpg", files);
                    Assert.Contains(@"Content\other\image2.jpg", files);
                }
            }
        }

        [Fact]
        public void PackCommand_IncludeExcludePackageFromJson()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles/any/any"),
                    "image.jpg",
                    "");

                Directory.CreateDirectory(
                    Path.Combine(workingDirectory, "bin/Debug"));

                Util.CreateFile(
                    workingDirectory,
                    Path.GetFileName(workingDirectory) + ".project.json",
                @"{
  ""version"": ""1.0.0"",
  ""title"": ""packageA"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""dependencies"": {
    ""packageB"": {
      ""version"": ""1.0.0"",
      ""include"": ""runtime, compile, build"",
      ""exclude"": ""runtime, compile""
    },
    ""packageC"": {
      ""version"": ""1.0.0"",
      ""include"": ""runtime, compile, build"",
      ""suppressParent"": ""compile, build""
    },
    ""packageD"": {
      ""version"": ""1.0.0"",
      ""exclude"": ""runtime, compile, build""
    },
    ""packageE"": {
      ""version"": ""1.0.0"",
      ""include"": ""all"",
      ""suppressParent"": ""none""
    },
  },
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack " + Path.GetFileName(workingDirectory) + ".project.json",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                var id = Path.GetFileName(workingDirectory);

                // Assert
                var path = Path.Combine(workingDirectory, id + ".1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                using (var zip = new ZipArchive(File.OpenRead(path)))
                {
                    var manifestReader
                        = new StreamReader(zip.Entries.Single(file => file.FullName == id + ".nuspec").Open());
                    var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());

                    var node = nuspecXml.Descendants().Single(e => e.Name.LocalName == "dependencies");

                    var actual = node.ToString().Replace("\r\n", "\n");

                    Assert.Equal(
                        @"<dependencies xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <group>
    <dependency id=""packageB"" version=""1.0.0"" exclude=""Runtime,Compile,Build,Native,Analyzers"" />
    <dependency id=""packageC"" version=""1.0.0"" exclude=""Compile,Build,Native,Analyzers"" />
    <dependency id=""packageD"" version=""1.0.0"" exclude=""Runtime,Compile,Build,Analyzers"" />
    <dependency id=""packageE"" version=""1.0.0"" include=""All"" />
  </group>
</dependencies>".Replace("\r\n", "\n"), actual);
                }
            }
        }

        [Fact]
        public void PackCommand_PackageFromNuspecWithFrameworkAssemblies()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    workingDirectory,
                    "packageA.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>packageA</id>
    <version>1.0.0.2</version>
    <title>packageA</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
    <frameworkAssemblies>
      <frameworkAssembly assemblyName=""System"" />
      <frameworkAssembly assemblyName=""System.Core"" />
      <frameworkAssembly assemblyName=""System.Xml"" />
      <frameworkAssembly assemblyName=""System.Xml.Linq"" />
      <frameworkAssembly assemblyName=""System.Net.Http"" targetFramework="""" />
      <frameworkAssembly assemblyName=""System.Net.Http.Formatting"" targetFramework=""net45"" />
      <frameworkAssembly assemblyName=""System.ComponentModel.DataAnnotations"" targetFramework=""net35"" />
    </frameworkAssemblies>
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
                var path = Path.Combine(workingDirectory, "packageA.1.0.0.2.nupkg");
                var package = new OptimizedZipPackage(path);
                using (var zip = new ZipArchive(File.OpenRead(path)))
                {
                    var manifestReader
                        = new StreamReader(zip.Entries.Single(file => file.FullName == "packageA.nuspec").Open());
                    var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());

                    var node = nuspecXml.Descendants().Single(e => e.Name.LocalName == "frameworkAssemblies");

                    var actual = node.ToString().Replace("\r\n", "\n");

                    Assert.Equal(
                        @"<frameworkAssemblies xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
  <frameworkAssembly assemblyName=""System"" targetFramework="""" />
  <frameworkAssembly assemblyName=""System.Core"" targetFramework="""" />
  <frameworkAssembly assemblyName=""System.Xml"" targetFramework="""" />
  <frameworkAssembly assemblyName=""System.Xml.Linq"" targetFramework="""" />
  <frameworkAssembly assemblyName=""System.Net.Http"" targetFramework="""" />
  <frameworkAssembly assemblyName=""System.Net.Http.Formatting"" targetFramework="".NETFramework4.5"" />
  <frameworkAssembly assemblyName=""System.ComponentModel.DataAnnotations"" targetFramework="".NETFramework3.5"" />
</frameworkAssemblies>".Replace("\r\n", "\n"), actual);
                }
            }
        }

        [Fact]
        public void PackCommand_PackageFromNuspecWithEmptyFilesTag()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
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
    <copyright>Copyright © 2013</copyright>
    <frameworkAssemblies>
      <frameworkAssembly assemblyName=""System"" />
    </frameworkAssemblies>
  </metadata>
  <files />
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

                var files = package.GetFiles();
                Assert.Equal(0, files.Count());
            }
        }

        [Fact]
        public void PackCommand_PackageFromNuspecWithoutEmptyFilesTag()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
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
    <copyright>Copyright © 2013</copyright>
    <frameworkAssemblies>
      <frameworkAssembly assemblyName=""System"" />
    </frameworkAssemblies>
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

                var files = package.GetFiles();
                Assert.Equal(1, files.Count());
            }
        }

        [Fact]
        public void PackCommand_PackRuntimesRefNativeNoWarnings()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                string id = Path.GetFileName(workingDirectory);

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
        }

        [Fact]
        public void PackCommand_PackJsonRuntimesRefNativeNoWarnings()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange

                string id = Path.GetFileName(workingDirectory);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "bin/Debug/uap10.0"),
                    id + ".dll",
                    string.Empty);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "bin/Debug/native"),
                    id + ".dll",
                    string.Empty);

                Util.CreateFile(
                    workingDirectory,
                    "project.json",
                @"{
  ""version"": ""1.0.0"",
  ""title"": ""packageA"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""frameworks"": {
    ""native"": {
    },
    ""uap10.0"": {
    }
  }
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack project.json",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var path = Path.Combine(workingDirectory, Path.GetFileName(workingDirectory) + ".1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                var files = package.GetFiles().Select(f => f.Path).OrderBy(s => s).ToArray();

                Assert.Equal(
                    files,
                    new string[]
                    {
                            @"lib\native\" + id + ".dll",
                            @"lib\uap10.0\" + id + ".dll",
                    });

                Assert.False(r.Item2.Contains("Assembly outside lib folder"));
            }
        }

        [Fact]
        public void PackCommand_PackAnalyzers()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
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
                    new string[]
                    {
                        @"analyzers\cs\code\a.dll",
                    },
                    files);

                Assert.False(r.Item2.Contains("Assembly outside lib folder"));
            }
        }

        [Fact]
        public void PackCommand_PackAnalyzersJson()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                string id = Path.GetFileName(workingDirectory);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "bin/Debug/native"),
                    id + ".dll",
                    string.Empty);

                Util.CreateFile(
                    workingDirectory,
                    "project.json",
                @"{
  ""version"": ""1.0.0"",
  ""title"": ""packageA"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack project.json",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var path = Path.Combine(workingDirectory, Path.GetFileName(workingDirectory) + ".1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                            @"lib\native\" + id + ".dll",
                    });

                Assert.False(r.Item2.Contains("Assembly outside lib folder"));
            }
        }

        [Fact]
        public void PackCommand_ContentV2PackageFromNuspec()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
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
        }

        // Test that when creating a package from project file, referenced projects
        // are also included in the package.
        [Fact]
        public void PackCommand_WithProjectReferences()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");

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
                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.nupkg"));
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
        }

        [Fact]
        public void PackCommand_PclProjectWithProjectJsonAndTargetsNetStandard()
        {
            // This bug tests issue: https://github.com/NuGet/Home/issues/3108
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var packagesDirectory = Path.Combine(proj1Directory, "packages");
                // create project 1
                Util.CreateFile(
                    proj1Directory,
                    "proj1.csproj",
@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>bin\Debug\</OutputPath>
    <TargetFrameworkProfile>
    </TargetFrameworkProfile>
    <TargetFrameworkVersion>v5.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='proj1_file1.cs'/>
  </ItemGroup>
  <ItemGroup>
    <None Include='project.json'/>
  </ItemGroup>
  <Import Project='$(MSBuildExtensionsPath32)\Microsoft\Portable\$(TargetFrameworkVersion)\Microsoft.Portable.CSharp.targets'/>
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

                Util.CreateFile(proj1Directory,
                    "project.json",
                    @"{
  ""supports"": {},
  ""dependencies"": {
                    ""Microsoft.NETCore.Portable.Compatibility"": ""1.0.1"",
    ""NETStandard.Library"": ""1.6.0""
  },
  ""frameworks"": {
                    ""netstandard1.3"": { }
                }
            }
            ");

                var t = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "restore ",
                    waitForExit: true);

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.csproj -build ",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var nupkgName = Path.Combine(proj1Directory, "proj1.0.0.0.nupkg");
                Assert.True(File.Exists(nupkgName));
                var package = new OptimizedZipPackage(nupkgName);
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);
                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\netstandard1.3\proj1.dll"
                    });
            }
        }

        [Fact]
        public void PackCommand_WithTransformFile()
        {
            // This bug tests issue: https://github.com/NuGet/Home/issues/3160
            // Fixed by PR : https://github.com/NuGet/NuGet.Client/pull/768
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var packagesDirectory = Path.Combine(proj1Directory, "packages");
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
    <Compile Include='proj1_file1.cs'/>
  </ItemGroup>
  <ItemGroup>
    <Content Include='Web.config'/>
  </ItemGroup>
  <ItemGroup>
    <None Include='packages.config'/>
  </ItemGroup>
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets'/>
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
                    "Web.config",
                    @"<?xml version='1.0' encoding='utf-8' ?>
<configuration>
</configuration>");

                Util.CreateFile(proj1Directory,
                    "packages.config",
                    @"<?xml version='1.0' encoding='utf-8'?>
<packages>
  <package id = 'Microsoft.AspNet.WebApi' version = '5.2.3' targetFramework = 'net452'/>
  <package id = 'Microsoft.AspNet.WebApi.Client' version = '5.2.3' targetFramework = 'net452'/>
  <package id = 'Microsoft.AspNet.WebApi.Core' version = '5.2.3' targetFramework = 'net452'/>
  <package id = 'Microsoft.AspNet.WebApi.WebHost' version = '5.2.3' targetFramework = 'net452'/>
  <package id = 'Newtonsoft.Json' version = '6.0.4' targetFramework = 'net452'/>
</packages> ");

                var t = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "restore packages.config -PackagesDirectory " + packagesDirectory,
                    waitForExit: true);

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.csproj -build ",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var nupkgName = Path.Combine(proj1Directory, "proj1.0.0.0.nupkg");
                Assert.True(File.Exists(nupkgName));
            }
        }

        // Test creating symbol package with -IncludeReferencedProject.
        [Fact]
        public void PackCommand_WithProjectReferencesSymbols()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");

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
                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.symbols.nupkg"));
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
        }

        // Test that when creating a package from project file, a referenced project that
        // has a nuspec file is added as dependency.
        [Fact]
        public void PackCommand_ReferencedProjectWithNuspecFile()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange

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
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));
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
                        new PackageDependency("proj2", VersionUtility.ParseVersionSpec("1.0.0")),
                        new PackageDependency("proj6", VersionUtility.ParseVersionSpec("2.0.0"))
                    },
                    new PackageDepencyComparer());
            }
        }

        // Test that when creating a package from project file, a referenced project that
        // has a json file is added as dependency.
        [Fact]
        public void PackCommand_ReferencedProjectWithJsonFile()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange

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
                    "project.json",
                    @"{
  ""version"": ""1.0.0.0"",
  ""title"": ""Proj2"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""dependencies"": {
    ""p1"": {
      ""version"": ""1.5.11""
    }
  },
}");
                Util.CreateFile(
                                Path.Combine(workingDirectory, "proj6"),
                                "project.json",
                                @"{
  ""version"": ""2.0.0.0"",
  ""title"": ""Proj6"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""dependencies"": {
    ""p2"": {
      ""version"": ""1.5.11""
    }
  }
}");

                // Act
                var proj1Directory = Path.Combine(workingDirectory, "proj1");

                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.csproj -build -IncludeReferencedProjects",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));
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
                    new PackageDependency[]
                    {
                        new PackageDependency("proj2", VersionUtility.ParseVersionSpec("1.0.0")),
                        new PackageDependency("proj6", VersionUtility.ParseVersionSpec("2.0.0"))
                    },
                    dependencies,
                    new PackageDepencyComparer());
            }
        }

        // Same test as PackCommand_ReferencedProjectWithNuspecFile, but with -MSBuidVersion
        // set to 14
        [Fact]
        public void PackCommand_ReferencedProjectWithNuspecFileWithMsbuild14()
        {
            var nugetexe = Util.GetNuGetExePath();
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange

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
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));
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
                        new PackageDependency("proj2", VersionUtility.ParseVersionSpec("1.0.0")),
                        new PackageDependency("proj6", VersionUtility.ParseVersionSpec("2.0.0"))
                    },
                    new PackageDepencyComparer());
            }
        }

        // Same test as PackCommand_ReferencedProjectWithJsonFile, but with -MSBuidVersion
        // set to 14
        [Fact]
        public void PackCommand_ReferencedProjectWithJsonFileWithMsbuild14()
        {
            var nugetexe = Util.GetNuGetExePath();
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange

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
                    "project.json",
                    @"{
  ""version"": ""1.0.0.0"",
  ""title"": ""Proj2"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""dependencies"": {
    ""p1"": {
      ""version"": ""1.5.11""
    }
  },
}");
                Util.CreateFile(
                                Path.Combine(workingDirectory, "proj6"),
                                "project.json",
                                @"{
  ""version"": ""2.0.0.0"",
  ""title"": ""Proj6"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""dependencies"": {
    ""p2"": {
      ""version"": ""1.5.11""
    }
  }
}");

                // Act
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    @"pack proj1.csproj -build -IncludeReferencedProjects  -MSBuildVersion 14",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));
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
                        new PackageDependency("proj2", VersionUtility.ParseVersionSpec("1.0.0")),
                        new PackageDependency("proj6", VersionUtility.ParseVersionSpec("2.0.0"))
                    },
                    new PackageDepencyComparer());
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
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange

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
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));

                // proj2 and proj6 are added as dependencies.
                var dependencies = package.DependencySets.First().Dependencies.OrderBy(d => d.Id);
                Assert.Equal(
                    dependencies.OrderBy(d => d.ToString()),
                    new PackageDependency[]
                    {
                        new PackageDependency("proj2", VersionUtility.ParseVersionSpec("1.0.0")),
                        new PackageDependency(prefixTokenValue + "proj6", VersionUtility.ParseVersionSpec("2.0.0"))
                    }.OrderBy(d => d.ToString()),
                    new PackageDepencyComparer());
            }
        }

        // Test that recognized tokens such as $id$ in the nuspec file of the
        // referenced project are replaced.
        [Fact]
        public void PackCommand_NuspecFileWithTokens()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange

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
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));
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
                        new PackageDependency("proj2", VersionUtility.ParseVersionSpec("1.2.0"))
                    },
                    new PackageDepencyComparer());
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(@"\\")]
        [InlineData("\\.")]
        // [InlineData("\\")] This suffix is not expected to work see https://github.com/NuGet/home/issues/1817
        public void PackCommand_OutputDirectorySuffixes(string suffix)
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                CreateTestProject(workingDirectory, "proj1", new string[] { });

                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var outputDirectory = Path.Combine(workingDirectory, "path with spaces") + suffix;

                Directory.CreateDirectory(outputDirectory);

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    $"pack proj1.csproj -build -IncludeReferencedProjects -outputDirectory \"{outputDirectory}\"",
                    waitForExit: true);

                Assert.True(0 == r.Item1, r.Item2 + Environment.NewLine + r.Item3);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(outputDirectory, "proj1.0.0.0.nupkg"));

                var files = package.GetFiles().Select(f => f.Path).ToArray();

                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\net40\proj1.dll"
                    });
            }
        }

        // Test that option -IncludeReferencedProjects works correctly for the case
        // where the same project is referenced by multiple projects in the
        // reference hierarchy.
        [Fact]
        public void PackCommand_ProjectReferencedByMultipleProjects()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange

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
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));
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
        }

        // Test that when creating a package from project A, the output of a referenced project
        // will be added to the same target framework folder as A, regardless of the target
        // framework of the referenced project.
        [Fact]
        public void PackCommand_ReferencedProjectWithDifferentTarget()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange

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
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));
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
        }

        // Test that when -IncludeReferencedProjects is not specified,
        // pack command will not try to look for the output files of the
        // referenced projects.
        public void PackCommand_IncludeReferencedProjectsOff()
        {
            var msbuild = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"Microsoft.NET\Framework\v4.0.30319\msbuild.exe");
            if (!File.Exists(msbuild))
            {
                return;
            }

            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");

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
                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\net40\proj2.dll"
                    });
            }
        }

        // Test that when -IncludeReferencedProjects is specified, the properties
        // passed thru command line will be applied if a referenced project
        // needs to be built.
        [Fact]
        public void PackCommand_PropertiesAppliedToReferencedProjects()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");

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

                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.nupkg"));
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
        }

        // Test that exclude masks starting with '**' work also
        // for files outside of the package/project root.
        [Fact]
        public void PackCommand_ExcludesFilesOutsideRoot()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var projDirectory = Path.Combine(workingDirectory, "package");
                var otherDirectory = Path.Combine(workingDirectory, "other");

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
                var package = new OptimizedZipPackage(Path.Combine(projDirectory, "ExcludeBug.0.1.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"Content\package\include.me"
                    });
            }
        }

        // Test that NuGet packages of the project are added as dependencies
        [Theory]
        [InlineData("packages.config")]
        [InlineData("packages.proj1.config")]
        public void PackCommand_PackagesAddedAsDependencies(string packagesConfigFileName)
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var packagesFolder = Path.Combine(proj1Directory, "packages");

                Directory.CreateDirectory(packagesFolder);

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
                Util.CreateTestPackage("testPackage1", "1.1.0", Path.Combine(packagesFolder, "testPackage1.1.1.0"));
                Util.CreateTestPackage("testPackage2", "1.1.0", Path.Combine(packagesFolder, "testPackage2.1.1.0"));

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
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));
                Assert.Equal(1, package.DependencySets.Count());
                var dependencySet = package.DependencySets.First();

                // verify that only testPackage1 is added as dependency. testPackage2 is not adde
                // as dependency because its developmentDependency is true.
                Assert.Equal(1, dependencySet.Dependencies.Count);
                var dependency = dependencySet.Dependencies.First();
                Assert.Equal("testPackage1", dependency.Id);
                Assert.Equal("1.1.0", dependency.VersionSpec.ToString());
            }
        }

        [Theory]
        [InlineData("packages.config")]
        [InlineData("packages.proj1.config")]
        public void PackCommand_PackagesAddedAsDependenciesWithoutSln(string packagesConfigFileName)
        {
            // This tests building without a solution file because that had caused a null ref exception
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var packagesFolder = Path.Combine(proj1Directory, "packages");

                Directory.CreateDirectory(packagesFolder);

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
                Util.CreateTestPackage("testPackage1", "1.1.0", Path.Combine(packagesFolder, "testPackage1.1.1.0"));
                Util.CreateTestPackage("testPackage2", "1.1.0", Path.Combine(packagesFolder, "testPackage2.1.1.0"));

                Util.CreateFile(
                    proj1Directory,
                    "proj1_file2.txt",
                    "file2");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.csproj -build",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));
                Assert.Equal(1, package.DependencySets.Count());
                var dependencySet = package.DependencySets.First();

                // verify that only testPackage1 is added as dependency. testPackage2 is not adde
                // as dependency because its developmentDependency is true.
                Assert.Equal(1, dependencySet.Dependencies.Count);
                var dependency = dependencySet.Dependencies.First();
                Assert.Equal("testPackage1", dependency.Id);
                Assert.Equal("1.1.0", dependency.VersionSpec.ToString());
            }
        }

        // Test that NuGet packages of the project are added as dependencies 
        // even if there is already an indirect depenency, provided that the 
        // project requires a higher version number than the indirect dependency.
        [Theory]
        [InlineData("packages.config")]
        public void PackCommand_PackagesAddedAsDependenciesIfProjectRequiresHigerVersionNumber(string packagesConfigFileName)
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
 
            try
            {
                Directory.CreateDirectory(workingDirectory);
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var packagesFolder = Path.Combine(proj1Directory, "packages");
                Directory.CreateDirectory(proj1Directory);
                Directory.CreateDirectory(packagesFolder);
 
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
  <package id=""testPackage2"" version=""1.2.0"" targetFramework=""net45"" />
  <package id=""testPackage3"" version=""1.3.0"" targetFramework=""net45"" />
  <package id=""testPackage4"" version=""1.4.0"" targetFramework=""net45"" />
  <package id=""testPackage5"" version=""1.5.0"" targetFramework=""net45"" />
</packages>");
                var packageDependencies =
                    new List<PackageDependencyGroup>()
                    {
                        new PackageDependencyGroup(
                            new NuGetFramework("net45"),
                            new List<Packaging.Core.PackageDependency>()
                            {
                                new Packaging.Core.PackageDependency(
                                      "testPackage2",
                                      new VersionRange(new NuGetVersion("1.0.0"), includeMinVersion: true)),
                                new Packaging.Core.PackageDependency(
                                      "testPackage3",
                                      new VersionRange(new NuGetVersion("1.3.0"), includeMinVersion: true)),
                                new Packaging.Core.PackageDependency(
                                      "testPackage4"),
                                new Packaging.Core.PackageDependency(
                                      "testPackage5",
                                      new VersionRange(new NuGetVersion("1.5.0"), includeMinVersion: false))
                            })
                    };
                Util.CreateTestPackage("testPackage1", "1.1.0", packagesFolder, new List<NuGetFramework>() { new NuGetFramework("net45") }, packageDependencies );
                Util.CreateTestPackage("testPackage2", "1.2.0", packagesFolder);
                Util.CreateTestPackage("testPackage3", "1.3.0", packagesFolder);
                Util.CreateTestPackage("testPackage4", "1.4.0", packagesFolder);
                Util.CreateTestPackage("testPackage5", "1.5.0", packagesFolder);

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
                    "pack proj1.csproj -build -IncludeReferencedProjects",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);
 
                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));
                Assert.Equal(1, package.DependencySets.Count());
                var dependencySet = package.DependencySets.First();
 
                // Verify that testPackage2 is added as dependency in addition to testPackage1. 
                // testPackage3 and testPackage4 are not added because they are already referenced by testPackage1 with the correct version range.
                Assert.Equal(4, dependencySet.Dependencies.Count);
                var dependency1 = dependencySet.Dependencies.Single(d => d.Id == "testPackage1");
                Assert.Equal("1.1.0", dependency1.VersionSpec.ToString());
                var dependency2 = dependencySet.Dependencies.Single(d => d.Id == "testPackage2");
                Assert.Equal("1.2.0", dependency2.VersionSpec.ToString());
                var dependency4 = dependencySet.Dependencies.Single(d => d.Id == "testPackage4");
                Assert.Equal("1.4.0", dependency4.VersionSpec.ToString());
                var dependency5 = dependencySet.Dependencies.Single(d => d.Id == "testPackage5");
                Assert.Equal("1.5.0", dependency5.VersionSpec.ToString());
            }
            finally
            {
                Directory.Delete(workingDirectory, true);
            }
        }
 
        // Test that NuGet packages of the project are added as dependencies 
        // even if there is already an indirect depenency, provided that the 
        // project requires a higher version number than the indirect dependency.
        [Theory]
        [InlineData("packages.config")]
        public void PackCommand_PackagesAddedAsDependenciesIfProjectRequiresHigerVersionNumber_AndIndirectDependencyIsAlreadyListed(string packagesConfigFileName)
        {
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
 
            try
            {
                Directory.CreateDirectory(workingDirectory);
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var packagesFolder = Path.Combine(proj1Directory, "packages");
                Directory.CreateDirectory(proj1Directory);
                Directory.CreateDirectory(packagesFolder);
 
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
  <package id=""testPackage2"" version=""1.2.0"" targetFramework=""net45"" />
  <package id=""testPackage3"" version=""1.3.0"" targetFramework=""net45"" />
</packages>");
                Util.CreateTestPackage("testPackage1", "1.1.0", packagesFolder);
                Util.CreateTestPackage("testPackage2", "1.2.0", packagesFolder);
                var packageDependencies =
                    new List<PackageDependencyGroup>()
                    {
                        new PackageDependencyGroup(
                            new NuGetFramework("net45"),
                            new List<Packaging.Core.PackageDependency>()
                            {
                                new Packaging.Core.PackageDependency(
                                      "testPackage1",
                                      new VersionRange(new NuGetVersion("1.0.0"), includeMinVersion: true)),
                                new Packaging.Core.PackageDependency(
                                      "testPackage2",
                                      new VersionRange(new NuGetVersion("1.2.0"), includeMinVersion: true))
                            })
                    };
                Util.CreateTestPackage("testPackage3", "1.3.0", packagesFolder, new List<NuGetFramework>() { new NuGetFramework("net45") }, packageDependencies);

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
                    "pack proj1.csproj -build -IncludeReferencedProjects",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);
 
                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0.nupkg"));
                Assert.Equal(1, package.DependencySets.Count());
                var dependencySet = package.DependencySets.First();
 
                // Verify that testPackage1 is added as dependency in addition to testPackage3. 
                // testPackage2 is not added because it is already referenced by testPackage3 with the correct version range.
                Assert.Equal(2, dependencySet.Dependencies.Count);                
                var dependency1 = dependencySet.Dependencies.Single(d => d.Id == "testPackage1");
                Assert.Equal("1.1.0", dependency1.VersionSpec.ToString());
                var dependency2 = dependencySet.Dependencies.Single(d => d.Id == "testPackage3");
                Assert.Equal("1.3.0", dependency2.VersionSpec.ToString());
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
            var msbuild = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"Microsoft.NET\Framework\v4.0.30319\msbuild.exe");
            if (!File.Exists(msbuild))
            {
                return;
            }

            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");

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
    <authors>author</authors>
    <owners>author</owners>
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
                Assert.Contains("Solution: Specify the version of dependency and rebuild your package.", r.Item2);
            }
        }

        // Test that nuget displays warnings when dependency version is not specified
        // in nuspec.
        [Fact]
        public void PackCommand_WarningDependencyVersionNotSpecifiedInJson()
        {
            var msbuild = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"Microsoft.NET\Framework\v4.0.30319\msbuild.exe");
            if (!File.Exists(msbuild))
            {
                return;
            }

            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");

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
                    "project.json",
                    @"{
  ""version"": ""1.0.0"",
  ""authors"": [ ""author"" ],
  ""owners"": [ ""author"" ],
  ""requireLicenseAcceptance"": ""false"",
  ""description"": ""description"",
  ""copyright"": ""Copyright 2013"",
  ""dependencies"": {
    ""json"": { }
  }
}");
                var r = CommandRunner.Run(
                    msbuild,
                    proj1Directory,
                    "proj1.csproj /p:Config=Release",
                    waitForExit: true);

                // Act
                r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack project.json",
                    waitForExit: true);
                Assert.Equal(1, r.Item1);

                // Assert
                Assert.Contains("Package dependencies must specify a version range.", r.Item3);
            }
        }

        // Tests that with -MSBuildVersion set to 14, a projec using C# 6.0 features (nameof in this test)
        // can be built successfully.
        [Fact]
        public void PackCommand_WithMsBuild14()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())

            {

                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");

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

                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.nupkg"));
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
        }

        // Tests that pack works with -MSBuildVersion set to 12
        [Fact]
        public void PackCommand_WithMsBuild12()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");

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

                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.nupkg"));
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
        }

        [Fact]
        public void PackCommand_WithMsBuildPath()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");

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
                var msbuildPath = @"C:\Program Files (x86)\MSBuild\14.0\Bin";
                var os = Environment.GetEnvironmentVariable("OSTYPE");
                if (RuntimeEnvironmentHelper.IsMono && os != null && os.StartsWith("darwin"))
                {
                    msbuildPath = @"/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/msbuild/14.1/bin/";
                }

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj2Directory,
                    $@"pack proj2.csproj -build -IncludeReferencedProjects -p Config=Release -MSBuildPath ""{msbuildPath}"" ",
                    waitForExit: true);
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert

                // Verify that proj1 was not built using the default config "Debug".
                Assert.False(Directory.Exists(Path.Combine(proj1Directory, "debug_out")));
                Assert.True(r.Item2.Contains($"Using Msbuild from '{msbuildPath}'."));

                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);
                Assert.Equal(
                    files,
                    new string[]
                    {
                        Path.Combine("content", "proj1_file2.txt"),
                        Path.Combine("lib", "net40", "proj1.dll"),
                        Path.Combine("lib", "net40", "proj2.dll")
                    });
            }
        }

        [Fact]
        public void PackCommand_WithMsBuildPathAndMsbuildVersion()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");

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
                var msbuildPath = @"C:\Program Files (x86)\MSBuild\14.0\Bin";
                var os = Environment.GetEnvironmentVariable("OSTYPE");
                if (RuntimeEnvironmentHelper.IsMono && os != null && os.StartsWith("darwin"))
                {
                    msbuildPath = @"/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/msbuild/14.1/bin/";
                }

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj2Directory,
                    $@"pack proj2.csproj -build -IncludeReferencedProjects -p Config=Release -MSBuildPath ""{msbuildPath}"" -MSBuildVersion 12 ",
                    waitForExit: true);
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert

                // Verify that proj1 was not built using the default config "Debug".
                Assert.False(Directory.Exists(Path.Combine(proj1Directory, "debug_out")));
                Assert.True(r.Item2.Contains($"Using Msbuild from '{msbuildPath}'."));
                Assert.True(r.Item2.Contains($"MsbuildPath : {msbuildPath} is using, ignore MsBuildVersion: 12."));

                var package = new OptimizedZipPackage(Path.Combine(proj2Directory, "proj2.0.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);
                Assert.Equal(
                    files,
                    new string[]
                    {
                        Path.Combine("content", "proj1_file2.txt"),
                        Path.Combine("lib", "net40", "proj1.dll"),
                        Path.Combine("lib", "net40", "proj2.dll")
                    });
            }
        }

        [Fact]
        public void PackCommand_WithNonExistMsBuildPath()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var proj2Directory = Path.Combine(workingDirectory, "proj2");

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
                var msbuildPath = @"\\not exist path";

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    proj2Directory,
                    $@"pack proj2.csproj -build -IncludeReferencedProjects -p Config=Release -MSBuildPath ""{msbuildPath}"" ",
                    waitForExit: true);
                Assert.True(1 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert

                Assert.True(r.Item3.Contains($"MSBuildPath : {msbuildPath}  doesn't not exist."));
            }
        }

        [Fact]
        public void PackCommand_VersionSuffixIsAssigned()
        {
            var nugetexe = Util.GetNuGetExePath();
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                CreateTestProject(workingDirectory, "proj1", null);

                // Act
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack proj1.csproj -Build -Suffix alpha",
                    waitForExit: true);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.0.0.0-alpha.nupkg"));
                Assert.Equal(package.Version.ToString(), "0.0.0-alpha");
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
            Directory.CreateDirectory(projectDirectory);

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

        [Fact]
        public void PackCommand_FrameworkAssemblies()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles/any/any"),
                    "image.jpg",
                    "");

                Directory.CreateDirectory(
                    Path.Combine(workingDirectory, "bin/Debug"));

                Util.CreateFile(
                    workingDirectory,
                    Path.GetFileName(workingDirectory) + ".project.json",
                @"{
  ""version"": ""1.0.0"",
  ""title"": ""packageA"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""frameworks"": {
    ""net46"": {
      ""frameworkAssemblies"": {
        ""System.Xml"": """",
        ""System.Xml.Linq"": """"
      }
    }
  },
  ""packInclude"": {
    ""image"": ""contentFiles/any/any/image.jpg""
  },
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
  }
}
");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack " + Path.GetFileName(workingDirectory) + ".project.json",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                var id = Path.GetFileName(workingDirectory);

                // Assert
                var path = Path.Combine(workingDirectory, id + ".1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                using (var zip = new ZipArchive(File.OpenRead(path)))
                {
                    var manifestReader
                        = new StreamReader(zip.Entries.Single(file => file.FullName == id + ".nuspec").Open());
                    var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());

                    var node = nuspecXml.Descendants().Single(e => e.Name.LocalName == "dependencies");
                    var actual = node.ToString().Replace("\r\n", "\n");

                    Assert.Equal(
                        @"<dependencies xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <group targetFramework="".NETFramework4.6"" />
</dependencies>".Replace("\r\n", "\n"), actual);

                    node = nuspecXml.Descendants().Single(e => e.Name.LocalName == "frameworkAssemblies");
                    actual = node.ToString().Replace("\r\n", "\n");

                    Assert.Equal(
                        @"<frameworkAssemblies xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <frameworkAssembly assemblyName=""System.Xml"" targetFramework="".NETFramework4.6"" />
  <frameworkAssembly assemblyName=""System.Xml.Linq"" targetFramework="".NETFramework4.6"" />
</frameworkAssemblies>".Replace("\r\n", "\n"), actual);
                }
            }
        }

        [Fact]
        public void PackCommand_PackageFromNuspecWithXmlEncoding()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    workingDirectory,
                    "packageA.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>packageA</id>
    <version>1.0.0</version>
    <title>packageA&lt;T&gt;</title>
    <authors>test &lt;test@microsoft.com&gt;</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description &lt;with&gt; &lt;&lt;bad
stuff \n &lt;&lt;
</description>
    <copyright>Copyright © &lt;T&gt; 2013</copyright>
    <frameworkAssemblies>
      <frameworkAssembly assemblyName=""System"" />
    </frameworkAssemblies>
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

                    // First test the nuspec to make sure the XML is encoded correctly
                    // Getting the value decodes the value so they will be unencoded here
                    // If it weren't encoded properly, this would fail to parse or have different text
                    var title = nuspecXml.Descendants().Single(e => e.Name.LocalName == "title");
                    Assert.Equal("packageA<T>", title.Value);

                    var authors = nuspecXml.Descendants().Single(e => e.Name.LocalName == "authors");
                    Assert.Equal("test <test@microsoft.com>", authors.Value);

                    var description = nuspecXml.Descendants().Single(e => e.Name.LocalName == "description");

                    var expectedDescription = @"Description <with> <<bad
stuff \n <<".Replace("\r\n", "\n");
                    var actualDescription = description.Value.Replace("\r\n", "\n");
                    Assert.Equal(expectedDescription, actualDescription);

                    var copyright = nuspecXml.Descendants().Single(e => e.Name.LocalName == "copyright");
                    Assert.Equal("Copyright © <T> 2013", copyright.Value);

                    // Now test the description in the psmdcp file
                    var packageReader
                        = new StreamReader(zip.Entries.Single(file => file.FullName.EndsWith(".psmdcp")).Open());
                    var packageXml = XDocument.Parse(packageReader.ReadToEnd());

                    description = packageXml.Descendants().Single(e => e.Name.LocalName == "description");
                    actualDescription = description.Value.Replace("\r\n", "\n");
                    Assert.Equal(expectedDescription, actualDescription);
                }
            }
        }

        [Fact]
        public void PackCommand_JsonSnapshotValue()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles/any/any"),
                    "image.jpg",
                    "");

                Directory.CreateDirectory(
                    Path.Combine(workingDirectory, "bin/Debug"));

                Util.CreateFile(
                    workingDirectory,
                    Path.GetFileName(workingDirectory) + ".project.json",
                @"{
  ""version"": ""1.0.0-*"",
  ""title"": ""packageA"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""dependencies"": {
    ""packageB"": {
      ""version"": ""1.0.0"",
    },
  },
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack " + Path.GetFileName(workingDirectory) + ".project.json -Suffix rc-123",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                var id = Path.GetFileName(workingDirectory);

                // Assert
                var path = Path.Combine(workingDirectory, id + ".1.0.0-rc-123.nupkg");
                var package = new OptimizedZipPackage(path);
                using (var zip = new ZipArchive(File.OpenRead(path)))
                {
                    var manifestReader
                        = new StreamReader(zip.Entries.Single(file => file.FullName == id + ".nuspec").Open());
                    var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());

                    var node = nuspecXml.Descendants().Single(e => e.Name.LocalName == "version");

                    Assert.Equal("1.0.0-rc-123", node.Value);
                }
            }
        }

        [Fact]
        public void PackCommand_JsonPackOptionsFiles()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "image1.jpg",
                    "");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "image2.jpg",
                    "");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "image3.jpg",
                    "");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "file1.txt",
                    "");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "file2.txt",
                    "");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "file3.txt",
                    "");

                Directory.CreateDirectory(
                    Path.Combine(workingDirectory, "bin/Debug"));

                Util.CreateFile(
                    workingDirectory,
                    Path.GetFileName(workingDirectory) + ".project.json",
                @"{
  ""version"": ""1.0.0-*"",
  ""title"": ""packageA"",
  ""authors"": [ ""test"" ],
  ""owners"": [ ""test"" ],
  ""requireLicenseAcceptance"": ""false"",
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""files"": {
      ""include"": ""contentFiles/**"",
      ""exclude"": ""contentFiles/**.txt"",
      ""includeFiles"": [ ""contentFiles/file2.txt"" ],
      ""excludeFiles"": [ ""contentFiles/image3.jpg"" ],
    }
  }
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack " + Path.GetFileName(workingDirectory) + ".project.json",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                var id = Path.GetFileName(workingDirectory);

                // Assert
                var path = Path.Combine(workingDirectory, id + ".1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                using (var zip = new ZipArchive(File.OpenRead(path)))
                {
                    var manifestReader
                        = new StreamReader(zip.Entries.Single(file => file.FullName == id + ".nuspec").Open());
                    var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());

                    var files = package.GetFiles().Select(f => f.Path).OrderBy(s => s).ToArray();

                    Assert.Equal(
                        new string[]
                        {
                            @"contentFiles\file2.txt",
                            @"contentFiles\image1.jpg",
                            @"contentFiles\image2.jpg",
                        },
                        files);
                }
            }
        }

        [Fact]
        public void PackCommand_JsonPackOptionsFilesMappings()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "image1.jpg",
                    "");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "image2.jpg",
                    "");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "image3.jpg",
                    "");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "file1.txt",
                    "");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "file2.txt",
                    "");
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles"),
                    "file3.txt",
                    "");

                Directory.CreateDirectory(
                    Path.Combine(workingDirectory, "bin/Debug"));

                Util.CreateFile(
                    workingDirectory,
                    Path.GetFileName(workingDirectory) + ".project.json",
                @"{
  ""version"": ""1.0.0-*"",
  ""title"": ""packageA"",
  ""authors"": [ ""test"" ],
  ""owners"": [ ""test"" ],
  ""requireLicenseAcceptance"": ""false"",
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""files"": {
      ""mappings"": {
          ""map1"" : {
            ""include"": ""contentFiles/**"",
            ""exclude"": ""contentFiles/**.txt""
          },
          ""map2"": {
            ""includeFiles"": [ ""contentFiles/file2.txt"", ""contentFiles/file3.txt"" ],
            ""excludeFiles"": [ ""contentFiles/file3.txt"" ]
          }
        }
      }
    }
  }
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack " + Path.GetFileName(workingDirectory) + ".project.json",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                var id = Path.GetFileName(workingDirectory);

                // Assert
                var path = Path.Combine(workingDirectory, id + ".1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                using (var zip = new ZipArchive(File.OpenRead(path)))
                {
                    var manifestReader
                        = new StreamReader(zip.Entries.Single(file => file.FullName == id + ".nuspec").Open());
                    var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());

                    var files = package.GetFiles().Select(f => f.Path).OrderBy(s => s).ToArray();

                    Assert.Equal(
                        new string[]
                        {
                            @"map1\image1.jpg",
                            @"map1\image2.jpg",
                            @"map1\image3.jpg",
                            @"map2\file2.txt",
                        },
                        files);
                }
            }
        }

        [Fact]
        public void PackCommand_JsonSnapshotRcValue()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    Path.Combine(workingDirectory, "contentFiles/any/any"),
                    "image.jpg",
                    "");

                Directory.CreateDirectory(
                    Path.Combine(workingDirectory, "bin/Debug"));

                Util.CreateFile(
                    workingDirectory,
                    Path.GetFileName(workingDirectory) + ".project.json",
                @"{
  ""version"": ""1.0.0-rc-*"",
  ""title"": ""packageA"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""dependencies"": {
    ""packageB"": {
      ""version"": ""1.0.0"",
    },
  },
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack " + Path.GetFileName(workingDirectory) + ".project.json -Suffix 123",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                var id = Path.GetFileName(workingDirectory);

                // Assert
                var path = Path.Combine(workingDirectory, id + ".1.0.0-rc-123.nupkg");
                var package = new OptimizedZipPackage(path);
                using (var zip = new ZipArchive(File.OpenRead(path)))
                {
                    var manifestReader
                        = new StreamReader(zip.Entries.Single(file => file.FullName == id + ".nuspec").Open());
                    var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());

                    var node = nuspecXml.Descendants().Single(e => e.Name.LocalName == "version");

                    Assert.Equal("1.0.0-rc-123", node.Value);
                }
            }
        }

        [Fact]
        public void PackCommand_PackJsonCorrectLibPathInNupkg()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange

                string id = Path.GetFileName(workingDirectory);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "someDirName", id, "bin/Debug/netcoreapp1.0"),
                    id + ".dll",
                    string.Empty);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "someDirName", id, "bin/Debug/netcoreapp1.0/win7-x64"),
                    id + ".dll",
                    string.Empty);

                Util.CreateFile(
                    workingDirectory,
                    "project.json",
                @"{
  ""version"": ""1.0.0"",
  ""title"": ""packageA"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""frameworks"": {
    ""native"": {
    },
    ""uap10.0"": {
    }
  }
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack project.json",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var path = Path.Combine(workingDirectory, Path.GetFileName(workingDirectory) + ".1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                var files = package.GetFiles().Select(f => f.Path).OrderBy(s => s).ToArray();

                Assert.Equal(
                    files,
                    new string[]
                    {
                            @"lib\netcoreapp1.0\" + id + ".dll",
                            @"lib\netcoreapp1.0\win7-x64\" + id + ".dll",
                    });

                Assert.False(r.Item2.Contains("Assembly outside lib folder"));
            }
        }

        // Test that a missing dependency causes a failure
        [Fact]
        public void PackCommand_MissingPackageCausesError()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var proj1Directory = Path.Combine(workingDirectory, "proj1");
                var packagesFolder = Path.Combine(proj1Directory, "packages");

                Directory.CreateDirectory(packagesFolder);

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
  <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
</Project>");

                Util.CreateFile(
                    proj1Directory,
                    "packages.config",
@"<?xml version=""1.0"" encoding=""utf-8""?>
<packages>
  <package id=""testPackage1"" version=""1.1.0"" targetFramework=""net45"" />
  <package id=""doesNotExist"" version=""1.1.0"" targetFramework=""net45"" />
</packages>");
                Util.CreateTestPackage("testPackage1", "1.1.0", Path.Combine(packagesFolder, "testPackage1.1.1.0"));

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
                Assert.Equal(1, r.Item1);

                // Assert
                Assert.Contains("Unable to find 'doesNotExist.1.1.0.nupkg'.", r.Item3);
            }
        }

        [Theory]
        [InlineData(".dll")]
        [InlineData(".exe")]
        public void PackCommand_PackJsonCorrectLibPathInNupkgWithOutputName(string extension)
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var testFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var id = "packageId";
                var workingDirectory = Path.Combine(testFolder, id);
                string dllName = "myDllName";

                Directory.CreateDirectory(id);
                Util.CreateFile(
                    Path.Combine(workingDirectory, "someDirName", id, "bin/Debug/netcoreapp1.0"),
                    dllName + extension,
                    string.Empty);

                Util.CreateFile(
                    Path.Combine(workingDirectory, "someDirName", id, "bin/Debug/netcoreapp1.0/win7-x64"),
                    dllName + extension,
                    string.Empty);

                Util.CreateFile(
                    workingDirectory,
                    "project.json",
                @"{
  ""version"": ""1.0.0"",
  ""title"": ""packageA"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""buildOptions"": {
    ""outputName"": """ + dllName + @""",
  },
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""copyright"": ""Copyright ©  2013"",
  ""frameworks"": {
    ""native"": {
    },
    ""uap10.0"": {
    }
  }
}");

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack project.json",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var path = Path.Combine(workingDirectory, Path.GetFileName(workingDirectory) + ".1.0.0.nupkg");
                var package = new OptimizedZipPackage(path);
                var files = package.GetFiles().Select(f => f.Path).OrderBy(s => s).ToArray();

                Assert.Equal(
                    files,
                    new string[]
                    {
                            @"lib\netcoreapp1.0\" + dllName + extension,
                            @"lib\netcoreapp1.0\win7-x64\" + dllName + extension,
                    });

                Assert.False(r.Item2.Contains("Assembly outside lib folder"));
            }
        }

        [Fact]
        public void PackCommand_BuildBareMinimumProjectJson()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var proj1Directory = Path.Combine(workingDirectory, "proj1");

                CreateTestProject(workingDirectory, "proj1",
                    new string[] {
                    });
                Util.CreateFile(
                proj1Directory,
                "project.json",
            @"{
  ""version"": ""1.0.0"",
  ""title"": ""proj1"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""frameworks"": {
    ""net46"": {
      ""frameworkAssemblies"": {
        ""System"": """",
        ""System.Runtime"": """"
      }
    }
  }
}");

                // Act
                CommandRunner.Run(
                    NuGetEnvironment.GetDotNetLocation(),
                    proj1Directory,
                    "restore",
                    waitForExit: true);
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack project.json -build",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
            }
        }

        [Fact]
        public void PackCommand_BuildProjectJson()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var proj1Directory = Path.Combine(workingDirectory, "proj1");

                CreateTestProject(workingDirectory, "proj1",
                    new string[] {
                    });
                Util.CreateFile(
                proj1Directory,
                "project.json",
            @"{
  ""version"": ""1.0.0"",
  ""title"": ""proj1"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""frameworks"": {
    ""net46"": {
      ""frameworkAssemblies"": {
        ""System"": """",
        ""System.Runtime"": """"
      }
    }
  }
}");

                // Act
                CommandRunner.Run(
                    NuGetEnvironment.GetDotNetLocation(),
                    proj1Directory,
                    "restore",
                    waitForExit: true);
                var r = CommandRunner.Run(
                    nugetexe,
                    proj1Directory,
                    "pack project.json -build",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "proj1.1.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\net46\proj1.dll",
                    });
            }
        }

        [Fact]
        public void PackCommand_BuildProjectJsonWithFullBasePath()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var proj1Directory = Path.Combine(workingDirectory, "proj1");

                CreateTestProject(workingDirectory, "proj1",
                    new string[] {
                    });
                Util.CreateFile(
                proj1Directory,
                "project.json",
            @"{
  ""version"": ""1.0.0"",
  ""title"": ""proj1"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""frameworks"": {
    ""net46"": {
      ""frameworkAssemblies"": {
        ""System"": """",
        ""System.Runtime"": """"
      }
    }
  }
}");

                // Act
                CommandRunner.Run(
                    NuGetEnvironment.GetDotNetLocation(),
                    proj1Directory,
                    "restore",
                    waitForExit: true);
                var r = CommandRunner.Run(
                    nugetexe,
                    "C:\\",
                    $"pack {proj1Directory}\\project.json -build -basepath {proj1Directory}\\buildDir -outputdir {proj1Directory}\\outDir",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "outDir", "proj1.1.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\net46\proj1.dll",
                    });
            }
        }

        [Fact]
        public void PackCommand_BuildProjectJsonWithRelativeBasePath()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var proj1Directory = Path.Combine(workingDirectory, "proj1");

                CreateTestProject(workingDirectory, "proj1",
                    new string[] {
                    });
                Util.CreateFile(
                proj1Directory,
                "project.json",
            @"{
  ""version"": ""1.0.0"",
  ""title"": ""proj1"",
  ""authors"": [ ""test"" ],
  ""description"": ""Description"",
  ""copyright"": ""Copyright ©  2013"",
  ""packOptions"": {
    ""owners"": [ ""test"" ],
    ""requireLicenseAcceptance"": ""false""
    },
  ""frameworks"": {
    ""net46"": {
      ""frameworkAssemblies"": {
        ""System"": """",
        ""System.Runtime"": """"
      }
    }
  }
}");

                // Act
                CommandRunner.Run(
                    NuGetEnvironment.GetDotNetLocation(),
                    proj1Directory,
                    "restore",
                    waitForExit: true);
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    $"pack proj1\\project.json -build -basepath buildDir -outputdir proj1\\outDir",
                    waitForExit: true);
                Assert.Equal(0, r.Item1);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(proj1Directory, "outDir", "proj1.1.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();
                Array.Sort(files);

                Assert.Equal(
                    files,
                    new string[]
                    {
                        @"lib\net46\proj1.dll",
                    });
            }
        }

        [Fact]
        public void PackCommand_SemVer200()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                Util.CreateFile(
                    Path.Combine(workingDirectory, "lib/netstandard1.6/"),
                    "a.dll",
                    "");

                Util.CreateFile(
                    workingDirectory,
                    "packageA.nuspec",
@"<package xmlns='http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd'>
  <metadata>
    <id>packageA</id>
    <version>1.0.0-beta.1.build.234+git.hash.6f3ae2d59140f5ea97eb7573535de1c286d6d336</version>
    <title>packageA</title>
    <authors>test</authors>
    <owners>test</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <copyright>Copyright ©  2013</copyright>
    <dependencies>
        <group targetFramework=""netstandard1.6"">
            <dependency id=""packageB"" version=""1.0.0-beta.1.build.234+git.hash.6f3ae2d59140f5ea97eb7573535de1c286d6d336"" />
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
                var path = Path.Combine(workingDirectory, "packageA.1.0.0-beta.1.build.234.nupkg");

                using (var reader = new PackageArchiveReader(path))
                {
                    var version = reader.NuspecReader.GetVersion();
                    var dependency = reader.NuspecReader.GetDependencyGroups().Single().Packages.Single();

                    Assert.Equal("1.0.0-beta.1.build.234+git.hash.6f3ae2d59140f5ea97eb7573535de1c286d6d336", version.ToFullString());
                    Assert.Equal("1.0.0-beta.1.build.234", version.ToString());
                    Assert.Equal("1.0.0-beta.1.build.234", dependency.VersionRange.ToLegacyShortString());
                }
            }
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