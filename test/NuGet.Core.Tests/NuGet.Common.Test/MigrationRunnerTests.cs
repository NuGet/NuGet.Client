// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common.Migrations;
using Xunit;

namespace NuGet.Common.Test
{
    [CollectionDefinition("MigrationRunner", DisableParallelization = true)]
    public class MigrationRunnerTests
    {
        [Fact]
        public void WhenExecutedInParallelOnlyOneFileIsCreatedForEveryMigration_Success()
        {
            var threads = new List<Thread>();
            int numThreads = 5;
            int timeoutInSeconds = 90;

            // Arrange
            string directory = MigrationRunner.GetMigrationsDirectory();
            if (Directory.Exists(directory))
                Directory.Delete(path: directory, recursive: true);

            // Act
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(MigrationRunner.Run);
                thread.Start();
                threads.Add(thread);
            }

            foreach (var thread in threads)
            {
                thread.Join(timeout: TimeSpan.FromSeconds(timeoutInSeconds));
            }

            // Assert
            Assert.True(Directory.Exists(directory));
            Assert.Equal(1, Directory.GetFiles(directory).Length);
        }
    }
}
