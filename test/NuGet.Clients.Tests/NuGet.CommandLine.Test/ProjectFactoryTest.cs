﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Build.Evaluation;
using Moq;
using NuGet.CommandLine.Test;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine
{
    using System.Xml;
    using NuGet.Packaging;

    public class ProjectFactoryTest
    {
        [Fact]
        public void ProjectFactoryInitializesPropertiesForPreprocessorFromAssemblyMetadata()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();
            const string inputSpec = @"<?xml version=""1.0""?>
	<package>
	    <metadata>
	        <id>$id$</id>
	        <version>$version$</version>
	        <description>$description$</description>
	        <authors>$owner$</authors>
	        <copyright>$copyright$</copyright>
	        <licenseUrl>https://github.com/NuGet/NuGet.Client/blob/master/LICENSE.txt</licenseUrl>
	        <projectUrl>https://github.com/NuGet/NuGet.Client</projectUrl>
	        <tags>nuget</tags>
	    </metadata>
	</package>";
            var projectXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
	<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
	    <PropertyGroup>
	        <ProjectGuid>{F879F274-EFA0-4157-8404-33A19B4E6AEC}</ProjectGuid>
	        <OutputType>Library</OutputType>
	        <RootNamespace>NuGet.Test</RootNamespace>
	        <AssemblyName>" + testAssembly.GetName().Name + @"</AssemblyName>
	        <TargetFrameworkProfile Condition="" '$(TargetFrameworkVersion)' == 'v4.0' "">Client</TargetFrameworkProfile>    
	        <OutputPath>.</OutputPath> <!-- Force it to look for the assembly in the base path -->
	        <TargetPath>" + testAssembly.ManifestModule.FullyQualifiedName + @"</TargetPath>
	    </PropertyGroup>
	    
	    <ItemGroup>
	        <Compile Include=""..\..\Dummy.cs"">
	          <Link>Dummy.cs</Link>
	        </Compile>
	    </ItemGroup>
	 
	    <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
	</Project>";

            // Set base path to the currently assembly's folder so that it will find the test assembly
            var basePath = Path.GetDirectoryName(testAssembly.CodeBase);

            var project = new Project(XmlReader.Create(new StringReader(projectXml)));
            project.FullPath = Path.Combine(project.DirectoryPath, "test.csproj");

            // Act
            var factory = new ProjectFactory(@"C:\Program Files (x86)\MSBuild\14.0\Bin", project) { Build = false };
            var packageBuilder = factory.CreateBuilder(basePath, new NuGetVersion("3.0.0"), "", true);
            var actual = Preprocessor.Process(inputSpec.AsStream(), factory, false);

            var xdoc = XDocument.Load(new StringReader(actual));
            Assert.Equal(testAssembly.GetName().Name, xdoc.XPathSelectElement("/package/metadata/id").Value);
            Assert.Equal(testAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion, xdoc.XPathSelectElement("/package/metadata/version").Value);
            Assert.Equal("", xdoc.XPathSelectElement("/package/metadata/description").Value);
            Assert.Equal(testAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright, xdoc.XPathSelectElement("/package/metadata/copyright").Value);
            Assert.Equal(
                testAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .Where(attr => attr.Key == "owner")
                    .Select(attr => attr.Value)
                    .FirstOrDefault(), 
                xdoc.XPathSelectElement("/package/metadata/authors").Value);
        }

        [Fact]
        public void CommandLinePropertiesOverrideAssemblyMetadataForPreprocessor()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();
            const string inputSpec = @"<?xml version=""1.0""?>
	<package>
	    <metadata>
	        <id>$id$</id>
	        <version>$version$</version>
	        <description>$description$</description>
	        <authors>$owner$</authors>
	        <copyright>$copyright$</copyright>
	        <licenseUrl>https://github.com/NuGet/NuGet.Client/blob/master/LICENSE.txt</licenseUrl>
	        <projectUrl>https://github.com/NuGet/NuGet.Client</projectUrl>
	        <tags>nuget</tags>
	    </metadata>
	</package>";
            var projectXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
	<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
	    <PropertyGroup>
	        <ProjectGuid>{F879F274-EFA0-4157-8404-33A19B4E6AEC}</ProjectGuid>
	        <OutputType>Library</OutputType>
	        <RootNamespace>NuGet.Test</RootNamespace>
	        <AssemblyName>" + testAssembly.GetName().Name + @"</AssemblyName>
	        <TargetFrameworkProfile Condition="" '$(TargetFrameworkVersion)' == 'v4.0' "">Client</TargetFrameworkProfile>    
	        <OutputPath>.</OutputPath> <!-- Force it to look for the assembly in the base path -->
	        <TargetPath>" + testAssembly.ManifestModule.FullyQualifiedName + @"</TargetPath>
	    </PropertyGroup>
	    
	    <ItemGroup>
	        <Compile Include=""..\..\Dummy.cs"">
	          <Link>Dummy.cs</Link>
	        </Compile>
	    </ItemGroup>
	 
	    <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
	</Project>";

            // Set base path to the currently assembly's folder so that it will find the test assembly
            var basePath = Path.GetDirectoryName(testAssembly.CodeBase);
            var cmdLineProperties = new Dictionary<string, string>
                    {
                        { "owner", "overriden" }
                    };
            var project = new Project(XmlReader.Create(new StringReader(projectXml)), cmdLineProperties, null);
            project.FullPath = Path.Combine(project.DirectoryPath, "test.csproj");

            var factory = new ProjectFactory(@"C:\Program Files (x86)\MSBuild\14.0\Bin", project) { Build = false };
            // Cmdline properties are added to the factory, see PackCommand.cs(351)
            factory.ProjectProperties["owner"] = "overriden";

            // Act
            var packageBuilder = factory.CreateBuilder(basePath, new NuGetVersion("3.0.0"), null, true);
            var actual = Preprocessor.Process(inputSpec.AsStream(), factory, false);

            var xdoc = XDocument.Load(new StringReader(actual));
            Assert.Equal(testAssembly.GetName().Name, xdoc.XPathSelectElement("/package/metadata/id").Value);
            Assert.Equal(testAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion, xdoc.XPathSelectElement("/package/metadata/version").Value);
            Assert.Equal("", xdoc.XPathSelectElement("/package/metadata/description").Value);
            Assert.Equal(testAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright, xdoc.XPathSelectElement("/package/metadata/copyright").Value);
            Assert.Equal(
                cmdLineProperties["owner"],
                xdoc.XPathSelectElement("/package/metadata/authors").Value);
        }

        [Fact]
        public void CommandLinePropertiesApplyForPreprocessor()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();
            const string inputSpec = @"<?xml version=""1.0""?>
	<package>
	    <metadata>
	        <id>$id$</id>
	        <version>$version$</version>
	        <description>$description$</description>
	        <authors>$overriden$</authors>
	        <copyright>$copyright$</copyright>
	        <licenseUrl>https://github.com/NuGet/NuGet.Client/blob/master/LICENSE.txt</licenseUrl>
	        <projectUrl>https://github.com/NuGet/NuGet.Client</projectUrl>
	        <tags>nuget</tags>
	    </metadata>
	</package>";
            var projectXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
	<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
	    <PropertyGroup>
	        <ProjectGuid>{F879F274-EFA0-4157-8404-33A19B4E6AEC}</ProjectGuid>
	        <OutputType>Library</OutputType>
	        <RootNamespace>NuGet.Test</RootNamespace>
	        <AssemblyName>" + testAssembly.GetName().Name + @"</AssemblyName>
	        <TargetFrameworkProfile Condition="" '$(TargetFrameworkVersion)' == 'v4.0' "">Client</TargetFrameworkProfile>    
	        <OutputPath>.</OutputPath> <!-- Force it to look for the assembly in the base path -->
	        <TargetPath>" + testAssembly.ManifestModule.FullyQualifiedName + @"</TargetPath>
	    </PropertyGroup>
	    
	    <ItemGroup>
	        <Compile Include=""..\..\Dummy.cs"">
	          <Link>Dummy.cs</Link>
	        </Compile>
	    </ItemGroup>
	 
	    <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
	</Project>";

            // Set base path to the currently assembly's folder so that it will find the test assembly
            var basePath = Path.GetDirectoryName(testAssembly.CodeBase);
            var cmdLineProperties = new Dictionary<string, string>
                    {
                        { "overriden", "Outercurve" }
                    };
            var project = new Project(XmlReader.Create(new StringReader(projectXml)), cmdLineProperties, null);
            project.FullPath = Path.Combine(project.DirectoryPath, "test.csproj");

            var factory = new ProjectFactory(@"C:\Program Files (x86)\MSBuild\14.0\Bin", project) { Build = false };
            // Cmdline properties are added to the factory, see PackCommand.cs
            factory.ProjectProperties.AddRange(cmdLineProperties);

            // Act
            var packageBuilder = factory.CreateBuilder(basePath, new NuGetVersion("3.0.0"), "", true);
            var actual = Preprocessor.Process(inputSpec.AsStream(), factory, false);

            var xdoc = XDocument.Load(new StringReader(actual));
            Assert.Equal(testAssembly.GetName().Name, xdoc.XPathSelectElement("/package/metadata/id").Value);
            Assert.Equal(testAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion, xdoc.XPathSelectElement("/package/metadata/version").Value);
            Assert.Equal("", xdoc.XPathSelectElement("/package/metadata/description").Value);
            Assert.Equal(testAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright, xdoc.XPathSelectElement("/package/metadata/copyright").Value);
            Assert.Equal(
                cmdLineProperties["overriden"],
                xdoc.XPathSelectElement("/package/metadata/authors").Value);
        }

        [Fact]
        public void CommandLineIdPropertyOverridesAssemblyNameForPreprocessor()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();
            const string inputSpec = @"<?xml version=""1.0""?>
	<package>
	    <metadata>
	        <id>$id$</id>
	        <version>$version$</version>
	        <description>$description$</description>
	        <authors>Outercurve</authors>
	        <copyright>$copyright$</copyright>
	        <licenseUrl>https://github.com/NuGet/NuGet.Client/blob/master/LICENSE.txt</licenseUrl>
	        <projectUrl>https://github.com/NuGet/NuGet.Client</projectUrl>
	        <tags>nuget</tags>
	    </metadata>
	</package>";
            var projectXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
	<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
	    <PropertyGroup>
	        <ProjectGuid>{F879F274-EFA0-4157-8404-33A19B4E6AEC}</ProjectGuid>
	        <OutputType>Library</OutputType>
	        <RootNamespace>NuGet.Test</RootNamespace>
	        <AssemblyName>" + testAssembly.GetName().Name + @"</AssemblyName>
	        <TargetFrameworkProfile Condition="" '$(TargetFrameworkVersion)' == 'v4.0' "">Client</TargetFrameworkProfile>    
	        <OutputPath>.</OutputPath> <!-- Force it to look for the assembly in the base path -->
	        <TargetPath>" + testAssembly.ManifestModule.FullyQualifiedName + @"</TargetPath>
	    </PropertyGroup>
	    
	    <ItemGroup>
	        <Compile Include=""..\..\Dummy.cs"">
	          <Link>Dummy.cs</Link>
	        </Compile>
	    </ItemGroup>
	 
	    <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
	</Project>";

            // Set base path to the currently assembly's folder so that it will find the test assembly
            var basePath = Path.GetDirectoryName(testAssembly.CodeBase);
            var cmdLineProperties = new Dictionary<string, string>
                    {
                        { "id", "Outercurve" }
                    };
            var project = new Project(XmlReader.Create(new StringReader(projectXml)), cmdLineProperties, null);
            project.FullPath = Path.Combine(project.DirectoryPath, "test.csproj");

            var factory = new ProjectFactory(@"C:\Program Files (x86)\MSBuild\14.0\Bin", project) { Build = false };
            // Cmdline properties are added to the factory, see PackCommand.cs
            factory.ProjectProperties.AddRange(cmdLineProperties);

            // Act
            var packageBuilder = factory.CreateBuilder(basePath, new NuGetVersion("3.0.0"), "", true);
            var actual = Preprocessor.Process(inputSpec.AsStream(), factory, false);

            var xdoc = XDocument.Load(new StringReader(actual));
            Assert.Equal(cmdLineProperties["id"], xdoc.XPathSelectElement("/package/metadata/id").Value);
        }

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
        <licenseUrl>https://github.com/NuGet/NuGet.Client/blob/master/LICENSE.txt</licenseUrl>
        <projectUrl>https://github.com/NuGet/NuGet.Client</projectUrl>
        <tags>nuget</tags>
    </metadata>
