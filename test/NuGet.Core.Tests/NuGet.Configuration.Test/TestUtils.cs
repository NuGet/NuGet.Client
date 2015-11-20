// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace NuGet.Configuration.Test
{
    public static class TestFilesystemUtility
    {
        private static readonly string _NuGetTestFolder = Path.Combine(Path.GetTempPath(), "NuGetTestFolder");

        public static string NuGetTestFolder
        {
            get
            {
                return _NuGetTestFolder;
            }
        }

        public static TestDirectory CreateRandomTestFolder()
        {
            var randomFolderName = Guid.NewGuid().ToString();
            var path = Path.Combine(NuGetTestFolder, randomFolderName);
            Directory.CreateDirectory(path);
            return new TestDirectory(path);
        }

        public static void DeleteRandomTestFolders(params string[] testPaths)
        {
            foreach (var testPath in testPaths)
            {
                DeleteRandomTestFolder(testPath);
            }
        }

        private static void DeleteRandomTestFolder(string testPath)
        {
            try
            {
                try
                {
                    if (Directory.Exists(testPath))
                    {
                        Directory.Delete(testPath, recursive: true);
                    }
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                    Directory.Delete(testPath, recursive: true);
                }
            }
            catch (Exception ex)
            {
                // Ignore failed deletes, and don't fail the tests.
                Console.WriteLine($"Failed to delete: {testPath} because {ex.ToString()}");
            }
        }

        public static void CreateConfigurationFile(string configurationPath, string mockBaseDirectory, string configurationContent)
        {
            Directory.CreateDirectory(mockBaseDirectory);
            using (var file = File.Create(Path.Combine(mockBaseDirectory, configurationPath)))
            {
                var info = new UTF8Encoding(true).GetBytes(configurationContent);
                file.Write(info, 0, info.Count());
            }
        }
    }
}
