// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NuGet.Common.Migrations;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Common.Test
{
    public class MigrationRunnerTests
    {
        [Fact]
        public void Run_WhenExecutedOnSingleThreadThenOneMigrationFileIsCreated_Success()
        {
            // Arrange
            using var testDirectory = TestDirectory.Create();

            string migrationsDirectory = Path.Combine(testDirectory, "migrations");

            // Act
            MigrationRunner.Run(migrationsDirectory);

            // Assert
            Assert.True(Directory.Exists(migrationsDirectory));
            var files = Directory.GetFiles(migrationsDirectory);
            Assert.Equal(1, files.Length);
            Assert.Equal(Path.Combine(migrationsDirectory, "1"), files[0]);
        }

        [Fact]
        public void Run_WhenExecutedInParallelThenOnlyOneMigrationFileIsCreated_Success()
        {
            var threads = new List<Thread>();
            const int numThreads = 5;
            TimeSpan timeout = TimeSpan.FromSeconds(90);

            // Arrange
            using var testDirectory = TestDirectory.Create();

            string migrationsDirectory = Path.Combine(testDirectory, "migrations");

            // Act
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() => MigrationRunner.Run(migrationsDirectory));
                thread.Start();
                threads.Add(thread);
            }

            foreach (var thread in threads)
            {
                thread.Join(timeout);
            }

            // Assert
            Assert.True(Directory.Exists(migrationsDirectory));
            var files = Directory.GetFiles(migrationsDirectory);
            Assert.Equal(1, files.Length);
            Assert.Equal(Path.Combine(migrationsDirectory, "1"), files[0]);
        }

        [Fact]
        public void Run_WhenAThreadAbandonsMutexThenNextMigrationRunReleasesMutexAndCreatesMigrationFile_Success()
        {
            Mutex _orphan = new Mutex(false, "NuGet-Migrations");
            bool signal = false;

            // Arrange
            using var testDirectory = TestDirectory.Create();

            string migrationsDirectory = Path.Combine(testDirectory, "migrations");

            Thread t = new Thread(new ThreadStart(AbandonMutex));
            t.Start();
            t.Join();
            Assert.True(signal, userMessage: "Failed to acquire the mutex.");

            // Act
            MigrationRunner.Run(migrationsDirectory);

            // Assert
            Assert.True(Directory.Exists(migrationsDirectory));
            var files = Directory.GetFiles(migrationsDirectory);
            Assert.Equal(1, files.Length);
            Assert.Equal(Path.Combine(migrationsDirectory, "1"), files[0]);


            void AbandonMutex()
            {
                signal = _orphan.WaitOne(TimeSpan.FromMinutes(1), false);
                // Abandon the mutex by exiting the method without releasing
            }
        }
    }
}
