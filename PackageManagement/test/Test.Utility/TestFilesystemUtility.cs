// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Test.Utility
{
    public static class TestFilesystemUtility
    {
        private static readonly string NuGetTestFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NuGetTestFolder");

        public static string CreateRandomTestFolder()
        {
            var randomFolderName = Guid.NewGuid().ToString();
            var path = Path.Combine(NuGetTestFolder, randomFolderName);
            Directory.CreateDirectory(path);
            return path;
        }

        public static void DeleteRandomTestFolders(params string[] randomTestPaths)
        {
            foreach (var randomTestPath in randomTestPaths)
            {
                DeleteRandomTestFolder(randomTestPath);
            }
        }

        private static void DeleteRandomTestFolder(string randomTestPath)
        {
            try
            {
                if (Directory.Exists(randomTestPath))
                {
                    Directory.Delete(randomTestPath, recursive: true);
                }
            }
            catch (Exception)
            {
                // Ignore exception while deleting directories
            }
        }
    }
}
