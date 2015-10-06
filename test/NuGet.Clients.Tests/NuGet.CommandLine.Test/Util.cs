using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public static class Util
    {
        private static readonly string NupkgFileFormat = "{0}.{1}.nupkg";

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
                packageBuilder.Files.Add(CreatePackageFile(@"content\test1.txt"));
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
        /// Creates the specified directory. If it exists, it's first deleted before
        /// it's created. Thus, the directory is guaranteed to be empty.
        /// </summary>
        /// <param name="directory">The directory to be created.</param>
        public static void CreateDirectory(string directory)
        {
            Util.DeleteDirectory(directory);
            Directory.CreateDirectory(directory);
        }

        /// <summary>
        /// Deletes the specified directory.
        /// </summary>
        /// <param name="packageDirectory">The directory to be deleted.</param>
        public static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
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

            server.Get.Add("/nuget", r => "OK");
            return server;
        }

        public static string GetNuGetExePath()
        {
            var targetDir = ConfigurationManager.AppSettings["TargetDir"] ?? Directory.GetCurrentDirectory();
            var nugetexe = Path.Combine(targetDir, "nuget.exe");
            return nugetexe;
        }

        public static string GetTestablePluginPath()
        {
            var targetDir = ConfigurationManager.AppSettings["TargetDir"] ?? Directory.GetCurrentDirectory();
            var plugin = Path.Combine(targetDir, "TestableCredentialProvider", "CredentialProvider.Testable.exe");
            return plugin;
        }

        public static string GetTestablePluginDirectory()
        {
            return Path.GetDirectoryName(GetTestablePluginPath());
        }

        public static bool IsSuccess(Tuple<int, string, string> result)
        {
            return result.Item1 == 0;
        }

        public static JObject CreateIndexJson()
        {
            return JObject.Parse(@"{
                  ""version"": ""3.2.0"",
                  ""resources"": [],
                ""@context"": {
                ""@vocab"": ""http://schema.nuget.org/services#"",
                ""comment"": ""http://www.w3.org/2000/01/rdf-schema#comment""
                    }}");
        }

        public static void AddFlatContainerResource(JObject index, MockServer server)
        {
            var resource = new JObject();
            resource.Add("@id", string.Format("{0}flat", server.Uri));
            resource.Add("@type", "PackageBaseAddress/3.0.0");

            var array = index["resources"] as JArray;
            array.Add(resource);
        }

        public static void AddRegistrationResource(JObject index, MockServer server)
        {
            var resource = new JObject();
            resource.Add("@id", string.Format("{0}reg", server.Uri));
            resource.Add("@type", "RegistrationsBaseUrl/3.0.0-beta");

            var array = index["resources"] as JArray;
            array.Add(resource);
        }

        public static void AddLegacyUrlResource(JObject index, MockServer serverV2)
        {
            var resource = new JObject();
            resource.Add("@id", string.Format("{0}", serverV2.Uri));
            resource.Add("@type", "LegacyGallery/2.0.0");

            var array = index["resources"] as JArray;
            array.Add(resource);
        }

        public static void AddPublishResource(JObject index, MockServer publishServer)
        {
            var resource = new JObject();
            resource.Add("@id", string.Format("{0}push", publishServer.Uri));
            resource.Add("@type", "PackagePublish/2.0.0");

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

            MachineCache.Default.RemovePackage(zipPackage);

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

        public static void VerifyResultSuccess(Tuple<int, string, string> result, string expectedOutputMessage = null)
        {
            Assert.True(
                result.Item1 == 0,
                "nuget.exe DID NOT SUCCEED: Ouput is " + result.Item2 + ". Error is " + result.Item3);

            if (!string.IsNullOrEmpty(expectedOutputMessage))
            {
                Assert.True(
                    result.Item2.Contains(expectedOutputMessage),
                    "Expected output is " + expectedOutputMessage + ". Actual output is " + result.Item2);
            }
        }

        public static void VerifyResultFailure(Tuple<int, string, string> result, string expectedErrorMessage)
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
            string normalizedVersion = packageIdentity.Version.ToNormalizedString();

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
            foreach(var package in packages)
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
                = new VersionFolderPathResolver(packagesDirectory, normalizePackageId: true);

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
    }
}