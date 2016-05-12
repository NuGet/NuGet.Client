using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageBuilderTest
    {
        [Fact]
        public void CreatePackageWithEmptyFoldersForV3Folders()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };

            builder.Authors.Add("testAuthor");

            var dependencies = new List<PackageDependency>();
            dependencies.Add(new PackageDependency("packageB", VersionRange.Parse("1.0.0"), null, new[] { "z" }));
            dependencies.Add(new PackageDependency(
                "packageC",
                VersionRange.Parse("1.0.0"),
                new[] { "a", "b", "c" },
                new[] { "b", "c" }));

            var set = new PackageDependencyGroup(NuGetFramework.AnyFramework, dependencies);
            builder.DependencyGroups.Add(set);

            builder.Files.Add(CreatePackageFile(@"build" + Path.DirectorySeparatorChar + "_._"));
            builder.Files.Add(CreatePackageFile(@"content" + Path.DirectorySeparatorChar + "_._"));
            builder.Files.Add(CreatePackageFile(@"contentFiles" + Path.DirectorySeparatorChar + "any" + Path.DirectorySeparatorChar + "any" + Path.DirectorySeparatorChar + "_._"));
            builder.Files.Add(CreatePackageFile(@"lib" + Path.DirectorySeparatorChar + "net45" + Path.DirectorySeparatorChar + "_._"));
            builder.Files.Add(CreatePackageFile(@"native" + Path.DirectorySeparatorChar + "net45" + Path.DirectorySeparatorChar + "_._"));
            builder.Files.Add(CreatePackageFile(@"ref" + Path.DirectorySeparatorChar + "net45" + Path.DirectorySeparatorChar + "_._"));
            builder.Files.Add(CreatePackageFile(@"runtimes" + Path.DirectorySeparatorChar + "net45" + Path.DirectorySeparatorChar + "_._"));
            builder.Files.Add(CreatePackageFile(@"tools" + Path.DirectorySeparatorChar + "_._"));

            using (var ms = new MemoryStream())
            {
                // Act
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                using (var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true))
                {
                    var files = archive.Entries
                        .Where(file => file.Name == "_._")
                        .Select(file => file.FullName)
                        .OrderBy(s => s)
                        .ToArray();

                    // Assert
                    Assert.Equal(8, files.Length);
                    Assert.Equal(@"build/_._", files[0]);
                    Assert.Equal(@"content/_._", files[1]);
                    Assert.Equal(@"contentFiles/any/any/_._", files[2]);
                    Assert.Equal(@"lib/net45/_._", files[3]);
                    Assert.Equal(@"native/net45/_._", files[4]);
                    Assert.Equal(@"ref/net45/_._", files[5]);
                    Assert.Equal(@"runtimes/net45/_._", files[6]);
                    Assert.Equal(@"tools/_._", files[7]);
                }
            }
        }

        [Fact]
        public void CreatePackageWithNuspecIncludeExcludeAnyGroup()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");

            var dependencies = new List<PackageDependency>();
            dependencies.Add(new PackageDependency("packageB", VersionRange.Parse("1.0.0"), null, new[] { "z" }));
            dependencies.Add(new PackageDependency(
                "packageC",
                VersionRange.Parse("1.0.0"),
                new[] { "a", "b", "c" },
                new[] { "b", "c" }));

            var set = new PackageDependencyGroup(NuGetFramework.AnyFramework, dependencies);
            builder.DependencyGroups.Add(set);

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);
                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                var result = manifestStream.ReadToEnd();

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <dependencies>
      <group>
        <dependency id=""packageB"" version=""1.0.0"" exclude=""z"" />
        <dependency id=""packageC"" version=""1.0.0"" include=""a,b,c"" exclude=""b,c"" />
      </group>
    </dependencies>
  </metadata>
</package>".Replace("\r\n", "\n"), result.Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CreatePackageWithNuspecIncludeExclude()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");

            var dependencies45 = new List<PackageDependency>();
            dependencies45.Add(new PackageDependency("packageB", VersionRange.Parse("1.0.0"), null, new[] { "z" }));

            var dependencies46 = new List<PackageDependency>();
            dependencies46.Add(new PackageDependency(
                "packageC",
                VersionRange.Parse("1.0.0"),
                new[] { "a", "b", "c" },
                new[] { "b", "c" }));

            var net45 = new PackageDependencyGroup(new NuGetFramework(".NETFramework", new Version(4, 5)), dependencies45);
            builder.DependencyGroups.Add(net45);

            var net46 = new PackageDependencyGroup(new NuGetFramework(".NETFramework", new Version(4, 6)), dependencies46);
            builder.DependencyGroups.Add(net46);

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                var result = manifestStream.ReadToEnd();

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <dependencies>
      <group targetFramework="".NETFramework4.5"">
        <dependency id=""packageB"" version=""1.0.0"" exclude=""z"" />
      </group>
      <group targetFramework="".NETFramework4.6"">
        <dependency id=""packageC"" version=""1.0.0"" include=""a,b,c"" exclude=""b,c"" />
      </group>
    </dependencies>
  </metadata>
