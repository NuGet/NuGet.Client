﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public static class Util
    {
        private static readonly string NupkgFileFormat = "{0}.{1}.nupkg";

        [DllImport("libc")]
        static extern int uname(IntPtr buf);

        public static string CreateTestPackage(
            string packageId,
            string version,
            string path,
            string framework,
            string dependencyPackageId,
            string dependencyPackageVersion)
        {
            var group = new PackageDependencyGroup(NuGetFramework.AnyFramework,
                new List<Packaging.Core.PackageDependency>()
            {
                new Packaging.Core.PackageDependency(dependencyPackageId, VersionRange.Parse(dependencyPackageVersion))
            });

            return CreateTestPackage(packageId, version, path,
                new List<NuGetFramework>() { NuGetFramework.Parse(framework) },
                new List<PackageDependencyGroup>() { group });
        }

        public static string CreateTestPackage(
            string packageId,
            string version,
            string path,
            List<NuGetFramework> frameworks,
            List<PackageDependencyGroup> dependencies)
        {
            var packageBuilder = new PackageBuilder
            {
                Id = packageId,
                Version = new SemanticVersion(version)
            };

            packageBuilder.Description = string.Format(
                CultureInfo.InvariantCulture,
                "desc of {0} {1}",
                packageId, version);

            foreach (var framework in frameworks)
            {
                var libPath = string.Format(
                    CultureInfo.InvariantCulture,
                    "lib/{0}/file.dll",
                    framework.GetShortFolderName());

                packageBuilder.Files.Add(CreatePackageFile(libPath));
            }

            packageBuilder.Authors.Add("test author");

            foreach (var group in dependencies)
            {
                var set = new PackageDependencySet(
                    null,
                    group.Packages.Select(package =>
                        new PackageDependency(package.Id,
                            VersionUtility.ParseVersionSpec(package.VersionRange.ToNormalizedString()))));

                packageBuilder.DependencySets.Add(set);
            }

            var packageFileName = string.Format("{0}.{1}.nupkg", packageId, version);
            var packageFileFullPath = Path.Combine(path, packageFileName);

            Directory.CreateDirectory(path);
            using (var fileStream = File.Create(packageFileFullPath))
            {
                packageBuilder.Save(fileStream);
            }

            return packageFileFullPath;
        }

        public static string CreateTestPackage(
            string packageId,
            string version,
            string path,
            List<NuGetFramework> frameworks,
            params string[] contentFiles)
        {
            var packageBuilder = new PackageBuilder
            {
                Id = packageId,
                Version = new SemanticVersion(version)
            };
            packageBuilder.Description = string.Format(
                CultureInfo.InvariantCulture,
                "desc of {0} {1}",
                packageId, version);
            foreach (var framework in frameworks)
            {
                var libPath = string.Format(
                    CultureInfo.InvariantCulture,
                    "lib/{0}/{1}.dll",
                    framework.GetShortFolderName(),
                    packageId);

                packageBuilder.Files.Add(CreatePackageFile(libPath));
            }

            foreach (var contentFile in contentFiles)
            {
                var packageFilePath = Path.Combine("content", contentFile);
                var packageFile = CreatePackageFile(packageFilePath);
                packageBuilder.Files.Add(packageFile);
            }

            packageBuilder.Authors.Add("test author");

            var packageFileName = string.Format("{0}.{1}.nupkg", packageId, version);
            var packageFileFullPath = Path.Combine(path, packageFileName);
            using (var fileStream = File.Create(packageFileFullPath))
            {
                packageBuilder.Save(fileStream);
            }

            return packageFileFullPath;
        }

        /// <summary>
        /// Creates a test package.
        /// </summary>
        /// <param name="packageId">The id of the created package.</param>
        /// <param name="version">The version of the created package.</param>
        /// <param name="path">The directory where the package is created.</param>
        /// <returns>The full path of the created package file.</returns>
        public static string CreateTestPackage(
            string packageId,
            string version,
            string path,
            Uri licenseUrl = null,
            params string[] contentFiles)
        {
            var packageBuilder = new PackageBuilder
            {
                Id = packageId,
                Version = new SemanticVersion(version)
            };
            packageBuilder.Description = string.Format(
                CultureInfo.InvariantCulture,
                "desc of {0} {1}",
                packageId, version);

            if (licenseUrl != null)
            {
                packageBuilder.LicenseUrl = licenseUrl;
            }

            if (contentFiles == null || contentFiles.Length == 0)
            {
                packageBuilder.Files.Add(CreatePackageFile(Path.Combine("content","test1.txt")));
            }
            else
            {
                foreach (var contentFile in contentFiles)
                {
                    var packageFilePath = Path.Combine("content", contentFile);
                    var packageFile = CreatePackageFile(packageFilePath);
                    packageBuilder.Files.Add(packageFile);
                }
            }

            packageBuilder.Authors.Add("test author");

            var packageFileName = string.Format("{0}.{1}.nupkg", packageId, version);
            var packageFileFullPath = Path.Combine(path, packageFileName);
            Directory.CreateDirectory(path);
            using (var fileStream = File.Create(packageFileFullPath))
            {
                packageBuilder.Save(fileStream);
            }

            return packageFileFullPath;
        }

        /// <summary>
        /// Creates a basic package builder for unit tests.
        /// </summary>
        public static PackageBuilder CreateTestPackageBuilder(string packageId, string version)
        {
            var packageBuilder = new PackageBuilder
            {
                Id = packageId,
                Version = new SemanticVersion(version)
            };

            packageBuilder.Description = string.Format(
                CultureInfo.InvariantCulture,
                "desc of {0} {1}",
                packageId, version);

            packageBuilder.Authors.Add("test author");

            return packageBuilder;
        }

        public static string CreateTestPackage(PackageBuilder packageBuilder, string directory)
        {
            var packageFileName = string.Format("{0}.{1}.nupkg", packageBuilder.Id, packageBuilder.Version);
            var packageFileFullPath = Path.Combine(directory, packageFileName);
            using (var fileStream = File.Create(packageFileFullPath))
            {
                packageBuilder.Save(fileStream);
            }

            return packageFileFullPath;
        }

        /// <summary>
        /// Create a project.json based project. Returns the path to the project file.
        /// </summary>
        public static string CreateUAPProject(string directory, string projectJsonContent)
        {
            return CreateUAPProject(directory, projectJsonContent, "a");
        }

        /// <summary>
        /// Create a project.json based project. Returns the path to the project file.
        /// </summary>
        public static string CreateUAPProject(string directory, string projectJsonContent, string projectName)
        {
            return CreateUAPProject(directory, projectJsonContent, projectName, nugetConfigContent: null);
        }

        /// <summary>
        /// Create a project.json based project. Returns the path to the project file.
        /// </summary>
        public static string CreateUAPProject(string directory, string projectJsonContent, string projectName, string nugetConfigContent)
        {
            Directory.CreateDirectory(directory);
            var projectDir = directory;
            var projectFile = Path.Combine(projectDir, projectName + ".csproj");
            var projectJsonPath = Path.Combine(projectDir, "project.json");
            var configPath = Path.Combine(projectDir, "NuGet.Config");

            // Clean up and validate json
            var json = JObject.Parse(projectJsonContent);

            File.WriteAllText(projectJsonPath, json.ToString());
            File.WriteAllText(projectFile, GetCSProjXML(projectName));

            if (!string.IsNullOrEmpty(nugetConfigContent))
            {
                File.WriteAllText(configPath, nugetConfigContent);
            }

            return projectFile;
        }

        /// <summary>
        /// Creates a file with the specified content.
        /// </summary>
        /// <param name="directory">The directory of the created file.</param>
        /// <param name="fileName">The name of the created file.</param>
        /// <param name="fileContent">The content of the created file.</param>
        public static void CreateFile(string directory, string fileName, string fileContent)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var fileFullName = Path.Combine(directory, fileName);
            CreateFile(fileFullName, fileContent);
        }

        public static void CreateFile(string fileFullName, string fileContent)
        {
            using (var writer = new StreamWriter(fileFullName))
            {
                writer.Write(fileContent);
            }
        }

        private static IPackageFile CreatePackageFile(string name)
        {
            var file = new Mock<IPackageFile>();
            file.SetupGet(f => f.Path).Returns(name);
            file.Setup(f => f.GetStream()).Returns(new MemoryStream());

            string effectivePath;
            var fx = VersionUtility.ParseFrameworkNameFromFilePath(name, out effectivePath);
            file.SetupGet(f => f.EffectivePath).Returns(effectivePath);
            file.SetupGet(f => f.TargetFramework).Returns(fx);

            return file.Object;
        }

        public static IPackageFile CreatePackageFile(string path, string content)
        {
            var file = new Mock<IPackageFile>();
            file.SetupGet(f => f.Path).Returns(path);
            file.Setup(f => f.GetStream()).Returns(new MemoryStream(Encoding.UTF8.GetBytes(content)));

            string effectivePath;
            var fx = VersionUtility.ParseFrameworkNameFromFilePath(path, out effectivePath);
            file.SetupGet(f => f.EffectivePath).Returns(effectivePath);
            file.SetupGet(f => f.TargetFramework).Returns(fx);

            return file.Object;
        }

        /// <summary>
        /// Creates a mock server that contains the specified list of packages
        /// </summary>
        public static MockServer CreateMockServer(IList<IPackage> packages)
        {
            var server = new MockServer();

            server.Get.Add("/nuget/$metadata", r =>
                   MockServerResource.NuGetV2APIMetadata);
            server.Get.Add("/nuget/FindPackagesById()", r =>
                new Action<HttpListenerResponse>(response =>
                {
                    response.ContentType = "application/atom+xml;type=feed;charset=utf-8";
                    string feed = server.ToODataFeed(packages, "FindPackagesById");
                    MockServer.SetResponseContent(response, feed);
                }));

            foreach (var package in packages)
            {
                var url = string.Format(
                    CultureInfo.InvariantCulture,
                    "/nuget/Packages(Id='{0}',Version='{1}')",
                    package.Id,
                    package.Version);
                server.Get.Add(url, r =>
                    new Action<HttpListenerResponse>(response =>
                    {
                        response.ContentType = "application/atom+xml;type=entry;charset=utf-8";
                        var p1 = server.ToOData(package);
                        MockServer.SetResponseContent(response, p1);
                    }));

                // download url
                url = string.Format(
                    CultureInfo.InvariantCulture,
                    "/package/{0}/{1}",
                    package.Id,
                    package.Version);
                server.Get.Add(url, r =>
                    new Action<HttpListenerResponse>(response =>
                    {
                        response.ContentType = "application/zip";
                        using (var stream = package.GetStream())
                        {
                            var content = stream.ReadAllBytes();
                            MockServer.SetResponseContent(response, content);
                        }
                    }));
            }

            // fall through to "package not found"
            server.Get.Add("/nuget/Packages(Id='", r =>
                new Action<HttpListenerResponse>(response =>
                {
                    response.StatusCode = 404;
                    MockServer.SetResponseContent(response, @"<?xml version=""1.0"" encoding=""utf-8""?>
<m:error xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  <m:code />
  <m:message xml:lang=""en-US"">Resource not found for the segment 'Packages'.</m:message>
</m:error>");
                }));

            server.Get.Add("/nuget", r =>
                new Action<HttpListenerResponse>(response =>
                {
                    response.StatusCode = 404;
                }));

            return server;
        }

        public static string GetNuGetExePath()
        {
            var targetDir = ConfigurationManager.AppSettings["TestTargetDir"] ?? Directory.GetCurrentDirectory();
            var nugetexe = Path.Combine(targetDir, "NuGet.exe");
            return nugetexe;
        }

        public static string GetTestablePluginPath()
        {
            var targetDir = ConfigurationManager.AppSettings["TestTargetDir"] ?? Directory.GetCurrentDirectory();
            var plugin = Path.Combine(targetDir, "TestableCredentialProvider", "CredentialProvider.Testable.exe");
            return plugin;
        }

        public static string GetTestablePluginDirectory()
        {
            return Path.GetDirectoryName(GetTestablePluginPath());
        }

        public static bool IsSuccess(CommandRunnerResult result)
        {
            return result.Item1 == 0;
        }

        public static JObject CreateIndexJson()
        {
            return JObject.Parse(@"
{
    ""version"": ""3.2.0"",
    ""resources"": [],
    ""@context"": {
        ""@vocab"": ""http://schema.nuget.org/services#"",
        ""comment"": ""http://www.w3.org/2000/01/rdf-schema#comment""
    }
}");
        }

        public static void AddFlatContainerResource(JObject index, MockServer server)
        {
            var resource = new JObject
            {
                { "@id", $"{server.Uri}flat" },
                { "@type", "PackageBaseAddress/3.0.0" }
            };

            var array = index["resources"] as JArray;
            array.Add(resource);
        }

        public static void AddRegistrationResource(JObject index, MockServer server)
        {
            var resource = new JObject
            {
                { "@id", $"{server.Uri}reg" },
                { "@type", "RegistrationsBaseUrl/3.0.0-beta" }
            };

            var array = index["resources"] as JArray;
            array.Add(resource);
        }

        public static void AddLegacyGalleryResource(JObject index, MockServer serverV2, string relativeUri = null)
        {
            var resourceUri = new Uri(serverV2.Uri);
            if (relativeUri != null)
            {
                resourceUri = new Uri(resourceUri, relativeUri);
            }

            var resource = new JObject
            {
                { "@id", resourceUri },
                { "@type", "LegacyGallery/2.0.0" }
            };

            var array = index["resources"] as JArray;
            array.Add(resource);
        }

        public static void AddPublishResource(JObject index, MockServer publishServer)
        {
            var resource = new JObject
            {
                { "@id", $"{publishServer.Uri}push" },
                { "@type", "PackagePublish/2.0.0" }
            };

            var array = index["resources"] as JArray;
            array.Add(resource);
        }

        public static void CreateConfigForGlobalPackagesFolder(string workingDirectory)
        {
            CreateNuGetConfig(workingDirectory, new List<string>());
        }

        public static void CreateNuGetConfig(string workingPath, List<string> sources)
        {
            var doc = new XDocument();
            var configuration = new XElement(XName.Get("configuration"));
            doc.Add(configuration);

            var config = new XElement(XName.Get("config"));
            configuration.Add(config);

            var globalFolder = new XElement(XName.Get("add"));
            globalFolder.Add(new XAttribute(XName.Get("key"), "globalPackagesFolder"));
            globalFolder.Add(new XAttribute(XName.Get("value"), Path.Combine(workingPath, "globalPackages")));
            config.Add(globalFolder);

            var solutionDir = new XElement(XName.Get("add"));
            solutionDir.Add(new XAttribute(XName.Get("key"), "repositoryPath"));
            solutionDir.Add(new XAttribute(XName.Get("value"), Path.Combine(workingPath, "packages")));
            config.Add(solutionDir);

            var packageSources = new XElement(XName.Get("packageSources"));
            configuration.Add(packageSources);
            packageSources.Add(new XElement(XName.Get("clear")));

            foreach (var source in sources)
            {
                var sourceEntry = new XElement(XName.Get("add"));
                sourceEntry.Add(new XAttribute(XName.Get("key"), source));
                sourceEntry.Add(new XAttribute(XName.Get("value"), source));
                packageSources.Add(sourceEntry);
            }

            Util.CreateFile(workingPath, "NuGet.Config", doc.ToString());
        }

        public static void CreateNuGetConfig(string workingPath, List<string> sources, List<string> pluginPaths)
        {
            CreateNuGetConfig(workingPath, sources);
            var existingConfig = Path.Combine(workingPath, "NuGet.Config");

            var doc = XDocument.Load(existingConfig);
            var config = doc.Descendants(XName.Get("config")).FirstOrDefault();

            foreach (var pluginPath in pluginPaths)
            {
                var key = "CredentialProvider.Plugin." + Path.GetFileNameWithoutExtension(pluginPath);
                var pluginElement = new XElement(XName.Get("add"));
                pluginElement.Add(new XAttribute(XName.Get("key"), key));
                pluginElement.Add(new XAttribute(XName.Get("value"), pluginPath));

                config.Add(pluginElement);
            }

            doc.Save(existingConfig);
        }

        public static void CreateNuGetConfig(string workingPath, List<string> sources, string packagesPath)
        {
            CreateNuGetConfig(workingPath, sources);
            var existingConfig = Path.Combine(workingPath, "NuGet.Config");

            var doc = XDocument.Load(existingConfig);
            var config = doc.Descendants(XName.Get("config")).FirstOrDefault();
            var repositoryPath = config.Descendants().First(x => x.Name == "add" && x.Attribute("key").Value == "repositoryPath").Attribute("value");
            repositoryPath.SetValue(packagesPath);

            doc.Save(existingConfig);
        }

        /// <summary>
        /// Create a simple package with a lib folder. This package should install everywhere.
        /// The package will be removed from the machine cache upon creation
        /// </summary>
        public static ZipPackage CreatePackage(string repositoryPath, string id, string version)
        {
            var package = Util.CreateTestPackageBuilder(id, version);
            var libFile = Util.CreatePackageFile("lib/uap/a.dll", "a");
            package.Files.Add(libFile);

            libFile = Util.CreatePackageFile("lib/net45/a.dll", "a");
            package.Files.Add(libFile);

            libFile = Util.CreatePackageFile("lib/native/a.dll", "a");
            package.Files.Add(libFile);

            libFile = Util.CreatePackageFile("lib/win/a.dll", "a");
            package.Files.Add(libFile);

            libFile = Util.CreatePackageFile("lib/net20/a.dll", "a");
            package.Files.Add(libFile);

            var path = Util.CreateTestPackage(package, repositoryPath);

            ZipPackage zipPackage = new ZipPackage(path);

            return zipPackage;
        }

        /// <summary>
        /// Create a registration blob for a single package
        /// </summary>
        public static JObject CreateSinglePackageRegistrationBlob(MockServer server, string id, string version)
        {
            var indexUrl = string.Format(CultureInfo.InvariantCulture,
                                    "{0}reg/{1}/index.json", server.Uri, id);

            JObject regBlob = new JObject();
            regBlob.Add(new JProperty("@id", indexUrl));
            var typeArray = new JArray();
            regBlob.Add(new JProperty("@type", typeArray));
            typeArray.Add("catalog: CatalogRoot");
            typeArray.Add("PackageRegistration");
            typeArray.Add("catalog: Permalink");

            regBlob.Add(new JProperty("commitId", Guid.NewGuid()));
            regBlob.Add(new JProperty("commitTimeStamp", "2015-06-22T22:30:00.1487642Z"));
            regBlob.Add(new JProperty("count", "1"));

            var pages = new JArray();
            regBlob.Add(new JProperty("items", pages));

            var page = new JObject();
            pages.Add(page);

            page.Add(new JProperty("@id", indexUrl + "#page/0.0.0/9.0.0"));
            page.Add(new JProperty("@type", indexUrl + "catalog:CatalogPage"));
            page.Add(new JProperty("commitId", Guid.NewGuid()));
            page.Add(new JProperty("commitTimeStamp", "2015-06-22T22:30:00.1487642Z"));
            page.Add(new JProperty("count", "1"));
            page.Add(new JProperty("parent", indexUrl));
            page.Add(new JProperty("lower", "0.0.0"));
            page.Add(new JProperty("upper", "9.0.0"));

            var items = new JArray();
            page.Add(new JProperty("items", items));

            var item = new JObject();
            items.Add(item);

            item.Add(new JProperty("@id",
                    string.Format("{0}reg/{1}/{2}.json", server.Uri, id, version)));

            item.Add(new JProperty("@type", "Package"));
            item.Add(new JProperty("commitId", Guid.NewGuid()));
            item.Add(new JProperty("commitTimeStamp", "2015-06-22T22:30:00.1487642Z"));

            var catalogEntry = new JObject();
            item.Add(new JProperty("catalogEntry", catalogEntry));
            item.Add(new JProperty("packageContent", $"{server.Uri}packages/{id}.{version}.nupkg"));
            item.Add(new JProperty("registration", indexUrl));

            catalogEntry.Add(new JProperty("@id",
                string.Format("{0}catalog/{1}/{2}.json", server.Uri, id, version)));

            catalogEntry.Add(new JProperty("@type", "PackageDetails"));
            catalogEntry.Add(new JProperty("authors", "test"));
            catalogEntry.Add(new JProperty("description", "test"));
            catalogEntry.Add(new JProperty("iconUrl", ""));
            catalogEntry.Add(new JProperty("id", id));
            catalogEntry.Add(new JProperty("language", "en-us"));
            catalogEntry.Add(new JProperty("licenseUrl", ""));
            catalogEntry.Add(new JProperty("listed", true));
            catalogEntry.Add(new JProperty("minClientVersion", ""));
            catalogEntry.Add(new JProperty("projectUrl", ""));
            catalogEntry.Add(new JProperty("published", "2015-06-22T22:30:00.1487642Z"));
            catalogEntry.Add(new JProperty("requireLicenseAcceptance", false));
            catalogEntry.Add(new JProperty("summary", ""));
            catalogEntry.Add(new JProperty("title", ""));
            catalogEntry.Add(new JProperty("version", version));
            catalogEntry.Add(new JProperty("tags", new JArray()));

            return regBlob;
        }

        public static string CreateProjFileContent(
            string projectName = "proj1",
            string targetFrameworkVersion = "v4.5",
            string[] references = null,
            string[] contentFiles = null)
        {
            var project = CreateProjFileXmlContent(projectName, targetFrameworkVersion, references, contentFiles);
            return project.ToString();
        }

        public static XElement CreateProjFileXmlContent(
            string projectName = "proj1",
            string targetFrameworkVersion = "v4.5",
            string[] references = null,
            string[] contentFiles = null)
        {
            XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";

            var project = new XElement(msbuild + "Project",
                new XAttribute("ToolsVersion", "4.0"), new XAttribute("DefaultTargets", "Build"));

            project.Add(new XElement(msbuild + "PropertyGroup",
                  new XElement(msbuild + "OutputType", "Library"),
                  new XElement(msbuild + "OutputPath", "out"),
                  new XElement(msbuild + "TargetFrameworkVersion", targetFrameworkVersion)));

            if (references != null && references.Any())
            {
                project.Add(new XElement(msbuild + "ItemGroup",
                        references.Select(r => new XElement(msbuild + "Reference", new XAttribute("Include", r)))));
            }

            if (contentFiles != null && contentFiles.Any())
            {
                project.Add(new XElement(msbuild + "ItemGroup",
                        contentFiles.Select(c => new XElement(msbuild + "Content", new XAttribute("Include", c)))));
            }

            project.Add(new XElement(msbuild + "ItemGroup",
                new XElement(msbuild + "Compile", new XAttribute("Include", "Source.cs"))));

            project.Add(new XElement(msbuild + "Import",
                new XAttribute("Project", @"$(MSBuildToolsPath)\Microsoft.CSharp.targets")));

            return project;
        }

        public static string CreateSolutionFileContent()
        {
            return @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"",
""proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject";
        }

        public static void VerifyResultSuccess(CommandRunnerResult result, string expectedOutputMessage = null)
        {
            Assert.True(
                result.Item1 == 0,
                "nuget.exe DID NOT SUCCEED: Ouput is " + result.Item2 + ". Error is " + result.Item3);

            if (!string.IsNullOrEmpty(expectedOutputMessage))
            {
                Assert.Contains(
                    expectedOutputMessage,
                    result.Item2);
            }
        }

        public static void VerifyResultFailure(CommandRunnerResult result,
                                               string expectedErrorMessage)
        {
            Assert.True(
                result.Item1 != 0,
                "nuget.exe DID NOT FAIL: Ouput is " + result.Item2 + ". Error is " + result.Item3);

            Assert.True(
                result.Item3.Contains(expectedErrorMessage),
                "Expected error is " + expectedErrorMessage + ". Actual error is " + result.Item3);
        }

        public static void VerifyPackageExists(
            PackageIdentity packageIdentity,
            string packagesDirectory)
        {
            string normalizedId = packageIdentity.Id.ToLowerInvariant();
            string normalizedVersion = packageIdentity.Version.ToNormalizedString().ToLowerInvariant();

            var packageIdDirectory = Path.Combine(packagesDirectory, normalizedId);
            Assert.True(Directory.Exists(packageIdDirectory));

            var packageVersionDirectory = Path.Combine(packageIdDirectory, normalizedVersion);
            Assert.True(Directory.Exists(packageVersionDirectory));

            var nupkgFileName = GetNupkgFileName(normalizedId, normalizedVersion);

            var nupkgFilePath = Path.Combine(packageVersionDirectory, nupkgFileName);
            Assert.True(File.Exists(nupkgFilePath));

            var nupkgSHAFilePath = Path.Combine(packageVersionDirectory, nupkgFileName + ".sha512");
            Assert.True(File.Exists(nupkgSHAFilePath));

            var nuspecFilePath = Path.Combine(packageVersionDirectory, normalizedId + ".nuspec");
            Assert.True(File.Exists(nuspecFilePath));
        }

        public static void VerifyPackageDoesNotExist(
            PackageIdentity packageIdentity,
            string packagesDirectory)
        {
            string normalizedId = packageIdentity.Id.ToLowerInvariant();
            var packageIdDirectory = Path.Combine(packagesDirectory, normalizedId);
            Assert.False(Directory.Exists(packageIdDirectory));
        }

        public static void VerifyPackagesExist(
            IList<PackageIdentity> packages,
            string packagesDirectory)
        {
            foreach (var package in packages)
            {
                VerifyPackageExists(package, packagesDirectory);
            }
        }

        public static void VerifyPackagesDoNotExist(
            IList<PackageIdentity> packages,
            string packagesDirectory)
        {
            foreach (var package in packages)
            {
                VerifyPackageDoesNotExist(package, packagesDirectory);
            }
        }

        /// <summary>
        /// To verify packages created using TestPackages.GetLegacyTestPackage
        /// </summary>
        public static void VerifyExpandedLegacyTestPackagesExist(
            IList<PackageIdentity> packages,
            string packagesDirectory)
        {
            var versionFolderPathResolver
                = new VersionFolderPathResolver(packagesDirectory);

            var packageFiles = new[]
            {
                    "lib/test.dll",
                    "lib/net40/test40.dll",
                    "lib/net40/test40b.dll",
                    "lib/net45/test45.dll",
                };

            foreach (var package in packages)
            {
                Util.VerifyPackageExists(package, packagesDirectory);
                var packageRoot = versionFolderPathResolver.GetInstallPath(package.Id, package.Version);
                foreach (var packageFile in packageFiles)
                {
                    var filePath = Path.Combine(packageRoot, packageFile);
                    Assert.True(File.Exists(filePath), $"For {package}, {filePath} does not exist.");
                }
            }
        }

        public static string GetNupkgFileName(string normalizedId, string normalizedVersion)
        {
            return string.Format(NupkgFileFormat, normalizedId, normalizedVersion);
        }

        /// <summary>
        /// Creates a junction point from the specified directory to the specified target directory.
        /// </summary>
        /// <remarks>Only works on NTFS.</remarks>
        /// <param name="junctionPoint">The junction point path</param>
        /// <param name="targetDirectoryPath">The target directory</param>
        /// <param name="overwrite">If true overwrites an existing reparse point or empty directory</param>
        /// <exception cref="IOException">
        /// Thrown when the junction point could not be created or when
        /// an existing directory was found and <paramref name="overwrite" /> if false
        /// </exception>
        public static void CreateJunctionPoint(string junctionPoint, string targetDirectoryPath, bool overwrite)
        {
            targetDirectoryPath = Path.GetFullPath(targetDirectoryPath);

            if (!Directory.Exists(targetDirectoryPath))
            {
                throw new IOException("Target path does not exist or is not a directory.");
            }

            if (Directory.Exists(junctionPoint))
            {
                if (!overwrite)
                {
                    throw new IOException("Directory already exists and overwrite parameter is false.");
                }
            }
            else
            {
                Directory.CreateDirectory(junctionPoint);
            }

            NativeMethods.CreateReparsePoint(junctionPoint, targetDirectoryPath);
        }

        public static string GetXProjXML()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
                <Project ToolsVersion=""14.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <PropertyGroup>
                    <VisualStudioVersion Condition=""'$(VisualStudioVersion)' == ''"">14.0</VisualStudioVersion>
                    <VSToolsPath Condition=""'$(VSToolsPath)' == ''"">$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)</VSToolsPath>
                  </PropertyGroup>
                  <Import Project=""$(VSToolsPath)\DNX\Microsoft.DNX.Props"" Condition=""'$(VSToolsPath)' != ''"" />
                  <PropertyGroup Label=""Globals"">
                    <ProjectGuid>82ff10c5-8724-4187-953e-5096ad90184f</ProjectGuid>
                  </PropertyGroup>
                  <ItemGroup>
                    <Service Include=""{82a7f48d-3b50-4b1e-b82e-3ada8210c358}"" />
                  </ItemGroup>
                  <Import Project=""$(VSToolsPath)\DNX\Microsoft.DNX.targets"" Condition=""'$(VSToolsPath)' != ''"" />
                </Project>";
        }

        /// <summary>
        /// Create a basic csproj file for net45.
        /// </summary>
        public static string GetCSProjXML(string projectName)
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
                <Project ToolsVersion=""14.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
                  <PropertyGroup>
                    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
                    <ProjectGuid>29b6f645-ae2a-4653-a142-d0de9341adba</ProjectGuid>
                    <OutputType>Library</OutputType>
                    <AppDesignerFolder>Properties</AppDesignerFolder>
                    <RootNamespace>$NAME$</RootNamespace>
                    <AssemblyName>$NAME$</AssemblyName>
                    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
                    <FileAlignment>512</FileAlignment>
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                    <Optimize>false</Optimize>
                    <OutputPath>bin\Debug\</OutputPath>
                    <DefineConstants>DEBUG;TRACE</DefineConstants>
                    <ErrorReport>prompt</ErrorReport>
                    <WarningLevel>4</WarningLevel>
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include=""System""/>
                    <Reference Include=""System.Core""/>
                    <Reference Include=""System.Xml.Linq""/>
                    <Reference Include=""System.Data.DataSetExtensions""/>
                    <Reference Include=""Microsoft.CSharp""/>
                    <Reference Include=""System.Data""/>
                    <Reference Include=""System.Net.Http""/>
                    <Reference Include=""System.Xml""/>
                  </ItemGroup>
                  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
                 </Project>".Replace("$NAME$", projectName);
        }

        public static void ClearWebCache()
        {
            var nugetexe = Util.GetNuGetExePath();

            var r = CommandRunner.Run(
            nugetexe,
            ".",
            "locals http-cache -Clear",
            waitForExit: true);

            Assert.Equal(0, r.Item1);
        }

        public static string CreateBasicTwoProjectSolution(TestDirectory workingPath, string proj1ConfigFileName, string proj2ConfigFileName)
        {
            var repositoryPath = Path.Combine(workingPath, "Repository");
            var proj1Directory = Path.Combine(workingPath, "proj1");
            var proj2Directory = Path.Combine(workingPath, "proj2");

            Directory.CreateDirectory(repositoryPath);
            Directory.CreateDirectory(proj1Directory);
            Directory.CreateDirectory(proj2Directory);

            CreateTestPackage("packageA", "1.1.0", repositoryPath);
            CreateTestPackage("packageB", "2.2.0", repositoryPath);

            CreateFile(workingPath, "a.sln",
                @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 2012
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj1"", ""proj1\proj1.csproj"", ""{A04C59CC-7622-4223-B16B-CDF2ECAD438D}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""proj2"", ""proj2\proj2.csproj"", ""{42641DAE-D6C4-49D4-92EA-749D2573554A}""
EndProject");

            var include1 = proj1ConfigFileName;

            if (string.IsNullOrEmpty(include1))
            {
                include1 = Guid.NewGuid().ToString();
            }

            CreateFile(proj1Directory, "proj1.csproj",
                $@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='{include1}' />
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
</Project>");

            if (!string.IsNullOrEmpty(proj1ConfigFileName))
            {
                CreateConfigFile(proj1Directory, proj1ConfigFileName, "net45", new List<PackageIdentity>
                {
                    new PackageIdentity("packageA", new NuGetVersion("1.1.0"))
                });
            }

            var include2 = proj2ConfigFileName;

            if (string.IsNullOrEmpty(include2))
            {
                include2 = Guid.NewGuid().ToString();
            }

            CreateFile(proj2Directory, "proj2.csproj",
                $@"<Project ToolsVersion='4.0' DefaultTargets='Build'
    xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <OutputPath>out</OutputPath>
    <TargetFrameworkVersion>v4.0</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <None Include='{include2}' />
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
</Project>");

            if (!string.IsNullOrEmpty(proj2ConfigFileName))
            {
                CreateConfigFile(proj2Directory, proj2ConfigFileName, "net45", new List<PackageIdentity>
                {
                    new PackageIdentity("packageB", new NuGetVersion("2.2.0"))
                });
            }

            // If either project uses project.json, then define "globalPackagesFolder" so the package doesn't get
            // installed in the usual global packages folder.
            if (IsProjectJson(proj1ConfigFileName) || IsProjectJson(proj2ConfigFileName))
            {
                CreateFile(workingPath, "nuget.config",
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <add key=""globalPackagesFolder"" value=""GlobalPackages"" />
  </config>
</configuration>");
            }

            return repositoryPath;
        }

        public static void CreateConfigFile(string path, string configFileName, string targetFramework, IEnumerable<PackageIdentity> packages)
        {
            var fileContent = IsProjectJson(configFileName)
                ? GetProjectJsonFileContents(targetFramework, packages)
                : GetPackagesConfigFileContents(targetFramework, packages);

            CreateFile(path, configFileName, fileContent);
        }

        public static string GetProjectJsonFileContents(string targetFramework, IEnumerable<PackageIdentity> packages)
        {
            var dependencies = string.Join(", ", packages.Select(package => $"'{package.Id}': '{package.Version}'"));
            return $@"
{{
  'dependencies': {{
    {dependencies}
  }},
  'frameworks': {{
    '{targetFramework}': {{ }}
  }}
}}";
        }

        public static string GetPackagesConfigFileContents(string targetFramework, IEnumerable<PackageIdentity> packages)
        {
            var dependencies = string.Join("\n", packages.Select(package => $@"<package id=""{package.Id}"" version=""{package.Version}"" targetFramework=""{targetFramework}"" />"));
            return $@"
<packages>
  {dependencies}
</packages>";
        }

        public static string GetMsbuildPathOnWindows()
        {
            var msbuildPath = @"C:\Program Files (x86)\MSBuild\14.0\Bin";
            if (!Directory.Exists(msbuildPath))
            {
                msbuildPath = @"C:\Program Files\MSBuild\14.0\Bin";
            }

            return msbuildPath;
        }

        public static string GetHintPath(string path)
        {
            return @"<HintPath>.." + Path.DirectorySeparatorChar + path + @"</HintPath>";
        }

        public static bool IsRunningOnMac()
        {

            IntPtr buf = IntPtr.Zero;

            try
            {

                buf = Marshal.AllocHGlobal(8192);

                // This is a hacktastic way of getting sysname from uname ()

                if (uname(buf) == 0)
                {

                    string os = Marshal.PtrToStringAnsi(buf);

                    if (os == "Darwin")

                        return true;

                }

            }
            catch
            {

            }
            finally
            {

                if (buf != IntPtr.Zero)

                    Marshal.FreeHGlobal(buf);

            }

            return false;

        }

        private static bool IsProjectJson(string configFileName)
        {
            // Simply test the extension as that is all we care about
            return string.Equals(Path.GetExtension(configFileName), ".json", StringComparison.OrdinalIgnoreCase);
        }
    }
}