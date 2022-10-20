// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
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

            // Arrange
            string directory = MigrationRunner.GetMigrationsDirectory();
            if (Directory.Exists(directory))
                Directory.Delete(path: directory, recursive: true);

            // Act
            for (int count = 0; count < 5; count++)
            {
                var thread = new Thread(MigrationRunner.Run);
                thread.Start();
                threads.Add(thread);
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.True(Directory.Exists(directory));
            Assert.Equal(1, Directory.GetFiles(directory).Length);
        }
    }
}
