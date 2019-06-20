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

namespace NuGet.Packaging.Test
{
    public class NoRefOrLibFolderInPackageRuleTests
    {
        [Fact]
        public void Validate_PacakageWithBuildFilesWithoutLibOrRefFiles_ShouldWarn()
        {
            //Arrange
            var nuspecContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
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

            using (var testDirectory = TestDirectory.Create())
            {
                var nuspecPath = Path.Combine(testDirectory, "test.nuspec");
                File.AppendAllText(nuspecPath, nuspecContent);

                Directory.CreateDirectory(Path.Combine(testDirectory, "build"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "build", "random_tfm"));

                var tfmStream = File.Create(Path.Combine(testDirectory, "build", "random_tfm", "test.dll"));
                tfmStream.Dispose();

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

                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var nupkgPath = Path.Combine(testDirectory, "test.1.0.0.nupkg");

                using (var reader = new PackageArchiveReader(nupkgPath))
                {
                    var issues = new List<PackagingLogMessage>();
                    //Act
                    issues.AddRange(rule.Validate(reader).OrderBy(p => p.Code.ToString(), StringComparer.CurrentCulture));

                    //Assert
                    Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5127));
                }
            }
        }
        
        [Fact]
        public void Validate_PacakageWithBuildFilesWithoutLibOrRefFilesWithNativeFileOnly_ShouldNotWarn()
        {
            //Arrange
            var nuspecContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
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

            using (var testDirectory = TestDirectory.Create())
            {
                var nuspecPath = Path.Combine(testDirectory, "test.nuspec");
                File.AppendAllText(nuspecPath, nuspecContent);

                Directory.CreateDirectory(Path.Combine(testDirectory, "build"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "build", "any"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "build", "native"));

                var nativeStream = File.Create(Path.Combine(testDirectory, "build", "native", "test.dll"));
                nativeStream.Dispose();

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

                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var nupkgPath = Path.Combine(testDirectory, "test.1.0.0.nupkg");

                using (var reader = new PackageArchiveReader(nupkgPath))
                {
                    var issues = new List<PackagingLogMessage>();
                    //Act
                    issues.AddRange(rule.Validate(reader).OrderBy(p => p.Code.ToString(), StringComparer.CurrentCulture));

                    //Assert
                    Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
                }
            }
        }
        [Fact]
        public void Validate_PacakageWithBuildFilesWithoutLibOrRefFilesWithAnyFileOnly_ShouldNotWarn()
        {
            //Arrange
            var nuspecContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
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

            using (var testDirectory = TestDirectory.Create())
            {
                var nuspecPath = Path.Combine(testDirectory, "test.nuspec");
                File.AppendAllText(nuspecPath, nuspecContent);

                Directory.CreateDirectory(Path.Combine(testDirectory, "build"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "build", "any"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "build", "native"));

                var anyStream = File.Create(Path.Combine(testDirectory, "build", "any", "test.dll"));
                anyStream.Dispose();

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

                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var nupkgPath = Path.Combine(testDirectory, "test.1.0.0.nupkg");

                using (var reader = new PackageArchiveReader(nupkgPath))
                {
                    var issues = new List<PackagingLogMessage>();
                    //Act
                    issues.AddRange(rule.Validate(reader).OrderBy(p => p.Code.ToString(), StringComparer.CurrentCulture));

                    //Assert
                    Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
                }
            }
        }

        [Fact]
        public void Validate_PackageWithBuildFileWithNativeAndAnyAndTFMFiles_ShouldWarn()
        {
            //Arrange
            var nuspecContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
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

            using (var testDirectory = TestDirectory.Create())
            {
                var nuspecPath = Path.Combine(testDirectory, "test.nuspec");
                File.AppendAllText(nuspecPath, nuspecContent);

                Directory.CreateDirectory(Path.Combine(testDirectory, "build"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "build", "random_tfm"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "build", "any"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "build", "native"));

                var tfmStream = File.Create(Path.Combine(testDirectory, "build", "random_tfm", "test.dll"));
                tfmStream.Dispose();

                var anyStream = File.Create(Path.Combine(testDirectory, "build", "any", "test.dll"));
                anyStream.Dispose();

                var nativeStream = File.Create(Path.Combine(testDirectory, "build", "native", "test.dll"));
                nativeStream.Dispose();

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

                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var nupkgPath = Path.Combine(testDirectory, "test.1.0.0.nupkg");

                using (var reader = new PackageArchiveReader(nupkgPath))
                {
                    var issues = new List<PackagingLogMessage>();

                    //Act
                    issues.AddRange(rule.Validate(reader).OrderBy(p => p.Code.ToString(), StringComparer.CurrentCulture));

                    //Assert
                    Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5127));
                }
            }
        }

        [Fact]
        public void Validate_PackageWithLibFiles_ShouldNotWarn()
        {
            //Arrange
            var nuspecContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
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

            using (var testDirectory = TestDirectory.Create())
            {
                var nuspecPath = Path.Combine(testDirectory, "test.nuspec");
                File.AppendAllText(nuspecPath, nuspecContent);

                Directory.CreateDirectory(Path.Combine(testDirectory, "build"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "lib"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "lib", "random_tfm"));

                var tfmStream = File.Create(Path.Combine(testDirectory, "lib", "random_tfm", "test.dll"));
                tfmStream.Dispose();

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

                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var nupkgPath = Path.Combine(testDirectory, "test.1.0.0.nupkg");

                using (var reader = new PackageArchiveReader(nupkgPath))
                {
                    var issues = new List<PackagingLogMessage>();
                    //Act
                    issues.AddRange(rule.Validate(reader).OrderBy(p => p.Code.ToString(), StringComparer.CurrentCulture));

                    //Assert
                    Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
                }
            }
        }

        [Fact]
        public void Validate_PackageWithRefFiles_ShouldNotWarn()
        {
            //Arrange
            var nuspecContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
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

            using (var testDirectory = TestDirectory.Create())
            {
                var nuspecPath = Path.Combine(testDirectory, "test.nuspec");
                File.AppendAllText(nuspecPath, nuspecContent);

                Directory.CreateDirectory(Path.Combine(testDirectory, "build"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "ref"));
                Directory.CreateDirectory(Path.Combine(testDirectory, "ref", "random_tfm"));

                var tfmStream = File.Create(Path.Combine(testDirectory, "ref", "random_tfm", "test.dll"));
                tfmStream.Dispose();

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

                var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
                var nupkgPath = Path.Combine(testDirectory, "test.1.0.0.nupkg");

                using (var reader = new PackageArchiveReader(nupkgPath))
                {
                    var issues = new List<PackagingLogMessage>();
                    //Act
                    issues.AddRange(rule.Validate(reader).OrderBy(p => p.Code.ToString(), StringComparer.CurrentCulture));
                   
                    //Assert
                    Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
                }
            }
        }
    }
}
