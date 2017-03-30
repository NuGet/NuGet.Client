// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Test.Utility
{
    public static class TestFileSystemUtility
    {
        public static readonly string NuGetTestFolder =
            Path.Combine(Path.GetTempPath(), "NuGetTestFolder");

        public static void DeleteRandomTestFolder(string randomTestPath)
        {
            if (Directory.Exists(randomTestPath))
            {
                AssertNotTempPath(randomTestPath);

                try
                {
                    Directory.Delete(randomTestPath, recursive: true);
                }
                catch
                {
                }
            }
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
            string oldPath = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(path);

            return new ResetDirectory()
            {
                OldPath = oldPath
            };
        }
    }
}
