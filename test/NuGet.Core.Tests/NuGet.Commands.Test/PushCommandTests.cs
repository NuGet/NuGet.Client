// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        public async Task PushCommand_AbsolutePathSourceAsync()
        {
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange (create a test package)
                var packagePushDest = new DirectoryInfo(Path.Combine(workingDir, "packagePushDest"));
                packagePushDest.Create();

                var packageSources = new List<PackageSource>
                {
                    new PackageSource(packagePushDest.FullName)
                };

                var packageInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(workingDir, "test", "1.0.0");

                // Act
                await PushRunner.Run(
                    Settings.LoadDefaultSettings(null, null, null),
                    new TestPackageSourceProvider(packageSources),
                    packageInfo.FullName,
                    packagePushDest.FullName,
                    null, // api key
                    null, // symbols source
                    null, // symbols api key
                    0, // timeout
                    false, // disable buffering
                    false, // no symbols,
                    false, // no continue on duplicate
                    false, // no continue on invalid
                    false, // enable server endpoint
                    new TestLogger());

                // Assert
                var destFile = Path.Combine(packagePushDest.FullName, packageInfo.Name);
                Assert.Equal(true, File.Exists(destFile));
            }
        }
    }
}
