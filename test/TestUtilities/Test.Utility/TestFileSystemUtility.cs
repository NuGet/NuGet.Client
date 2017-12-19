// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NuGet.Test.Utility
{
    public static class TestFileSystemUtility
    {
        private static readonly Lazy<string> _root = new Lazy<string>(() => GetRootDirectory());
        private static readonly Lazy<bool> _skipCleanUp = new Lazy<bool>(() => SkipCleanUp());

        /// <summary>
        /// Root test folder where temporary test outputs should go.
        /// </summary>
        public static string NuGetTestFolder => _root.Value;

        private static bool SkipCleanUp()
        {
            // Option to leave files around for debugging
            var val = Environment.GetEnvironmentVariable("NUGET_PERSIST_TESTFOLDERS");

            if (StringComparer.OrdinalIgnoreCase.Equals(val, "true"))
            {
                return true;
            }

            return false;
        }

        private static string GetRootDirectory()
        {
            var repoRoot = GetRepositoryRoot();

            // Default for tests outside of the repo
            var path = Path.Combine(Path.GetTempPath(), "NuGetTestFolder");

            if (repoRoot != null)
            {
                path = Path.Combine(repoRoot, ".test");
                Directory.CreateDirectory(path);
                path = Path.Combine(path, "work");
            }

            Directory.CreateDirectory(path);
            return path;
        }

        private static string GetRepositoryRoot()
        {
            var assemblyPath = new FileInfo(typeof(TestFileSystemUtility).GetTypeInfo().Assembly.Location);
            var currentDir = assemblyPath.Directory;

            while (currentDir != null)
            {
                if (currentDir.GetFiles().Any(e => e.Name.Equals("build.ps1", StringComparison.OrdinalIgnoreCase)))
                {
                    // We have found the repo root.
                    break;
                }

                currentDir = currentDir.Parent;
            }

            return currentDir?.FullName;
        }

        public static bool DeleteRandomTestFolder(string randomTestPath)
        {
            // Avoid cleaning up test folders if 
            if (!_skipCleanUp.Value && Directory.Exists(randomTestPath))
            {
                AssertNotTempPath(randomTestPath);

                try
                {
                    Directory.Delete(randomTestPath, recursive: true);
                }
                catch
                {
                    // Failure
                    return false;
                }
            }

            // Success or skipped
            return true;
        }

        public static void AssertNotTempPath(string path)
        {
            var expanded = Path.GetFullPath(path).TrimEnd(new char[] { '\\', '/' });
            var expandedTempPath = Path.GetFullPath(Path.GetTempPath()).TrimEnd(new char[] { '\\', '/' });

            if (expanded.Equals(expandedTempPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Trying to delete the temp folder in a test");
            }

            if (expanded.Equals(Path.GetFullPath(NuGetTestFolder), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Trying to delete the root test folder in a test");
            }
        }

        private class ResetDirectory : IDisposable
        {
            public string OldPath { get; set; }

            void IDisposable.Dispose()
            {
                Directory.SetCurrentDirectory(OldPath);
            }
        }

        public static IDisposable SetCurrentDirectory(string path)
        {
            var oldPath = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(path);

            return new ResetDirectory()
            {
                OldPath = oldPath
            };
        }
    }
}