</package>";
            var metadata = new ManifestMetadata
            {
                Id = "ProjectFactoryTest",
                Version = NuGetVersion.Parse("2.0.30619.9000"),
                Title = "NuGet.Test",
                Description = "",
                Copyright = "\x00a9 Outercurve. All rights reserved.",
                Authors = new[] { "Outercurve Foundation" },
            };
            var projectMock = new Mock<Project>();
            var msbuildDirectory = NuGet.CommandLine.MsBuildUtility.GetMsbuildDirectory("4.0", console: null);
            var factory = new ProjectFactory(msbuildDirectory, projectMock.Object);

            // act
            var author = factory.InitializeProperties(metadata);
            var actual = NuGet.Common.Preprocessor.Process(inputSpec.AsStream(), propName => factory.GetPropertyValue(propName));

            // assert
            Assert.Equal("Outercurve Foundation", author);

            var xdoc = XDocument.Load(new StringReader(actual));
            Assert.Equal(metadata.Id, xdoc.XPathSelectElement("/package/metadata/id").Value);
            Assert.Equal(metadata.Version.ToString(), xdoc.XPathSelectElement("/package/metadata/version").Value);
            Assert.Equal(metadata.Description, xdoc.XPathSelectElement("/package/metadata/description").Value);
            Assert.Equal(string.Join(",", metadata.Authors), xdoc.XPathSelectElement("/package/metadata/authors").Value);
            Assert.Equal(metadata.Copyright, xdoc.XPathSelectElement("/package/metadata/copyright").Value);
        }

        [Fact]
        public void ProjectFactoryCanCompareContentsOfReadOnlyFile()
        {
            var us = Assembly.GetExecutingAssembly();
            var sourcePath = us.Location;
            var fullPath = sourcePath + "readOnly";
            File.Copy(sourcePath, fullPath);
            File.SetAttributes(fullPath, FileAttributes.ReadOnly);
            try
            {
                var actual = ProjectFactory.ContentEquals(sourcePath, fullPath);

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
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange

                var projPath = Path.Combine(workingDirectory, "Assembly.csproj");
                File.WriteAllText(projPath, GetProjectContent());
                File.WriteAllText(Path.Combine(workingDirectory, "Assembly.nuspec"), GetNuspecContent());
                File.WriteAllText(Path.Combine(workingDirectory, "Source.cs"), GetSourceFileContent());

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    workingDirectory,
                    "pack Assembly.csproj -build",
                    waitForExit: true);

                // Assert
                var package = new OptimizedZipPackage(Path.Combine(workingDirectory, "Assembly.1.0.0.nupkg"));
                var files = package.GetFiles().Select(f => f.Path).ToArray();

                Assert.Equal(0, r.Item1);
                Array.Sort(files);
                Assert.Equal(files, new[] {
                    @"lib\net45\Assembly.dll",
                    @"lib\net45\Assembly.xml"
                });
            }
        }

        [Fact]
        public void EnsureProjectFactoryWorksAsExpectedWithReferenceOutputAssemblyValuesBasic()
        {
            // Setup
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Setup the projects
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
        }

        [Fact]
        public void EnsureProjectFactoryWorksAsExpectedWithReferenceOutputAssemblyValuesComplex()
        {
            // Setup
            var nugetexe = Util.GetNuGetExePath();

            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Setup the projects
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
                foreach (var projectReferenceXElement in ProjectReferences)
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
