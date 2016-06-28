using System;
using System.IO;
using System.Net;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetDeleteCommandTest
    {
        // Tests deleting a package from a source that is a file system directory.
        [Fact]
        public void DeleteCommand_DeleteFromV2FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", source);
                Assert.True(File.Exists(packageFileName));

                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.False(File.Exists(packageFileName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteReadOnlyFromV2FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", source);
                Assert.True(File.Exists(packageFileName));
                File.SetAttributes(packageFileName,
                    File.GetAttributes(packageFileName) | FileAttributes.ReadOnly);
                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.False(File.Exists(packageFileName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteFromV3FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestFileSystemUtility.CreateRandomTestFolder())
            {
                //drop dummy artifacts to make it a V3
                var dummyPackageName = "foo";
                var version = "1.0.0";
                var packageFolder = Directory.CreateDirectory(Path.Combine(source.Path, dummyPackageName));
                var packageVersionFolder = Directory.CreateDirectory(Path.Combine(packageFolder.FullName, "1.0.0"));
                File.WriteAllText(Path.Combine(packageVersionFolder.FullName, dummyPackageName + ".nuspec"), "dummy text");
                Assert.True(Directory.Exists(packageVersionFolder.FullName));
                // Act
                string[] args = new string[] {
                    "delete", "foo", version,
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                //The specific version folder should be gone.
                Assert.False(Directory.Exists(packageVersionFolder.FullName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteReadOnlyFileFromV3FileSystemSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var source = TestFileSystemUtility.CreateRandomTestFolder())
            {
                //drop dummy artifacts to make it a V3
                var dummyPackageName = "foo";
                var version = "1.0.0";
                var packageFolder = Directory.CreateDirectory(Path.Combine(source.Path, dummyPackageName));
                var packageVersionFolder = Directory.CreateDirectory(Path.Combine(packageFolder.FullName, "1.0.0"));
                var dummyNuspec = Path.Combine(packageVersionFolder.FullName, dummyPackageName + ".nuspec");
                File.WriteAllText(dummyNuspec, "dummy text");
                File.SetAttributes(dummyNuspec, File.GetAttributes(dummyNuspec) | FileAttributes.ReadOnly);
                // Act
                string[] args = new string[] {
                    "delete", "foo", version,
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    String.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.False(Directory.Exists(packageVersionFolder.FullName));
            }
        }

        // Same as DeleteCommand_DeleteFromFileSystemSource, except that the directory is specified
        // in unix style.
        [Fact]
        public void DeleteCommand_DeleteFromFileSystemSourceUnixStyle()
        {
            var nugetexe = Util.GetNuGetExePath();

            using (var windowsSource = TestFileSystemUtility.CreateRandomTestFolder())
            {
                string source = ((string)windowsSource).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Arrange
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", windowsSource);
                Assert.True(File.Exists(packageFileName));

                // Act
                string[] args = new string[] {
                    "delete",
                    "testPackage1",
                    "1.1.0",
                    "-Source",
                    source,
                    "-NonInteractive" };

                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    $"delete testPackage1 1.1.0 -Source {source} -NonInteractive",
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.False(File.Exists(packageFileName));
            }
        }

        [Fact]
        public void DeleteCommand_DeleteFromHttpSource()
        {
            var nugetexe = Util.GetNuGetExePath();

            // Arrange
            using (var server = new MockServer())
            {
                server.Start();
                bool deleteRequestIsCalled = false;

                server.Delete.Add("/nuget/testPackage1/1.1", request =>
                {
                    deleteRequestIsCalled = true;
                    return HttpStatusCode.OK;
                });

                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", server.Uri + "nuget", "-NonInteractive" };

                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.True(deleteRequestIsCalled);
            }
        }

        [Fact]
        public void DeleteCommand_WithApiKeyAsThirdArgument()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();
            using (var server = new MockServer())
            {
                server.Start();
                var deleteRequestIsCalled = false;
                var expectedApiKey = "SOME_API_KEY";
                string actualApiKey = null;

                server.Delete.Add("/nuget/testPackage1/1.1", request =>
                {
                    deleteRequestIsCalled = true;
                    actualApiKey = request.Headers["X-NuGet-ApiKey"];

                    return HttpStatusCode.OK;
                });

                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0", expectedApiKey,
                    "-Source", server.Uri + "nuget", "-NonInteractive" };

                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.True(deleteRequestIsCalled);
                Assert.Equal(expectedApiKey, actualApiKey);
            }
        }

        [Fact]
        public void DeleteCommand_WithApiKeyAsNamedArgument()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();
            using (var server = new MockServer())
            {
                server.Start();
                var deleteRequestIsCalled = false;
                var expectedApiKey = "SOME_API_KEY";
                string actualApiKey = null;

                server.Delete.Add("/nuget/testPackage1/1.1", request =>
                {
                    deleteRequestIsCalled = true;
                    actualApiKey = request.Headers["X-NuGet-ApiKey"];

                    return HttpStatusCode.OK;
                });

                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "should-be-ignored",  // The named argument is preferred over the positional argument.
                    "-ApiKey", expectedApiKey,
                    "-Source", server.Uri + "nuget", "-NonInteractive" };

                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.True(deleteRequestIsCalled);
                Assert.Equal(expectedApiKey, actualApiKey);
            }
        }
    }
}
