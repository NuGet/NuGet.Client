using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using Moq;
using NuGet.CommandLine.Test;
using Xunit;

namespace NuGet.CommandLine
{
    public class ProjectFactoryTest
    {
        [Fact]
        public void ProjectFactoryInitializesPropertiesForPreprocessor()
        {
            // arrange
            const string inputSpec = @"<?xml version=""1.0""?>
<package>
    <metadata>
        <id>$id$</id>
        <version>$version$</version>
        <description>$description$</description>
        <authors>$author$</authors>
        <copyright>$copyright$</copyright>
        <licenseUrl>http://nuget.codeplex.com/license</licenseUrl>
        <projectUrl>http://nuget.codeplex.com</projectUrl>
        <tags>nuget</tags>
    </metadata>
</package>";
            var metadata = new ManifestMetadata
            {
                Id = "ProjectFactoryTest",
                Version = "2.0.30619.9000",
                Title = "NuGet.Test",
                Description = "",
                Copyright = "\x00a9 Outercurve. All rights reserved.",
                Authors = "Outercurve Foundation",
            };
            var projectMock = new Mock<Project>();
            var msbuildDirectory = NuGet.CommandLine.MsBuildUtility.GetMsbuildDirectory("4.0", console: null);
            var factory = new ProjectFactory(msbuildDirectory, projectMock.Object);

            // act
            var author = factory.InitializeProperties(metadata);
            var actual = Preprocessor.Process(inputSpec.AsStream(), factory, false);

            // assert
            Assert.Equal("Outercurve Foundation", author);
            const string expected = @"<?xml version=""1.0""?>
<package>
    <metadata>
        <id>ProjectFactoryTest</id>
        <version>2.0.30619.9000</version>
        <description></description>
        <authors>Outercurve Foundation</authors>
        <copyright>© Outercurve. All rights reserved.</copyright>
        <licenseUrl>http://nuget.codeplex.com/license</licenseUrl>
        <projectUrl>http://nuget.codeplex.com</projectUrl>
        <tags>nuget</tags>
    </metadata>
</package>";
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ProjectFactoryCanCompareContentsOfReadOnlyFile()
        {
            var us = Assembly.GetExecutingAssembly();
            var sourcePath = us.Location;
            var targetFile = new PhysicalPackageFile { SourcePath = sourcePath };
            var fullPath = sourcePath + "readOnly";
            File.Copy(sourcePath, fullPath);
            File.SetAttributes(fullPath, FileAttributes.ReadOnly);
            try
            {
                var actual = ProjectFactory.ContentEquals(targetFile, fullPath);

                Assert.Equal(true, actual);
            }
            finally
            {
                File.SetAttributes(fullPath, FileAttributes.Normal);
                File.Delete(fullPath);
            }
        }

        /// <summary>
        /// This test ensures that when building a nuget package from a project file (e.g. .csproj)
        /// that if the case doesn't match between a file in the .nuspec file and the file on disk
        /// that NuGet won't attempt to add it again.
        /// </summary>
        /// <example>
        /// Given: The .nuspec file contains &quot;Assembly.xml&quot; and the file on disk is &quot;Assembly.XML.&quot;
        /// Command: nuget pack Assembly.csproj
        /// Output: Exception: An item with the key already exists
        /// </example>
        [Fact]
        public void EnsureProjectFactoryDoesNotAddFileThatIsAlreadyInPackage()
        {
            // Setup
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                // Arrange
                Directory.CreateDirectory(workingDirectory);
                File.WriteAllText(Path.Combine(workingDirectory, "Assembly.nuspec"), GetNuspecContent());
                File.WriteAllText(Path.Combine(workingDirectory, "Assembly.csproj"), GetProjectContent());
                File.WriteAllText(Path.Combine(workingDirectory, "Source.cs"), GetSourceFileContent());
                var projPath = Path.Combine(workingDirectory, "Assembly.csproj");

                // Act 
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack Assembly.csproj -build",
                    waitForExit: true);
                var package = new OptimizedZipPackage(Path.Combine(workingDirectory, "Assembly.1.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();

                // Assert
                Assert.Equal(0, r.Item1);
                Array.Sort(files);
                Assert.Equal(files, new[] {
                    @"lib\net45\Assembly.dll",
                    @"lib\net45\Assembly.xml"
                });
            }
            finally
            {
                // Teardown
                try
                {
                    Directory.Delete(workingDirectory, true);
                }
                catch
                {

                }
            }
        }

        [Fact]
        public void EnsureProjectFactoryWorksAsExpectedWithReferenceOutputAssemblyValuesBasic()
        {
            // Setup
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                // Setup the projects
                Directory.CreateDirectory(workingDirectory);
                DummyProject link = new DummyProject("Link", Path.Combine(workingDirectory, "Link\\Link.csproj"));
                DummyProject a = new DummyProject("A", Path.Combine(workingDirectory, "A\\A.csproj"));
                DummyProject b = new DummyProject("B", Path.Combine(workingDirectory, "B\\B.csproj"));
                link.AddProjectReference(a, false);
                link.AddProjectReference(b, true);
                link.WriteToFile();
                a.WriteToFile();
                b.WriteToFile();

                // Act 
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack Link\\Link.csproj -build -IncludeReferencedProjects -Version 1.0.0",
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(r);

                var package = new OptimizedZipPackage(Path.Combine(workingDirectory, "Link.1.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();

                Assert.Equal(0, r.Item1);
                Array.Sort(files);
                Assert.Equal(files, new[] {
                    @"lib\net45\A.dll",
                    @"lib\net45\Link.dll"
                });
            }
            finally
            {
                // Teardown
                try
                {
                    Directory.Delete(workingDirectory, true);
                }
                catch
                {

                }
            }
        }

        [Fact]
        public void EnsureProjectFactoryWorksAsExpectedWithReferenceOutputAssemblyValuesComplex()
        {
            // Setup
            var nugetexe = Util.GetNuGetExePath();
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                // Setup the projects
                Directory.CreateDirectory(workingDirectory);
                DummyProject link = new DummyProject("Link", Path.Combine(workingDirectory, "Link\\Link.csproj"));
                DummyProject a = new DummyProject("A", Path.Combine(workingDirectory, "A\\A.csproj"));
                DummyProject b = new DummyProject("B", Path.Combine(workingDirectory, "B\\B.csproj"));
                DummyProject c = new DummyProject("C", Path.Combine(workingDirectory, "C\\C.csproj"));
                DummyProject d = new DummyProject("D", Path.Combine(workingDirectory, "D\\D.csproj"));
                DummyProject e = new DummyProject("E", Path.Combine(workingDirectory, "E\\E.csproj"));
                link.AddProjectReference(a, false);
                link.AddProjectReference(b, true);
                a.AddProjectReference(c, false);
                c.AddProjectReference(d, true);
                b.AddProjectReference(e, false);
                link.WriteToFile();
                a.WriteToFile();
                b.WriteToFile();
                c.WriteToFile();
                d.WriteToFile();
                e.WriteToFile();

                // Act 
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack Link\\Link.csproj -build -IncludeReferencedProjects -Version 1.0.0",
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(r);
                var package = new OptimizedZipPackage(Path.Combine(workingDirectory, "Link.1.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();

                Assert.Equal(0, r.Item1);
                Array.Sort(files);
                Assert.Equal(files, new[] {
                    @"lib\net45\A.dll",
                    @"lib\net45\C.dll",
                    @"lib\net45\Link.dll"
                });
            }
            finally
            {
                // Teardown
                try
                {
                    Directory.Delete(workingDirectory, true);
                }
                catch
                {

                }
            }
        }

        private static string GetNuspecContent()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
    <metadata>
        <id>Assembly</id>
        <version>1.0.0</version>
        <title />
        <authors>Author</authors>
        <owners />
        <requireLicenseAcceptance>false</requireLicenseAcceptance>
        <description>Description for Assembly.</description>
    </metadata>
    <files>
        <file src=""bin\Debug\Assembly.dll"" target=""lib\net45\Assembly.dll"" />
        <file src=""bin\Debug\Assembly.xml"" target=""lib\net45\Assembly.xml"" />
    </files>
</package>";
        }

        private static string GetProjectContent()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""4.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProjectGuid>{CD08AD03-0CBF-47B1-8A95-D9E9C2330F50}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>Assembly</RootNamespace>
    <AssemblyName>Assembly</AssemblyName>
    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DocumentationFile>bin\Debug\Assembly.XML</DocumentationFile>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""System"" />
    <Reference Include=""System.Core"" />
    <Reference Include=""System.Xml.Linq"" />
    <Reference Include=""System.Data.DataSetExtensions"" />
    <Reference Include=""Microsoft.CSharp"" />
    <Reference Include=""System.Data"" />
    <Reference Include=""System.Xml"" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""Source.cs"" />
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
</Project>";
        }

        private static string GetSourceFileContent()
        {
            return @"using System;

namespace Assembly
{
    /// <summary>Source</summary>
    public class Source
    {
        // Does nothing
    }
}";
        }

        #region Helper
        private class DummyProject
        {
            private static XNamespace MSBuildNS = "http://schemas.microsoft.com/developer/msbuild/2003";
            public DummyProject(string name, string path)
            {
                Id = Guid.NewGuid();
                Location = path;
                Name = name;
                ProjectReferences = new List<XElement>();
            }

            public string Name { get; }
            public Guid Id { get; }
            public string Location { get; }
            private List<XElement> ProjectReferences { get; }

            public void AddProjectReference(DummyProject project, bool exclude)
            {
                var projectReferenceElement = GenerateProjectReference(project, exclude);
                ProjectReferences.Add(projectReferenceElement);
            }

            private static XElement GenerateProjectReference(DummyProject project, bool exclude)
            {
                var projectReferenceXElement = new XElement(MSBuildNS + "ProjectReference",
                    new XAttribute("Include", project.Location),
                    new XElement(MSBuildNS + "Project", project.Id),
                    new XElement(MSBuildNS + "Name", project.Name));

                if (exclude)
                {
                    projectReferenceXElement.Add(new XElement(MSBuildNS + "ReferenceOutputAssembly", "false"));
                }

                return projectReferenceXElement;
            }

            public void WriteToFile()
            {
                FileInfo file = new FileInfo(Location);
                file.Directory.Create();
                File.WriteAllText(Location, ToString());
            }

            public override string ToString()
            {
                var itemGroup = new XElement(MSBuildNS + "ItemGroup");
                foreach(var projectReferenceXElement in ProjectReferences)
                {
                    itemGroup.Add(projectReferenceXElement);
                }

                var projectXElement = Util.CreateProjFileXmlContent(Name);
                projectXElement.Add(itemGroup);

                return projectXElement.ToString();
            }
        }
        #endregion
    }
}
