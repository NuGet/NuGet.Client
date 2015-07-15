using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetDeleteCommandTest
    {
        // Tests deleting a package from a source that is a file system directory.
        [Fact]
        public void DeleteCommand_DeleteFromFileSystemSource()
        {
            var targetDir = ConfigurationManager.AppSettings["TargetDir"];
            var nugetexe = Path.Combine(targetDir, "nuget.exe");
            var tempPath = Path.GetTempPath();
            var source = Path.Combine(tempPath, Guid.NewGuid().ToString());
            try
            {
                // Arrange            
                Util.CreateDirectory(source);
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
            finally
            {
                // Cleanup
                Util.DeleteDirectory(source);
            }
        }

        // Same as DeleteCommand_DeleteFromFileSystemSource, except that the directory is specified
        // in unix style.
        [Fact]
        public void DeleteCommand_DeleteFromFileSystemSourceUnixStyle()
        {
            var targetDir = ConfigurationManager.AppSettings["TargetDir"];
            var nugetexe = Path.Combine(targetDir, "nuget.exe");
            var tempPath = Path.GetTempPath();
            var source = Path.Combine(tempPath, Guid.NewGuid().ToString());
            source = source.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            try
            {
                // Arrange
                Util.CreateDirectory(source);
                var packageFileName = Util.CreateTestPackage("testPackage1", "1.1.0", source);
                Assert.True(File.Exists(packageFileName));

                // Act
                string[] args = new string[] {
                    "delete", "testPackage1", "1.1.0",
                    "-Source", source, "-NonInteractive" };
                var r = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.False(File.Exists(packageFileName));
            }
            finally
            {
                // Cleanup
                Util.DeleteDirectory(source);
            }
        }

        [Fact]
        public void DeleteCommand_DeleteFromHttpSource()
        {
            var targetDir = ConfigurationManager.AppSettings["TargetDir"];
            var nugetexe = Path.Combine(targetDir, "nuget.exe");
            var tempPath = Path.GetTempPath();
            var mockServerEndPoint = "http://localhost:1234/";

            // Arrange
            var server = new MockServer(mockServerEndPoint);
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
                    "-Source", mockServerEndPoint + "nuget", "-NonInteractive" };
            var r = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                string.Join(" ", args),
                waitForExit: true);
            server.Stop();

            // Assert
            Assert.Equal(0, r.Item1);
            Assert.True(deleteRequestIsCalled);
        }
    }
}
