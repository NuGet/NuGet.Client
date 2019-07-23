// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class GetRestorePackageDownloadsTaskTests
    {
        [Fact]
        public void Execute_CheckAllMetadata()
        {
            // Arrange
            var buildEngine = new TestBuildEngine();

            var packageX = new TaskItem();
            packageX.ItemSpec = "x";
            packageX.SetMetadata("Version", "[1.0.0]");

            var packageDownloads = new List<ITaskItem>()
            {
                packageX
            };

            var task = new GetRestorePackageDownloadsTask()
            {
                BuildEngine = buildEngine,
                ProjectUniqueName = "MyProj",
                TargetFrameworks = "netstandard2.0",
                PackageDownloads = packageDownloads.ToArray()
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result, "Task failed");
            Assert.Equal(1, task.RestoreGraphItems.Length);
            Assert.Equal("x", task.RestoreGraphItems[0].GetMetadata("Id"));
            Assert.Equal(task.ProjectUniqueName, task.RestoreGraphItems[0].GetMetadata("ProjectUniqueName"));
            Assert.Equal("DownloadDependency", task.RestoreGraphItems[0].GetMetadata("Type"));
            Assert.Equal("[1.0.0]", task.RestoreGraphItems[0].GetMetadata("VersionRange"));
            Assert.Equal("netstandard2.0", task.RestoreGraphItems[0].GetMetadata("TargetFrameworks"));
        }

        [Fact]
        public void Execute_UseFirstVersionPerId()
        {
            // Arrange
            var buildEngine = new TestBuildEngine();

            var packageX1 = new TaskItem();
            packageX1.ItemSpec = "x";
            packageX1.SetMetadata("Version", "[1.0.0]");

            var packageX2 = new TaskItem();
            packageX2.ItemSpec = "x";
            packageX2.SetMetadata("Version", "[2.0.0]");

            var packageDownloads = new List<ITaskItem>()
            {
                packageX1,
                packageX2
            };

            var task = new GetRestorePackageDownloadsTask()
            {
                BuildEngine = buildEngine,
                ProjectUniqueName = "MyProj",
                TargetFrameworks = "netstandard2.0",
                PackageDownloads = packageDownloads.ToArray()
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result, "Task failed");
            Assert.Equal(1, task.RestoreGraphItems.Length);
            Assert.Equal("x", task.RestoreGraphItems[0].GetMetadata("Id"));
            Assert.Equal("[1.0.0]", task.RestoreGraphItems[0].GetMetadata("VersionRange"));
        }

        [Fact]
        public void Execute_AllowMultiRangeVersion()
        {
            // Arrange
            var buildEngine = new TestBuildEngine();

            var packageX = new TaskItem();
            packageX.ItemSpec = "x";
            packageX.SetMetadata("Version", "[1.0.0];[2.0.0]");

            var packageDownloads = new List<ITaskItem>()
            {
                packageX
            };

            var task = new GetRestorePackageDownloadsTask()
            {
                BuildEngine = buildEngine,
                ProjectUniqueName = "MyProj",
                TargetFrameworks = "netstandard2.0",
                PackageDownloads = packageDownloads.ToArray()
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result, "Task failed");
            Assert.Equal(1, task.RestoreGraphItems.Length);
            Assert.Equal("x", task.RestoreGraphItems[0].GetMetadata("Id"));
            Assert.Equal("[1.0.0];[2.0.0]", task.RestoreGraphItems[0].GetMetadata("VersionRange"));
        }

        [Fact]
        public void Execute_AllowMultiplePackages()
        {
            // Arrange
            var buildEngine = new TestBuildEngine();

            var packageX = new TaskItem();
            packageX.ItemSpec = "x";
            packageX.SetMetadata("Version", "[1.0.0]");

            var packageY = new TaskItem();
            packageY.ItemSpec = "y";
            packageY.SetMetadata("Version", "[2.0.0]");

            var packageDownloads = new List<ITaskItem>()
            {
                packageX,
                packageY
            };

            var task = new GetRestorePackageDownloadsTask()
            {
                BuildEngine = buildEngine,
                ProjectUniqueName = "MyProj",
                TargetFrameworks = "netstandard2.0",
                PackageDownloads = packageDownloads.ToArray()
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result, "Task failed");
            Assert.Equal(2, task.RestoreGraphItems.Length);

            var package = task.RestoreGraphItems.SingleOrDefault(i => i.GetMetadata("Id") == "x");
            Assert.NotNull(package);
            Assert.Equal("[1.0.0]", package.GetMetadata("VersionRange"));

            package = task.RestoreGraphItems.SingleOrDefault(i => i.GetMetadata("Id") == "y");
            Assert.NotNull(package);
            Assert.Equal("[2.0.0]", package.GetMetadata("VersionRange"));
        }
    }
}
