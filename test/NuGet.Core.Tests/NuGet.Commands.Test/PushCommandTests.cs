﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class PushCommandTests
    {
        [Fact]
        public async Task PushCommand_AbsolutePathSource()
        {
            using (TestDirectory workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange (create a test package)
                DirectoryInfo packagePushDest = new DirectoryInfo(Path.Combine(workingDir, "packagePushDest"));
                packagePushDest.Create();

                List<PackageSource> packageSources = new List<PackageSource>();
                packageSources.Add(new PackageSource(packagePushDest.FullName));

                FileInfo packageInfo = SimpleTestPackageUtility.CreateFullPackage(workingDir, "test", "1.0.0");

                // Act
                await PushRunner.Run(
                    Settings.LoadDefaultSettings(null, null, null),
                    new TestPackageSourceProvider(packageSources),
                    packageInfo.FullName,
                    packagePushDest.FullName,
                    null, // api key
                    0, // timeout
                    false, // disable buffering
                    false, // no symbols
                    new TestLogger());

                // Assert
                string destFile = Path.Combine(packagePushDest.FullName, packageInfo.Name);
                Assert.Equal(true, File.Exists(destFile));
            }
        }
    }
}
