// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace NuGet.Common.Migrations
{
    public static class MigrationRunner
    {
        private static readonly IReadOnlyList<Action> Migrations = new List<Action>()
        {
            Migration1.Run
        };
        private const string MaxMigrationFilename = "1";

        public static void Run()
        {
            // since migrations run once per machine, optimize for the scenario where the migration has already run
            Debug.Assert(MaxMigrationFilename == Migrations.Count.ToString(CultureInfo.InvariantCulture));

            string migrationsDirectory = GetMigrationsDirectory();
            var expectedMigrationFilename = Path.Combine(migrationsDirectory, MaxMigrationFilename);

            if (!File.Exists(expectedMigrationFilename))
            {
                // Multiple processes or threads might be trying to call this concurrently (especially via NuGetSdkResolver)
                // so use a global mutex and then check if someone else already did the work.
                using (var mutex = new Mutex(false, "NuGet-Migrations"))
                {
                    if (WaitForMutex(mutex))
                    {
                        if (!File.Exists(expectedMigrationFilename))
                        {
                            // Only run migrations that have not already been run
                            int highestMigrationRun = GetHighestMigrationRun(migrationsDirectory);
                            for (int i = highestMigrationRun + 1; i < Migrations.Count; i++)
                            {
                                try
                                {
                                    Migrations[i]();
                                    // Create file for every migration run, so that if an older version of NuGet is run, it doesn't try to run
                                    // migrations again.
                                    string migrationFile = Path.Combine(migrationsDirectory, (i + 1).ToString(CultureInfo.InvariantCulture));
                                    File.WriteAllText(migrationFile, string.Empty);
                                }
                                catch { }
                            }
                        }
                        mutex.ReleaseMutex();
                    }
                }
            }

            static bool WaitForMutex(Mutex mutex)
            {
                bool captured;

                try
                {
                    captured = mutex.WaitOne(TimeSpan.FromMinutes(1), false);
                }
                catch (AbandonedMutexException)
                {
                    captured = true;
                }

                return captured;
            }
        }

        internal static string GetMigrationsDirectory()
        {
            string migrationsDirectory;
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                migrationsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NuGet", "Migrations");
            }
            else
            {
                var XdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                migrationsDirectory = string.IsNullOrEmpty(XdgDataHome)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "NuGet", "Migrations")
                    : Path.Combine(XdgDataHome, "NuGet", "Migrations");
            }
            Directory.CreateDirectory(migrationsDirectory);
            return migrationsDirectory;
        }

        private static int GetHighestMigrationRun(string directory)
        {
            for (int i = Migrations.Count - 1; i >= 0; --i)
            {
                var filename = Path.Combine(directory, (i + 1).ToString(CultureInfo.InvariantCulture));
                if (File.Exists(filename))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
