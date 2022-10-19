// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Common.Migrations;
using NuGet.PackageManagement;
using Xunit;

namespace NuGet.Common.Test
{
    [CollectionDefinition("MigrationRunner", DisableParallelization = true)]
    public class MigrationRunnerTests
    {
        [Fact]
        public async Task WhenExecutedInParallelOnlyOneFileIsCreatedForEveryMigration_SuccessAsync()
        {
            var tasks = new List<Task>();

            // Arrange
            string directory = MigrationRunner.GetMigrationsDirectory();
            if (Directory.Exists(directory))
                Directory.Delete(path: directory, recursive: true);

            // Act
            for(int count = 0; count < 5; count++)           
                tasks.Add(Task.Run(() => MigrationRunner.Run()));  

            Task t = Task.WhenAll(tasks);
            await t;

            // Assert
            Assert.True(t.IsCompleted);
            Assert.Equal(TaskStatus.RanToCompletion, t.Status);
            Assert.True(Directory.Exists(directory));
            Assert.Equal(1, Directory.GetFiles(directory).Length);
        }
    }
}
