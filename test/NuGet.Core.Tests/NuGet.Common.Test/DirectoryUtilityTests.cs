// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Common.Test
{
    public class DirectoryUtilityTests
    {
        [PlatformTheory(Platform.Darwin, Platform.Linux)]
        [InlineData("áéíóúäëïöü")]
        [InlineData("ഔ")]
        public void CreateSharedDirectory_WithUnicodeChars_CreatesDirectory(string dirPath)
        {
            // Arrange
            using TestDirectory testDirectory = TestDirectory.Create();
            string dirWithUnicode = Path.Combine(testDirectory, dirPath);

            // Act
            DirectoryUtility.CreateSharedDirectory(dirWithUnicode);

            // Assert
            Assert.Equal("777", StatPermissions(dirWithUnicode));
        }

        [Fact]
        public void DirectoryUtility_CreateSharedDirectory_BasicSuccess()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var parentDir = Path.Combine(testDirectory, "parent");
                var childDir = Path.Combine(parentDir, "child");

                // Act
                DirectoryUtility.CreateSharedDirectory(childDir);

                // Assert
                Assert.True(Directory.Exists(parentDir));
                Assert.True(Directory.Exists(childDir));
                if (!RuntimeEnvironmentHelper.IsWindows)
                {
                    Assert.Equal("777", StatPermissions(parentDir));
                    Assert.Equal("777", StatPermissions(childDir));
                }
            }
        }

        [Fact]
        public void DirectoryUtility_CreateSharedDirectory_Idempotent()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Arrange
                var parentDir = Path.Combine(testDirectory, "parent");
                var childDir = Path.Combine(parentDir, "child");
                DirectoryUtility.CreateSharedDirectory(childDir);

                // Act
                DirectoryUtility.CreateSharedDirectory(childDir);

                // Assert
                Assert.True(Directory.Exists(parentDir));
                Assert.True(Directory.Exists(childDir));
            }
        }

        private string StatPermissions(string path)
        {
            string permissions;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                FileName = "stat"
            };
            if (RuntimeEnvironmentHelper.IsLinux)
            {
                startInfo.Arguments = "-c %a " + path;
            }
            else
            {
                startInfo.Arguments = "-f %A " + path;
            }

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;

                process.Start();
                permissions = process.StandardOutput.ReadLine();

                process.WaitForExit();
            }

            return permissions;
        }
    }
}
