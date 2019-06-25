using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Commands;
using NuGet.Test.Utility;
using Xunit;
using NuGet.Packaging.Rules;
using Moq;
using System.Xml.Linq;
using System.Collections;
using System.Runtime.CompilerServices;

namespace NuGet.Packaging.Test
{
    public class NoRefOrLibFolderInPackageRuleTests
    {
        string _nuspecContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
"<package xmlns=\"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\">" +
"   <metadata>" +
"        <id>test</id>" +
"        <version>1.0.0</version>" +
"        <authors>Unit Test</authors>" +
"        <description>Sample Description</description>" +
"        <language>en-US</language>" +
"    <dependencies>" +
"      <dependency id=\"System.Collections.Immutable\" version=\"4.3.0\" />" +
"    </dependencies>" +
"    </metadata>" +
"</package>";

        [Fact]
        public void Validate_PacakageWithBuildFilesWithoutLibOrRefFiles_ShouldWarn()
        {
            //Arrange
            var files = new[]
            {
                "build/random_tfm/test.dll",
            };

            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }
        
        [Fact]
        public void Validate_PacakageWithBuildFilesWithoutLibOrRefFilesWithNativeFileOnly_ShouldNotWarn()
        {
            //Arrange
            var files = new[]
            {
                "build/native/test.dll"
            };

            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }
        [Fact]
        public void Validate_PacakageWithBuildFilesWithoutLibOrRefFilesWithAnyFileOnly_ShouldNotWarn()
        {
            //Arrange
            var files = new[]
            {
                "build/any/test.dll"
            };

            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }

        [Fact]
        public void Validate_PackageWithBuildFileWithNativeAndAnyAndTFMFiles_ShouldWarn()
        {
            //Arrange
            var files = new[]
            {
                "build/random_tfm/test.dll",
                "build/any/test.dll",
                "build/native/test.dll"
            };

            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }

        [Fact]
        public void Validate_PackageWithLibFiles_ShouldNotWarn()
        {
            // Assemble 
            var files = new[] { "lib/random_tfm/test.dll" };
            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }

        [Fact]
        public void Validate_PackageWithRefFiles_ShouldNotWarn()
        {
            //Arrange
            var files = new[]
            {
                "ref/random_tfm/test.dll"
            };

            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }

        [Fact]
        public void Validate_PackageWithBuildFolderAndLibFolder_ShouldNotWarn()
        {
            var files = new[]
            {
                "build/random_tfm/test.dll",
                "lib/random_tfm/test.dll"
            };

            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }

        [Fact]
        public void Validate_PackageWithBuildAndRefFolder_ShouldNotWarn()
        {
            var files = new[]
            {
                "build/random_tfm/test.dll",
                "ref/random_tfm/test.dll"
            };

            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }

        [Fact]
        public void Validate_PackageWithBuildAndContentFolder_ShouldWarn()
        {
            var files = new[]
            {
                "build/random_tfm/test.dll",
                "content/random_tfm/test.dll"
            };

            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }

        [Fact]
        public void Validate_PackageWithBuildFolderAndFile_ShouldWarn()
        {
            var files = new[]
            {
                "build/random_tfm/test.dll",
                "test.dll"
            };

            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }

        [Fact]
        public void Validate_PackageWithLibFolderAndFile_ShouldNotWarn()
        {
            var files = new[]
            {
                "lib/random_tfm/test.dll",
                "test.dll"
            };

            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }

        [Fact]
        public void Validate_PackageWithRefFolderAndFile_ShouldNotWarn()
        {
            var files = new[]
            {
                "ref/random_tfm/test.dll",
                "test.dll"
            };

            using (var testContext = TestPackageFile.Create(files, _nuspecContent))
            {
                // Act
                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var issues = rule.Validate(testContext.PackageArchiveReader).ToList();

                // Assert
                Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
            }
        }
    }

    internal class TestPackageFile : IDisposable
    {
        private TestPackageFile(PackageArchiveReader reader)
        {
            PackageArchiveReader = reader;
        }

        public static TestPackageFile Create(IEnumerable<string> files, string nuspecContent)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var nuspecPath = Path.Combine(testDirectory, "test.nuspec");
                File.AppendAllText(nuspecPath, nuspecContent);

                foreach(var file in files)
                {
                    Directory.CreateDirectory(Path.Combine(testDirectory, Path.GetDirectoryName(file)));

                    var tfmStream = File.Create(Path.Combine(testDirectory, file));
                    tfmStream.Dispose();
                }

                var builder = new PackageBuilder();
                var runner = new PackCommandRunner(
                    new PackArgs
                    {
                        CurrentDirectory = testDirectory,
                        OutputDirectory = testDirectory,
                        Path = nuspecPath,
                        Exclude = Array.Empty<string>(),
                        Symbols = true,
                        Logger = NullLogger.Instance
                    },
                    MSBuildProjectFactory.ProjectCreator,
                    builder);

                runner.BuildPackage();
                var nupkgPath = Path.Combine(testDirectory, "test.1.0.0.nupkg");

                return new TestPackageFile(new PackageArchiveReader(nupkgPath));
            }
        }

        public void Dispose()
        {
            PackageArchiveReader.Dispose();
        }

        public PackageArchiveReader PackageArchiveReader { get; private set; }

    }
}