</package>".Replace("\r\n", "\n"), result.Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CreatePackageWithNuspecContentV2()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.Files.Add(CreatePackageFile("contentFiles" + Path.DirectorySeparatorChar + "any" + Path.DirectorySeparatorChar + "any" + Path.DirectorySeparatorChar + "config" + Path.DirectorySeparatorChar + "config.xml"));
            builder.Files.Add(CreatePackageFile("contentFiles" + Path.DirectorySeparatorChar + "cs" + Path.DirectorySeparatorChar + "net45" + Path.DirectorySeparatorChar + "code.cs.pp"));

            builder.ContentFiles.Add(new ManifestContentFiles()
            {
                Include = "**/*",
                BuildAction = "Compile"
            });

            builder.ContentFiles.Add(new ManifestContentFiles()
            {
                Include = "**/*",
                Exclude = "**/*.cs",
                BuildAction = "None",
                Flatten = "true",
                CopyToOutput = "true"
            });

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                var result = manifestStream.ReadToEnd();

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <contentFiles>
      <files include=""**/*"" buildAction=""Compile"" />
      <files include=""**/*"" exclude=""**/*.cs"" buildAction=""None"" copyToOutput=""true"" flatten=""true"" />
    </contentFiles>
  </metadata>
</package>".Replace("\r\n", "\n"), result.Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void OwnersFallsBackToAuthorsIfNoneSpecified()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Description"
            };
            builder.Authors.Add("JohnDoe");
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>JohnDoe</authors>
    <owners>JohnDoe</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void ReleaseNotesAttributeIsRecognized()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Description",
                ReleaseNotes = "Release Notes"
            };
            builder.Authors.Add("JohnDoe");
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>JohnDoe</authors>
    <owners>JohnDoe</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <releaseNotes>Release Notes</releaseNotes>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void CreatePackageUsesV1SchemaNamespaceIfFrameworkAssembliesAreUsed()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("JohnDoe");
            builder.FrameworkReferences.Add(new FrameworkAssemblyReference("System.Web", new[] { NuGetFramework.AnyFramework }));
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>JohnDoe</authors>
    <owners>JohnDoe</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <frameworkAssemblies>
      <frameworkAssembly assemblyName=""System.Web"" targetFramework="""" />
    </frameworkAssemblies>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void CreatePackageUsesV2SchemaNamespaceIfReferenceAssembliesAreUsed()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
                PackageAssemblyReferences = new[] { new PackageReferenceSet(NuGetFramework.AnyFramework, new string[] { "foo.dll" }) }
            };
            builder.Authors.Add("JohnDoe");
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>JohnDoe</authors>
    <owners>JohnDoe</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <references>
      <reference file=""foo.dll"" />
    </references>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void CreatePackageUsesV2SchemaNamespaceIfDependecyHasNoTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");

            var dependencies = new PackageDependency[] {
                        new PackageDependency("B")
                    };

            builder.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.AnyFramework, dependencies));
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <dependencies>
      <dependency id=""B"" />
    </dependencies>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void CreatePackageUsesV4SchemaNamespaceIfDependecyHasTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");

            var fx = new NuGetFramework("Silverlight", new Version("4.0"));
            var dependencies = new PackageDependency[] {
                        new PackageDependency("B", null)
                    };
            builder.DependencyGroups.Add(new PackageDependencyGroup(fx, dependencies));

            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <dependencies>
      <group targetFramework=""Silverlight4.0"">
        <dependency id=""B"" />
      </group>
    </dependencies>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void CreatePackageUsesV4SchemaNamespaceIfContentHasTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.Files.Add(CreatePackageFile("content" + Path.DirectorySeparatorChar + "winrt53" + Path.DirectorySeparatorChar + "one.txt"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageWithPackageTypes()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Description",
                Authors = { "testAuthor" },
                PackageTypes = new[]
                {
                    new PackageType("foo", new Version(0, 0)),
                    new PackageType("bar", new Version(2, 0, 0)),
                },
                Files =
                {
                    CreatePackageFile("content" + Path.DirectorySeparatorChar + "winrt53" + Path.DirectorySeparatorChar + "one.txt")
                }
            };

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2016/04/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <packageTypes>
      <packageType name=""foo"" />
      <packageType name=""bar"" version=""2.0.0"" />
    </packageTypes>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageDoesNotUseV4SchemaNamespaceIfContentHasUnsupportedTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.Files.Add(CreatePackageFile("content" + Path.DirectorySeparatorChar + "bar" + Path.DirectorySeparatorChar + "one.txt"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageUsesV4SchemaNamespaceIfToolsHasTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.Files.Add(CreatePackageFile("tools" + Path.DirectorySeparatorChar + "sl4" + Path.DirectorySeparatorChar + "one.dll"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageDoesNotUseV4SchemaNamespaceIfToolsHasUnsupportedTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.Files.Add(CreatePackageFile("tools" + Path.DirectorySeparatorChar + "foo" + Path.DirectorySeparatorChar + "one.dll"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Theory]
        [InlineData("lib\\sl4\\_._")]
        [InlineData("content\\winrt\\_._")]
        [InlineData("tools\\sl4-wp\\_._")]
        public void CreatePackageUsesV4SchemaNamespaceIfLibHasEmptyTargetFramework(string packagePath)
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.Files.Add(CreatePackageFile(packagePath.Replace('\\', Path.DirectorySeparatorChar)));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Theory]
        [InlineData("content\\web.config.install.xdt")]
        [InlineData("content\\app.config.uninstall.xdt")]
        [InlineData("content\\winrt45\\foo.uninstall.xdt")]
        [InlineData("content\\winrt45\\sub\\bar.uninstall.xdt")]
        public void CreatePackageUsesV5SchemaNamespaceIfContentHasTransformFile(string packagePath)
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.Files.Add(CreatePackageFile(packagePath.Replace('\\', Path.DirectorySeparatorChar)));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageUsesV5SchemaNamespaceIfContentHasBothInstallAndUninstallTransformFile()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.Files.Add(CreatePackageFile("content" + Path.DirectorySeparatorChar + "web.config.install.xdt"));
            builder.Files.Add(CreatePackageFile("content" + Path.DirectorySeparatorChar + "app.config.uninstall.xdt"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Theory]
        [InlineData("lib\\web.config.install.xdt")]
        [InlineData("lib\\app.config.uninstall.xdt")]
        [InlineData("tools\\foo.uninstall.xdt")]
        [InlineData("random\\sub\\bar.uninstall.xdt")]
        public void CreatePackageDoesNotUseV5SchemaNamespaceIfTransformFileIsOutsideContent(string packagePath)
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.Files.Add(CreatePackageFile(packagePath.Replace('\\', Path.DirectorySeparatorChar)));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Theory]
        [InlineData("content\\web.config.install2.xdt")]
        [InlineData("content\\app.config.xdt")]
        [InlineData("content\\foo.update.xdt")]
        [InlineData("content\\sub\\bar.xdt.uninstall")]
        [InlineData("content\\sub\\bar.xdt.install")]
        public void CreatePackageDoesNotUseV5SchemaNamespaceIfTransformFileExtensionIsNotComplete(string packagePath)
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.Files.Add(CreatePackageFile(packagePath.Replace('\\', Path.DirectorySeparatorChar)));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageUsesV5SchemaNamespaceIfReferencesTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.PackageAssemblyReferences = new[] {
                new PackageReferenceSet(
                    NuGetFramework.Parse(".NET, Version=3.0"),
                    new[] { "one.dll" })};
            builder.Files.Add(CreatePackageFile("lib" + Path.DirectorySeparatorChar + "one.dll"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <references>
      <group targetFramework="".NETFramework3.0"">
        <reference file=""one.dll"" />
      </group>
    </references>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageUsesV5SchemaNamespaceIfDevelopmentDependency()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
                DevelopmentDependency = true
            };
            builder.Authors.Add("testAuthor");
            builder.PackageAssemblyReferences = new[] {
                new PackageReferenceSet(
                    NuGetFramework.Parse(".NET, Version=3.0"),
                    new[] { "one.dll" })};
            builder.Files.Add(CreatePackageFile("lib" + Path.DirectorySeparatorChar + "one.dll"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <developmentDependency>true</developmentDependency>
    <description>Descriptions</description>
    <references>
      <group targetFramework="".NETFramework3.0"">
        <reference file=""one.dll"" />
      </group>
    </references>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageDoesNotUseV5SchemaNamespaceIfReferencesHasOnlyNullTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("testAuthor");
            builder.PackageAssemblyReferences = new[] {
                new PackageReferenceSet(
                    NuGetFramework.UnsupportedFramework,
                    new[] { "one.dll" })};
            builder.Files.Add(CreatePackageFile("lib" + Path.DirectorySeparatorChar + "one.dll"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <references>
      <reference file=""one.dll"" />
    </references>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageUsesV5SchemaNamespaceIfMinClientVersionIsSet()
        {
            // Arrange
            var builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
                MinClientVersion = new Version("2.0")
            };
            builder.Authors.Add("testAuthor");
            builder.Files.Add(CreatePackageFile("a.txt"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
  <metadata minClientVersion=""2.0"">
    <id>A</id>
    <version>1.0.0</version>
    <authors>testAuthor</authors>
    <owners>testAuthor</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageTrimsExtraWhitespace()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "                 A                 ",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions                                         ",
                Summary = "                            Summary",
                Language = "     en-us   ",
                Copyright = "            Copyright 2012                "
            };
            builder.Authors.Add("JohnDoe");
            builder.Owners.Add("John");
            builder.Tags.Add("t1");
            builder.Tags.Add("t2");
            builder.Tags.Add("t3");
            var dependencies = new PackageDependency[] {
                        new PackageDependency("    X     ")
                    };
            builder.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.AnyFramework, dependencies));
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>JohnDoe</authors>
    <owners>John</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <summary>Summary</summary>
    <copyright>Copyright 2012</copyright>
    <language>en-us</language>
    <tags>t1 t2 t3</tags>
    <dependencies>
      <dependency id=""X"" />
    </dependencies>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void VersionFormatIsPreserved()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
                Summary = "Summary",
            };
            builder.Authors.Add("JohnDoe");

            var dependencySet = new PackageDependencyGroup(NuGetFramework.AnyFramework, new[] {
                        new PackageDependency("B", new VersionRange(NuGetVersion.Parse("1.0"), true)),
                        new PackageDependency("C", new VersionRange(NuGetVersion.Parse("1.0"), false, NuGetVersion.Parse("5.0")))
            });

            //var dependencySet = new PackageDependencyGroup(NuGetFramework.AnyFramework, new[] {
            //            new PackageDependency("B", new VersionSpec
            //                {
            //                    MinVersion = NuGetVersion.Parse("1.0"),
            //                    IsMinInclusive = true
            //                }),
            //            new PackageDependency("C", new VersionSpec
            //            {
            //                MinVersion = NuGetVersion.Parse("1.0"),
            //                MaxVersion = NuGetVersion.Parse("5.0"),
            //                IsMinInclusive = false
            //            })
            //        });


            builder.DependencyGroups.Add(dependencySet);

            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // REVIEW: Changed the value of these to get this test passing
            //      <dependency id=""B"" version=""1.0"" />
            //      <dependency id=""C"" version=""(1.0, 5.0)"" />

            // Assert
            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <authors>JohnDoe</authors>
    <owners>JohnDoe</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <summary>Summary</summary>
    <dependencies>
      <dependency id=""B"" version=""1.0.0"" />
      <dependency id=""C"" version=""(1.0.0, 5.0.0)"" />
    </dependencies>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void SavingPackageWithDuplicateDependenciesThrows()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
                Summary = "Summary",
            };
            builder.Authors.Add("JohnDoe");

            var dependencySet = new PackageDependencyGroup(NuGetFramework.AnyFramework, new[] {
                        new PackageDependency("B", new VersionRange(NuGetVersion.Parse("1.0"), true)),
                        new PackageDependency("B", new VersionRange(NuGetVersion.Parse("1.0"), false, NuGetVersion.Parse("5.0")))
            });

            builder.DependencyGroups.Add(dependencySet);

            var ms = new MemoryStream();

            // Act
            ExceptionAssert.Throws<InvalidOperationException>(() => Manifest.Create(builder).Save(ms), "'A' already has a dependency defined for 'B'.");
        }

        [Fact]
        public void SavingPackageWithInvalidDependencyRangeThrows()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
                Summary = "Summary",
            };
            builder.Authors.Add("JohnDoe");

            var dependencySet = new PackageDependencyGroup(NuGetFramework.AnyFramework, new[] {
                        new PackageDependency("B", new VersionRange(NuGetVersion.Parse("1.0"), true, NuGetVersion.Parse("1.0")))
                    });

            builder.DependencyGroups.Add(dependencySet);

            var ms = new MemoryStream();

            // Act
            ExceptionAssert.Throws<InvalidOperationException>(() => Manifest.Create(builder).Save(ms), "Dependency 'B' has an invalid version.");
        }

        [Fact]
        public void SavingPackageValidatesReferences()
        {
            // Arrange
            var builder = new PackageBuilder
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Test",
            };
            builder.Authors.Add("Test");
            builder.Files.Add(new PhysicalPackageFile { TargetPath = @"lib\Foo.dll" });
            builder.PackageAssemblyReferences = new[] { new PackageReferenceSet(NuGetFramework.AnyFramework, new string[] { "Bar.dll" }) };

            ExceptionAssert.Throws<InvalidDataException>(() => builder.Save(new MemoryStream()),
                "Invalid assembly reference 'Bar.dll'. Ensure that a file named 'Bar.dll' exists in the lib directory.");
        }

        [Fact]
        public void SavingPackageWithInvalidDependencyVersionMaxLessThanMinThrows()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Descriptions",
                Summary = "Summary",
            };
            builder.Authors.Add("JohnDoe");

            var dependencySet = new PackageDependencyGroup(NuGetFramework.AnyFramework, new[] {
                        new PackageDependency("B", new VersionRange(NuGetVersion.Parse("2.0"), true, NuGetVersion.Parse("1.0")))
                    });

            builder.DependencyGroups.Add(dependencySet);

            var ms = new MemoryStream();

            // Act
            ExceptionAssert.Throws<InvalidOperationException>(() => Manifest.Create(builder).Save(ms), "Dependency 'B' has an invalid version.");
        }

        [Fact]
        public void SaveThrowsIfRequiredPropertiesAreMissing()
        {
            // Arrange
            var builder = new PackageBuilder();
            builder.Id = "Package";
            builder.Files.Add(new Mock<IPackageFile>().Object);

            // Act & Assert
            ExceptionAssert.Throws<Exception>(() => builder.Save(new MemoryStream()), @"Version is required.
Authors is required.
Description is required.");
        }

        [Fact]
        public void SaveThrowsIfNoFilesOrDependencies()
        {
            // Arrange
            var builder = new PackageBuilder();
            builder.Id = "A";
            builder.Version = NuGetVersion.Parse("1.0");
            builder.Description = "Description";

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => builder.Save(new MemoryStream()), "Cannot create a package that has no dependencies nor content.");
        }

        [Fact]
        public void PackageBuilderThrowsIfXmlIsMalformed()
        {
            // Arrange
            string spec1 = "kjdkfj";
            string spec2 = @"<?xml version=""1.0"" encoding=""utf-8""?>";
            string spec3 = @"<?xml version=""1.0"" encoding=""utf-8""?><package />";
            string spec4 = @"<?xml version=""1.0"" encoding=""utf-8""?><package><metadata></metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
#if !IS_CORECLR
            // REVIEW: Unsupported on CoreCLR
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
#endif

            // Act and Assert
            ExceptionAssert.Throws<XmlException>(() => new PackageBuilder(spec1.AsStream(), null), "Data at the root level is invalid. Line 1, position 1.");
            ExceptionAssert.Throws<XmlException>(() => new PackageBuilder(spec2.AsStream(), null), "Root element is missing.");
#if !IS_CORECLR
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec3.AsStream(), null));
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec4.AsStream(), null));
#else
            ExceptionAssert.Throws<InvalidDataException>(() => new PackageBuilder(spec3.AsStream(), null));
            ExceptionAssert.Throws<InvalidDataException>(() => new PackageBuilder(spec4.AsStream(), null));
#endif
        }

        [Fact]
        public void MissingIdThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
  </metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
#if !IS_CORECLR
            // REVIEW: Unsupported on CoreCLR
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
#endif

            // Act & Assert
#if !IS_CORECLR
            ExceptionAssert.Throws<InvalidOperationException>(
                () => new PackageBuilder(spec.AsStream(), null),
                "The element 'metadata' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd' has incomplete content. " +
                "List of possible elements expected: 'id' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'. " +
                "This validation error occurred in a 'metadata' element.");
#else
            ExceptionAssert.Throws<InvalidDataException>(() => new PackageBuilder(spec.AsStream(), null), "The required element 'id' is missing from the manifest.");
#endif
        }

        [Fact]
        public void WrongCaseIdThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <ID>aaa</ID>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
  </metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
#if !IS_CORECLR
            // REVIEW: Unsupported on CoreCLR
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
#endif

            // Act & Assert
#if !IS_CORECLR
            var exception = Assert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null));
            Assert.StartsWith(
                "The element 'metadata' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd' has invalid child element 'ID' " +
                "in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'.",
                exception.Message);
            Assert.EndsWith(
                "This validation error occurred in a 'ID' element.",
                exception.Message);
#else
            ExceptionAssert.Throws<InvalidDataException>(() => new PackageBuilder(spec.AsStream(), null), "The required element 'id' is missing from the manifest.");
#endif
        }

        [Fact]
        public void IdExceedingMaxLengthThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
  </metadata></package>";

            // Act & Assert
            ExceptionAssert.Throws<Exception>(() => new PackageBuilder(spec.AsStream(), null), "Id must not exceed 100 characters.");
        }

        [Fact]
        public void SpecialVersionExceedingMaxLengthThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>aaa</id>
    <version>2.5.0-vvvvvvvvvvvvvvvvvvvvv</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <dependencies>
       <dependency id=""A"" />
    </dependencies>
  </metadata>
</package>";

            var builder = new PackageBuilder(spec.AsStream(), null);

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(
                () => builder.Save(new MemoryStream()),
                "The special version part cannot exceed 20 characters.");
        }

        [Fact]
        public void MissingVersionThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>    
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
  </metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
#if !IS_CORECLR
            // REVIEW: Unsupported on CoreCLR
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
#endif

            // Act & Assert
#if !IS_CORECLR
            ExceptionAssert.Throws<InvalidOperationException>(
                () => new PackageBuilder(spec.AsStream(), null),
                "The element 'metadata' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd' has incomplete content. " +
                "List of possible elements expected: 'version' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'. " +
                "This validation error occurred in a 'metadata' element.");
#else
            ExceptionAssert.Throws<InvalidDataException>(() => new PackageBuilder(spec.AsStream(), null), "The required element 'version' is missing from the manifest.");
#endif
        }

        [Fact]
        public void MissingAuthorsThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <language>en-us</language>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
  </metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
#if !IS_CORECLR
            // REVIEW: Unsupported on CoreCLR
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
#endif

            // Act & Assert
#if !IS_CORECLR
            ExceptionAssert.Throws<InvalidOperationException>(
                () => new PackageBuilder(spec.AsStream(), null),
                "The element 'metadata' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd' has incomplete content. " + 
                "List of possible elements expected: 'authors' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'. " +
                "This validation error occurred in a 'metadata' element.");
#else
            ExceptionAssert.Throws<InvalidDataException>(() => new PackageBuilder(spec.AsStream(), null), "The required element 'authors' is missing from the manifest.");
#endif
        }

        [Fact]
        public void MissingDescriptionThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
  </metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
#if !IS_CORECLR
            // REVIEW: Unsupported on CoreCLR
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
#endif

            // Act & Assert
#if !IS_CORECLR
            ExceptionAssert.Throws<InvalidOperationException>(
                () => new PackageBuilder(spec.AsStream(), null),
                "The element 'metadata' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd' has incomplete content. " +
                "List of possible elements expected: 'description' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'. " +
                "This validation error occurred in a 'metadata' element.");
#else
            ExceptionAssert.Throws<InvalidDataException>(() => new PackageBuilder(spec.AsStream(), null), "The required element 'description' is missing from the manifest.");
#endif
        }

        [Fact]
        public void MalformedDependenciesThrows()
        {
            // Switch to invariant culture to ensure the error message is in english.
#if !IS_CORECLR
            // REVIEW: Unsupported on CoreCLR
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
#endif

            // Act & Assert
#if !IS_CORECLR
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <dependencies>
        <dependency />
    </dependencies>
  </metadata></package>";

            ExceptionAssert.Throws<InvalidOperationException>(
                () => new PackageBuilder(spec.AsStream(), null),
                "The required attribute 'id' is missing. " +
                "This validation error occurred in a 'dependency' element.");
#else
            // Not thrown in CoreCLR
#endif
        }

        [Fact]
        public void ReferencesContainMixedElementsThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <references>
        <reference file=""a.dll"" />
        <group>
           <reference file=""b.dll"" />
        </group>
    </references>
</metadata></package>";

            // Act & Assert
            ExceptionAssert.Throws<InvalidDataException>(() => new PackageBuilder(spec.AsStream(), null), "<references> element must not contain both <group> and <reference> child elements.");
        }

        [Fact]
        public void MissingFileSrcThrows()
        {
            // Act
            // Switch to invariant culture to ensure the error message is in english.
#if !IS_CORECLR
            // REVIEW: Unsupported on CoreCLR
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
#endif

            // Assert
#if !IS_CORECLR
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <dependencies>
        <dependency id=""foo"" />
    </dependencies>
  </metadata>
  <files>
    <file />
  </files>
</package>";

            ExceptionAssert.Throws<InvalidOperationException>(
                () => new PackageBuilder(spec.AsStream(), null),
                "The required attribute 'src' is missing. " +
                "This validation error occurred in a 'file' element.");
#else
            // REVIEW: Not thrown in CoreCLR
#endif
        }

        [Fact]
        public void MisplacedFileNodeThrows()
        {
            // Arrange
            // Act & Assert
#if !IS_CORECLR
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <dependencies>
        <dependency id=""foo"" />
    </dependencies>
  <files>
    <file />
  </files>
  </metadata>
</package>";

            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null));
#else
            // REVIEW: Not thrown in CoreCLR
#endif
        }

        [Fact]
        public void ReadingManifestWithNamespaceBuilderFromStreamCopiesMetadata()
        {
            // Arrange
            string spec = @"<?xml version=""1.0""?>
<package>
    <metadata>
    <id>Artem.XmlProviders  </id>
    <version>2.5</version>
    <title>Some awesome package       </title>
    <authors>These are the authors</authors>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
    <licenseUrl>http://somesite/somelicense.txt</licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <tags>t1      t2    foo-bar</tags>
    <copyright>Copyright 2011</copyright>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);
            var authors = builder.Authors.ToList();
            var owners = builder.Owners.ToList();
            var tags = builder.Tags.ToList();

            // Assert
            Assert.Equal("Artem.XmlProviders", builder.Id);
            Assert.Equal(new NuGetVersion(2, 5, 0, 0), builder.Version);
            Assert.Equal("Some awesome package", builder.Title);
            Assert.Equal(1, builder.Authors.Count);
            Assert.Equal("These are the authors", authors[0]);
            Assert.Equal("These are the authors", owners[0]);
            Assert.Equal(3, builder.Tags.Count);
            Assert.Equal("t1", tags[0]);
            Assert.Equal("t2", tags[1]);
            Assert.Equal("foo-bar", tags[2]);
            Assert.Equal("en-US", builder.Language);
            Assert.Equal("Copyright 2011", builder.Copyright);
            Assert.Equal("This is the Description (With, Comma-Separated, Words, in Parentheses).", builder.Description);
            Assert.Equal(new Uri("http://somesite/somelicense.txt"), builder.LicenseUrl);
            Assert.True(builder.RequireLicenseAcceptance);
        }

        [Fact]
        public void ReadingManifestWithSerializationNamespaceBuilderFromStreamCopiesMetadata()
        {
            // Arrange
            string spec = @"<?xml version=""1.0""?>
<package>
    <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <title>Some awesome package</title>
    <authors>Velio Ivanov</authors>
    <owners>John Doe</owners>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
    <licenseUrl>http://somesite/somelicense.txt</licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);
            var authors = builder.Authors.ToList();
            var owners = builder.Owners.ToList();

            // Assert
            Assert.Equal("Artem.XmlProviders", builder.Id);
            Assert.Equal(new NuGetVersion(2, 5, 0, 0), builder.Version);
            Assert.Equal("Some awesome package", builder.Title);
            Assert.Equal(1, builder.Authors.Count);
            Assert.Equal("Velio Ivanov", authors[0]);
            Assert.Equal(1, builder.Owners.Count);
            Assert.Equal("John Doe", owners[0]);
            Assert.Equal("en-US", builder.Language);
            Assert.Equal("This is the Description (With, Comma-Separated, Words, in Parentheses).", builder.Description);
            Assert.Equal(new Uri("http://somesite/somelicense.txt"), builder.LicenseUrl);
            Assert.True(builder.RequireLicenseAcceptance);
        }

        [Fact]
        public void ReadingManifestWithOldStyleXmlnsDeclaratoinsFromStreamCopiesMetadata()
        {
            // Arrange
            string spec = @"<?xml version=""1.0""?>
<package xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
    <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <title>Some awesome package</title>
    <authors>Velio Ivanov</authors>
    <owners>John Doe</owners>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
    <licenseUrl>http://somesite/somelicense.txt</licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);
            var authors = builder.Authors.ToList();
            var owners = builder.Owners.ToList();

            // Assert
            Assert.Equal("Artem.XmlProviders", builder.Id);
            Assert.Equal(new NuGetVersion(2, 5, 0, 0), builder.Version);
            Assert.Equal("Some awesome package", builder.Title);
            Assert.Equal(1, builder.Authors.Count);
            Assert.Equal("Velio Ivanov", authors[0]);
            Assert.Equal(1, builder.Owners.Count);
            Assert.Equal("John Doe", owners[0]);
            Assert.Equal("en-US", builder.Language);
            Assert.Equal("This is the Description (With, Comma-Separated, Words, in Parentheses).", builder.Description);
            Assert.Equal(new Uri("http://somesite/somelicense.txt"), builder.LicenseUrl);
            Assert.True(builder.RequireLicenseAcceptance);
        }

        [Fact]
        public void ReadingPackageManifestFromStreamCopiesMetadata()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <title>Some awesome package</title>
    <authors>Velio Ivanov</authors>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
    <licenseUrl>http://somesite/somelicense.txt</licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <copyright>2010</copyright>
    <packageTypes>
        <packageType name=""foo"" />
        <packageType name=""bar"" version=""2.0.0"" />
    </packageTypes>
    <dependencies>
        <dependency id=""A"" version=""[1.0]"" />
        <dependency id=""B"" version=""[1.0, 2.5)"" />
    </dependencies>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);
            var authors = builder.Authors.ToList();

            // Assert
            Assert.Equal("Artem.XmlProviders", builder.Id);
            Assert.Equal(new NuGetVersion(2, 5, 0, 0), builder.Version);
            Assert.Equal("Some awesome package", builder.Title);
            Assert.Equal(1, builder.Authors.Count);
            Assert.Equal("Velio Ivanov", authors[0]);
            Assert.Equal("en-US", builder.Language);
            Assert.Equal("2010", builder.Copyright);
            Assert.Equal("This is the Description (With, Comma-Separated, Words, in Parentheses).", builder.Description);
            Assert.Equal(new Uri("http://somesite/somelicense.txt"), builder.LicenseUrl);
            Assert.True(builder.RequireLicenseAcceptance);

            Assert.Equal(2, builder.PackageTypes.Count);
            Assert.Equal("foo", builder.PackageTypes.ElementAt(0).Name);
            Assert.Equal(new Version(0, 0), builder.PackageTypes.ElementAt(0).Version);
            Assert.Equal("bar", builder.PackageTypes.ElementAt(1).Name);
            Assert.Equal(new Version(2, 0, 0), builder.PackageTypes.ElementAt(1).Version);

            Assert.Equal(1, builder.DependencyGroups.Count);
            var dependencyGroup = builder.DependencyGroups.ElementAt(0);

            IDictionary<string, VersionRange> dependencies = dependencyGroup.Packages.ToDictionary(p => p.Id, p => p.VersionRange);
            // <dependency id="A" version="[1.0]" />
            Assert.True(dependencies["A"].IsMinInclusive);
            Assert.True(dependencies["A"].IsMaxInclusive);
            Assert.Equal(NuGetVersion.Parse("1.0"), dependencies["A"].MinVersion);
            Assert.Equal(NuGetVersion.Parse("1.0"), dependencies["A"].MaxVersion);
            // <dependency id="B" version="[1.0, 2.5)" />
            Assert.True(dependencies["B"].IsMinInclusive);
            Assert.False(dependencies["B"].IsMaxInclusive);
            Assert.Equal(NuGetVersion.Parse("1.0"), dependencies["B"].MinVersion);
            Assert.Equal(NuGetVersion.Parse("2.5"), dependencies["B"].MaxVersion);
        }

        [Fact]
        public void ReadingPackageManifestRecognizeDependencyWithTargetFramework()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>SuperPackage</id>
    <version>2.5</version>
    <authors>testAuthor</authors>
    <description>description</description>
    <dependencies>
        <group targetFramework=""sl4"">
            <dependency id=""A"" />
        </group>
    </dependencies>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);

            // Assert
            Assert.Equal(1, builder.DependencyGroups.Count);
            var dependencyGroup = builder.DependencyGroups.ElementAt(0);

            Assert.Equal(NuGetFramework.Parse("Silverlight, Version=4.0"), dependencyGroup.TargetFramework);
            var dependencies = dependencyGroup.Packages.ToList();
            Assert.Equal(1, dependencies.Count);
            Assert.Equal("A", dependencies[0].Id);
            Assert.Equal(dependencies[0].VersionRange, VersionRange.All);
        }

        [Fact]
        public void ReadingPackageManifestRecognizeMultipleDependenciesWithTargetFramework()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>SuperPackage</id>
    <version>2.5</version>
    <authors>testAuthor</authors>
    <description>description</description>
    <dependencies>
        <group targetFramework=""sl4"">
            <dependency id=""A"" />
        </group>
        <group targetFramework=""net1"">
            <dependency id=""B"" />
            <dependency id=""C"" />
        </group>
        <group targetFramework=""net40-client"">
        </group>
    </dependencies>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);

            // Assert
            Assert.Equal(3, builder.DependencyGroups.Count);
            var dependencyGroup1 = builder.DependencyGroups.ElementAt(0);
            var dependencyGroup2 = builder.DependencyGroups.ElementAt(1);
            var dependencyGroup3 = builder.DependencyGroups.ElementAt(2);

            Assert.Equal(NuGetFramework.Parse("Silverlight, Version=4.0"), dependencyGroup1.TargetFramework);
            var dependencies1 = dependencyGroup1.Packages.ToList();
            Assert.Equal(1, dependencies1.Count);
            Assert.Equal("A", dependencies1[0].Id);
            Assert.Equal(dependencies1[0].VersionRange, VersionRange.All);

            Assert.Equal(NuGetFramework.Parse(".NETFramework, Version=1.0"), dependencyGroup2.TargetFramework);
            var dependencies2 = dependencyGroup2.Packages.ToList();
            Assert.Equal(2, dependencies2.Count);
            Assert.Equal("B", dependencies2[0].Id);
            Assert.Equal(dependencies2[0].VersionRange, VersionRange.All);
            Assert.Equal("C", dependencies2[1].Id);
            Assert.Equal(dependencies2[0].VersionRange, VersionRange.All);

            Assert.Equal(NuGetFramework.Parse(".NETFramework, Version=4.0, Profile=Client"), dependencyGroup3.TargetFramework);
            Assert.False(dependencyGroup3.Packages.Any());
        }

        [Fact]
        public void PackageBuilderThrowsWhenDependenciesHasMixedDependencyAndGroupChildren()
        {
            // Arrange
            string spec =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>SuperPackage</id>
    <version>2.5</version>
    <authors>testAuthor</authors>
    <description>description</description>
    <dependencies>
        <dependency id=""A"" />
        <group targetFramework=""net40-client"">
        </group>
    </dependencies>
  </metadata>
</package>";

            // Act
            ExceptionAssert.Throws<InvalidDataException>(
                () => { new PackageBuilder(spec.AsStream(), null); },
                "<dependencies> element must not contain both <group> and <dependency> child elements.");
        }

        [Fact]
        public void PackageBuilderThrowsWhenLicenseUrlIsPresentButEmpty()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
    <licenseUrl></licenseUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
  </metadata>
</package>";

            // Act
            ExceptionAssert.Throws<Exception>(() => new PackageBuilder(spec.AsStream(), null), "LicenseUrl cannot be empty.");
        }

        [Fact]
        public void PackageBuilderThrowsWhenLicenseUrlIsWhiteSpace()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
    <licenseUrl>    </licenseUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
  </metadata>
</package>";

            // Act
            ExceptionAssert.Throws<Exception>(() => new PackageBuilder(spec.AsStream(), null), "LicenseUrl cannot be empty.");
        }

        [Fact]
        public void ValidateReferencesAllowsPartialFileNames()
        {
            // Arrange
            var files = new[] {
                        new PhysicalPackageFile { TargetPath = @"lib" + Path.DirectorySeparatorChar + "net40" + Path.DirectorySeparatorChar + "foo.dll" },
                        new PhysicalPackageFile { TargetPath = @"lib" + Path.DirectorySeparatorChar + "net40" + Path.DirectorySeparatorChar + "bar.dll" },
                        new PhysicalPackageFile { TargetPath = @"lib" + Path.DirectorySeparatorChar + "net40" + Path.DirectorySeparatorChar + "baz.exe" },
                    };
            var packageAssemblyReferences = new PackageReferenceSet(NuGetFramework.AnyFramework, new string[] { "foo.dll", "bar", "baz" });

            // Act and Assert
            PackageBuilder.ValidateReferenceAssemblies(files, new[] { packageAssemblyReferences });

            // If we've got this far, no exceptions were thrown.
            Assert.True(true);
        }

        [Fact]
        public void ValidateReferencesThrowsForPartialNamesThatDoNotHaveAKnownExtension()
        {
            // Arrange
            var files = new[] {
                        new PhysicalPackageFile { TargetPath = @"lib" + Path.DirectorySeparatorChar + "net20" + Path.DirectorySeparatorChar + "foo.dll" },
                        new PhysicalPackageFile { TargetPath = @"lib" + Path.DirectorySeparatorChar + "net20" + Path.DirectorySeparatorChar + "bar.dll" },
                        new PhysicalPackageFile { TargetPath = @"lib" + Path.DirectorySeparatorChar + "net20" + Path.DirectorySeparatorChar + "baz.qux" },
                    };
            var packageAssemblyReferences = new PackageReferenceSet(NuGetFramework.Parse("Silverlight, Version=1.0"), new string[] { "foo.dll", "bar", "baz" });

            // Act and Assert
            ExceptionAssert.Throws<InvalidDataException>(() => PackageBuilder.ValidateReferenceAssemblies(files, new[] { packageAssemblyReferences }),
                "Invalid assembly reference 'baz'. Ensure that a file named 'baz' exists in the lib directory.");
        }

        [Theory]
        [MemberData("InvalidDependencyData")]
        public void ValidateDependenciesThrowsIfAnyDependencyForAStableReleaseIsPrerelease(VersionRange versionRange)
        {
            // Arrange
            var badDependency = new PackageDependency("A", versionRange);
            var dependencies = new[] {
                        badDependency,
                        new PackageDependency("B", new VersionRange()),
                    };
            var packageVersion = NuGetVersion.Parse("1.0.0");

            var dependencySets = new PackageDependencyGroup[] {
                        new PackageDependencyGroup(NuGetFramework.AnyFramework, dependencies)
                    };

            // Act and Assert
            ExceptionAssert.Throws<InvalidDataException>(() => PackageBuilder.ValidateDependencyGroups(packageVersion, dependencySets),
                String.Format(CultureInfo.InvariantCulture,
                    "A stable release of a package should not have on a prerelease dependency. Either modify the version spec of dependency \"{0}\" or update the version field.",
                    badDependency));
        }

        [Theory]
        [MemberData("InvalidDependencyData")]
        public void ValidateDependenciesDoesNotThrowIfDependencyForAPrereleaseVersionIsPrerelease(VersionRange versionRange)
        {
            // Arrange
            var dependencies = new[] {
                        new PackageDependency("A", versionRange),
                        new PackageDependency("B", new VersionRange()),
                    };
            var packageVersion = NuGetVersion.Parse("1.0.0-beta");

            var dependencySets = new PackageDependencyGroup[] {
                        new PackageDependencyGroup(NuGetFramework.AnyFramework, dependencies)
                    };

            // Act
            PackageBuilder.ValidateDependencyGroups(packageVersion, dependencySets);

            // Assert
            // If we've got this far, no exceptions were thrown.
            Assert.True(true);
        }

        [Fact]
        public void ValidateDependenciesDoesNotThrowIfDependencyForAStableVersionIsStable()
        {
            // Arrange
            var dependencies = new[] {
                        new PackageDependency("A", new VersionRange(NuGetVersion.Parse("1.0.0"))),
                        new PackageDependency("B", new VersionRange(NuGetVersion.Parse("1.0.1"), true, NuGetVersion.Parse("1.2.3"))),
                    };
            var packageVersion = NuGetVersion.Parse("1.0.0");

            var dependencySets = new PackageDependencyGroup[] {
                        new PackageDependencyGroup(NuGetFramework.AnyFramework, dependencies)
                    };

            // Act
            PackageBuilder.ValidateDependencyGroups(packageVersion, dependencySets);

            // Assert
            // If we've got this far, no exceptions were thrown.
            Assert.True(true);
        }

        public static IEnumerable<object[]> InvalidDependencyData
        {
            get
            {
                var prereleaseVer = NuGetVersion.Parse("1.0.0-a");
                var version = NuGetVersion.Parse("2.3.0.6232");

                yield return new object[] { new VersionRange(prereleaseVer) };
                yield return new object[] { new VersionRange(prereleaseVer, true, version) };
                yield return new object[] { new VersionRange(version, true, prereleaseVer, true) };
                yield return new object[] { new VersionRange(prereleaseVer, true, prereleaseVer) };
            }
        }

        [Fact]
        public void PackageBuilderRequireLicenseAcceptedWithoutLicenseUrlThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
    <licenseUrl></licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
  </metadata>
</package>";

            // Act
            ExceptionAssert.Throws<Exception>(() => new PackageBuilder(spec.AsStream(), null), @"LicenseUrl cannot be empty.
Enabling license acceptance requires a license url.");
        }

        [Fact]
        public void PackageBuilderThrowsWhenLicenseUrlIsMalformed()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
    <licenseUrl>this-is-a-malformed-url</licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
  </metadata>
</package>";

            // Switch to invariant culture to ensure the error message is in english.
#if !IS_CORECLR
            // REVIEW: Unsupported on CoreCLR
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
#endif

            // Act
            ExceptionAssert.Throws<UriFormatException>(() => new PackageBuilder(spec.AsStream(), null), "Invalid URI: The format of the URI could not be determined.");
        }

        [Fact]
        public void PackageBuilderThrowsIfPackageIdInvalid()
        {
            // Arrange
            var builder = new PackageBuilder
            {
                Id = "  a.  b",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Description"
            };
            builder.Authors.Add("Me");

            // Act & Assert            
            ExceptionAssert.ThrowsArgumentException(() => builder.Save(new MemoryStream()), "The package ID '  a.  b' contains invalid characters. Examples of valid package IDs include 'MyPackage' and 'MyPackage.Sample'.");
        }

        [Fact]
        public void PackageBuilderThrowsIfPackageIdExceedsMaxLengthLimit()
        {
            // Arrange
            var builder = new PackageBuilder
            {
                Id = new string('c', 101),
                Version = NuGetVersion.Parse("1.0"),
                Description = "Description"
            };
            builder.Authors.Add("Me");

            // Act & Assert            
            ExceptionAssert.ThrowsArgumentException(() => builder.Save(new MemoryStream()), "Id must not exceed 100 characters.");
        }

        [Fact]
        public void PackageBuilderThrowsIfSpecialVersionExceedsMaxLengthLimit()
        {
            // Arrange
            var builder = new PackageBuilder
            {
                Id = "cool",
                Version = NuGetVersion.Parse("1.0-vvvvvvvvvvvvvvvvvvvvK"),
                Description = "Description"
            };
            builder.Authors.Add("Me");

            var dependencies = new PackageDependency[] {
                        new PackageDependency("X")
                    };
            builder.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.AnyFramework, dependencies));

            // Act & Assert            
            ExceptionAssert.Throws<InvalidOperationException>(() => builder.Save(new MemoryStream()), "The special version part cannot exceed 20 characters.");
        }

        [Fact]
        public void PackageBuilderThrowsIfDependencyIdInvalid()
        {
            // Arrange
            var builder = new PackageBuilder
            {
                Id = "a.b",
                Version = NuGetVersion.Parse("1.0"),
                Description = "Description"
            };
            builder.Authors.Add("Me");

            builder.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.AnyFramework, new[] { new PackageDependency("brainf%2ack") }));

            // Act & Assert            
            ExceptionAssert.ThrowsArgumentException(() => builder.Save(new MemoryStream()), "The package ID 'brainf%2ack' contains invalid characters. Examples of valid package IDs include 'MyPackage' and 'MyPackage.Sample'.");
        }

#if !IS_CORECLR
        [Fact]
        public void ReadingPackageWithUnknownSchemaThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/03/nuspec.xsd"">
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
  </metadata>
</package>";

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null), "The schema version of 'Artem.XmlProviders' is incompatible with version " + typeof(Manifest).Assembly.GetName().Version + " of NuGet. Please upgrade NuGet to the latest version from http://go.microsoft.com/fwlink/?LinkId=213942.");
        }

        [Fact]
        public void ReadingPackageWithUnknownSchemaAndMissingIdThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/03/nuspec.xsd"">
  <metadata>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
  </metadata>
</package>";

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null), "The schema version of '' is incompatible with version " + typeof(Manifest).Assembly.GetName().Version + " of NuGet. Please upgrade NuGet to the latest version from http://go.microsoft.com/fwlink/?LinkId=213942.");
        }
#endif

        [Fact]
        public void ReadingPackageWithSchemaWithOlderVersionAttribute()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata schemaVersion=""2.0"">
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
  </metadata>
</package>";

            // Act
            var packageBuilder = new PackageBuilder(spec.AsStream(), null);

            // Assert
            Assert.Equal("Artem.XmlProviders", packageBuilder.Id);
            Assert.Equal(NuGetVersion.Parse("2.5"), packageBuilder.Version);
            Assert.Equal("Velio Ivanov", packageBuilder.Authors.Single());
            Assert.Equal("This is the Description (With, Comma-Separated, Words, in Parentheses).", packageBuilder.Description);
            Assert.Equal("en-US", packageBuilder.Language);
        }

        [Fact]
        public void ReadingPackageWithSchemaVersionAttribute()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata schemaVersion=""3.0"">
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
    <references>
        <reference file=""foo.dll"" />
    </references>
  </metadata>
</package>";

            // Act
            var packageBuilder = new PackageBuilder(spec.AsStream(), null);

            // Assert
            Assert.Equal("Artem.XmlProviders", packageBuilder.Id);
            Assert.Equal(NuGetVersion.Parse("2.5"), packageBuilder.Version);
            Assert.Equal("Velio Ivanov", packageBuilder.Authors.Single());
            Assert.Equal("This is the Description (With, Comma-Separated, Words, in Parentheses).", packageBuilder.Description);
            Assert.Equal("en-US", packageBuilder.Language);

            var packageReferenceSet = packageBuilder.PackageAssemblyReferences.Single();
            Assert.Null(packageReferenceSet.TargetFramework);
            Assert.Equal("foo.dll", packageReferenceSet.References.Single());
        }

        [Fact]
        public void ReadingPackageWithSchemaVersionAttributeWithNamespace()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata schemaVersion=""2.0"">
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>This is the Description (With, Comma-Separated, Words, in Parentheses).</description>
    <language>en-US</language>
  </metadata>
</package>";

            // Act
            var packageBuilder = new PackageBuilder(spec.AsStream(), null);

            // Assert
            Assert.Equal("Artem.XmlProviders", packageBuilder.Id);
            Assert.Equal(NuGetVersion.Parse("2.5"), packageBuilder.Version);
            Assert.Equal("Velio Ivanov", packageBuilder.Authors.Single());
            Assert.Equal("This is the Description (With, Comma-Separated, Words, in Parentheses).", packageBuilder.Description);
            Assert.Equal("en-US", packageBuilder.Language);
        }

        [Fact]
        public void CreatingPackageWithUngroupedReference()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
    <metadata>
        <id>SourceDepotClient</id>
        <version>2.8.0.0</version>
           <authors>pranjalg</authors>
           <owners>pranjalg</owners>
           <licenseUrl>http://cbt-userguide/NugetCanUseInLabs.html</licenseUrl>
        <requireLicenseAcceptance>true</requireLicenseAcceptance>
        <description>Source Depot Client assembly with SDApi.</description>
           <copyright>Copyright 2015</copyright>
              <references>
                  <reference file=""SourceDepotClient.dll"" />
               </references>
           </metadata>
       </package>";
            var ms = new MemoryStream();

            // Act
            var packageBuilder = new PackageBuilder(spec.AsStream(), null);
            Manifest.Create(packageBuilder).Save(ms);

            // Assert
            Assert.Equal("SourceDepotClient", packageBuilder.Id);
            Assert.Equal(NuGetVersion.Parse("2.8.0.0"), packageBuilder.Version);
            Assert.Equal("pranjalg", packageBuilder.Authors.Single());
            Assert.Equal("Source Depot Client assembly with SDApi.", packageBuilder.Description);
            Assert.Equal("SourceDepotClient.dll", packageBuilder.PackageAssemblyReferences.First().References.First());

            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
  <metadata>
    <id>SourceDepotClient</id>
    <version>2.8.0</version>
    <authors>pranjalg</authors>
    <owners>pranjalg</owners>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <licenseUrl>http://cbt-userguide/NugetCanUseInLabs.html</licenseUrl>
    <description>Source Depot Client assembly with SDApi.</description>
    <copyright>Copyright 2015</copyright>
    <references>
      <reference file=""SourceDepotClient.dll"" />
    </references>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void MissingMetadataNodeThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>  
</package>";

            // Switch to invariant culture to ensure the error message is in english.
#if !IS_CORECLR
            // REVIEW: Unsupported on CoreCLR
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
#endif

            // Act
#if !IS_CORECLR
            ExceptionAssert.Throws<InvalidOperationException>(
                () => new PackageBuilder(spec.AsStream(), null),
                "The element 'package' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd' has incomplete content. " +
                "List of possible elements expected: 'metadata' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'. " +
                "This validation error occurred in a 'package' element.");
#else
            ExceptionAssert.Throws<InvalidDataException>(() => new PackageBuilder(spec.AsStream(), null), "The required element 'metadata' is missing from the manifest.");
#endif
        }

        [Fact]
        public void PackageBuilderWorksWithFileNamesContainingSpecialCharacters()
        {
            // Arrange
            var fileNames = new[] {
                        @"lib\regular.file.dll",
                        @"lib\name with spaces.dll",
                        @"lib\C#\test.dll",
                        @"content\images\logo123?#78.png",
                        @"content\images\bread&butter.jpg",
                    };

            // Act
            var builder = new PackageBuilder { Id = "test", Version = NuGetVersion.Parse("1.0"), Description = "test" };
            builder.Authors.Add("test");
            foreach (var name in fileNames)
            {
                builder.Files.Add(CreatePackageFile(name.Replace('\\', Path.DirectorySeparatorChar)));
            }

            // Assert
            using (MemoryStream stream = new MemoryStream())
            {
                builder.Save(stream);

                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    var files = archive.GetFiles().OrderBy(s => s).ToArray();

                    // Linux sorts the first two in different order than Windows
                    Assert.Contains<string>(@"[Content_Types].xml", files);
                    Assert.Contains<string>(@"_rels/.rels", files);
                    Assert.Equal(@"content/images/bread&butter.jpg", files[2]);
                    Assert.Equal(@"content/images/logo123?#78.png", files[3]);
                    Assert.Equal(@"lib/C#/test.dll", files[4]);
                    Assert.Equal(@"lib/name with spaces.dll", files[5]);
                    Assert.Equal(@"lib/regular.file.dll", files[6]);

                    Assert.StartsWith(@"package/services/metadata/core-properties/", files[7]);
                    Assert.EndsWith(@".psmdcp", files[7]);

                    Assert.Equal(@"test.nuspec", files[8]);
                }
            }
        }

        private static IPackageFile CreatePackageFile(string name)
        {
            var file = new Mock<IPackageFile>();
            file.SetupGet(f => f.Path).Returns(name);
            file.Setup(f => f.GetStream()).Returns(new MemoryStream());

            string effectivePath;
            var fx = FrameworkNameUtility.ParseFrameworkNameFromFilePath(name, out effectivePath);
            file.SetupGet(f => f.EffectivePath).Returns(effectivePath);
            file.SetupGet(f => f.TargetFramework).Returns(fx);

            return file.Object;
        }

        private Stream GetManifestStream(Stream packageStream)
        {
            Stream resultStream = null;

            using (var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true))
            {
                var entry = archive.GetEntry(@"_rels/.rels");
                using (var stream = entry.Open())
                {
                    using (var streamReader = new StreamReader(stream))
                    {
                        var contents = streamReader.ReadToEnd();
                        var xDoc = XDocument.Parse(contents);

                        var relationshipsNS = "http://schemas.openxmlformats.org/package/2006/relationships";
                        var relationsShipsName = XName.Get("Relationships", relationshipsNS);
                        var relationshipsElements = xDoc.Elements(relationsShipsName);

                        var relationsShipName = XName.Get("Relationship", relationshipsNS);
                        var relationshipElements = relationshipsElements.Elements(relationsShipName);

                        var typeName = XName.Get("Type");
                        var typeAttrs = relationshipElements.Attributes(typeName);

                        var manifestNS = "http://schemas.microsoft.com/packaging/2010/07/manifest";
                        var manifestTypeAttr = typeAttrs.FirstOrDefault(attr => attr.Value == manifestNS);

                        var targetName = XName.Get("Target");
                        var targetPath = manifestTypeAttr.Parent.Attribute(targetName).Value.TrimStart('/');

                        var resultEntry = archive.GetEntry(targetPath);
                        resultStream = resultEntry.Open();
                    }
                }
            }

            return resultStream;
        }
    }
}