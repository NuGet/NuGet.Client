// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Test.Utility;
using System.IO;
using Xunit;
using System.Diagnostics;

namespace NuGet.Common.Test
{
    public class DirectoryUtilityTests
    {
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

        private string StatPermissions(string path)
        {
            string permissions;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                Arguments = "-c %a " + path,
                FileName = "stat"
            };

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
