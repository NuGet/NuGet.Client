using System;
using System.Collections.Concurrent;
using System.IO;
using NuGet.ProjectModel;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreProjectJsonTest : IDisposable
    {
        [Fact]
        public void RestoreProjectJson_IsLockedTrueAfterRestore()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            _dirs.TryAdd(tempPath, false);

            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
            Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0',
                                            'packageB': '2.2.0'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            // Restore once to get a lock file
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            // Set IsLocked=true
            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();
            var lockFile = lockFileFormat.Read(lockFilePath);
            lockFile.IsLocked = true;
            lockFileFormat.Write(lockFilePath, lockFile);

            // Act
            // Restore using the locked lock file
            r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFileAfter = lockFileFormat.Read(lockFilePath);

            // Assert
            Assert.True(lockFileAfter.IsLocked);
            Assert.True(lockFile.Equals(lockFileAfter));
        }

        [Fact]
        public void RestoreProjectJson_CorruptedLockFile()
        {
            // Arrange
            var tempPath = Path.GetTempPath();
            _dirs.TryAdd(tempPath, false);

            var workingPath = Path.Combine(tempPath, Guid.NewGuid().ToString());
            var repositoryPath = Path.Combine(workingPath, Guid.NewGuid().ToString());
            var currentDirectory = Directory.GetCurrentDirectory();
            var nugetexe = Util.GetNuGetExePath();

            Util.CreateDirectory(workingPath);
            Util.CreateDirectory(repositoryPath);
            Util.CreateDirectory(Path.Combine(workingPath, ".nuget"));
            Util.CreateTestPackage("packageA", "1.1.0", repositoryPath);
            Util.CreateTestPackage("packageB", "2.2.0", repositoryPath);
            Util.CreateFile(workingPath, "project.json",
                                            @"{
                                            'dependencies': {
                                            'packageA': '1.1.0',
                                            'packageB': '2.2.0'
                                            },
                                            'frameworks': {
                                                        'uap10.0': { }
                                                    }
                                            }");

            string[] args = new string[] {
                "restore",
                "-Source",
                repositoryPath,
                "-solutionDir",
                workingPath,
                "project.json"
            };

            var lockFilePath = Path.Combine(workingPath, "project.lock.json");
            var lockFileFormat = new LockFileFormat();
            using (var writer = new StreamWriter(lockFilePath))
            {
                writer.WriteLine("{ \"CORRUPTED!\": \"yep\"");
            }

            // Act
            var r = CommandRunner.Run(
                nugetexe,
                workingPath,
                string.Join(" ", args),
                waitForExit: true);

            var lockFile = lockFileFormat.Read(lockFilePath);

            // Assert
            // If the library count can be obtained then a new lock file was created
            Assert.Equal(2, lockFile.Libraries.Count);
        }

        /// <summary>
        /// Store all directories used by the unit tests and clean them up at the end during Dispose()
        /// </summary>
        private ConcurrentDictionary<string, bool> _dirs = new ConcurrentDictionary<string, bool>();

        public void Dispose()
        {
            foreach (var dir in _dirs.Keys)
            {
                try
                {
                    Util.DeleteDirectory(dir);
                }
                catch
                {

                }
            }
        }
    }
}
