using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class LocalResourceUtilsTest
    {
        [WindowsNTFact]
        public void DeleteDirectoryTreeDeletesReparsePointsButNotReparsePointTargets()
        {
            // This test creates a cached package and a subdirectory in the working directory.
            // The subdirectory is a reparse point linked to the target directory.

            // Deleting the directory tree should delete the reparse point, but not the target of the reparse point.
            // The cached package in the working directory should also be deleted.

            // The also creates a dummy file in the linked target directory, and verifies
            // this file is left as-is after deletion of the directory tree including the reparse point.

            // Finally, test clean-up is verified.

            var failedDeletes = new List<string>();

            string targetDirectoryPath;
            string fileInTargetDirectory;
            using (var targetDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                targetDirectoryPath = targetDirectory.Path;
                var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder();
                var subDirectoryPath = Path.Combine(workingDirectory.Path, "SubDirectory");
                var subDirectory = Directory.CreateDirectory(subDirectoryPath);

                Util.CreatePackage(workingDirectory.Path, Guid.NewGuid().ToString("N"), "1.0.0");
                fileInTargetDirectory = Path.Combine(targetDirectoryPath, "test.txt");
                File.WriteAllText(fileInTargetDirectory, string.Empty);

                Util.CreateJunctionPoint(subDirectory.FullName, targetDirectoryPath, overwrite: true);

                // Act
                LocalResourceUtils.DeleteDirectoryTree(workingDirectory.Path, failedDeletes);

                // Assert
                Assert.Empty(failedDeletes);
                Assert.False(Directory.Exists(workingDirectory.Path));
                Assert.True(Directory.Exists(targetDirectoryPath));
                Assert.True(File.Exists(fileInTargetDirectory));
            }

            // Verify clean-up
            Assert.False(Directory.Exists(targetDirectoryPath));
            Assert.False(File.Exists(fileInTargetDirectory));
        }
    }

    /// <summary>
    /// This attribute ensures the Fact is only run on Windows.
    /// </summary>
    public class WindowsNTFactAttribute
        : FactAttribute
    {
        public WindowsNTFactAttribute()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Skip = "Test only runs on Windows NT or later.";
            }
        }
    }
}