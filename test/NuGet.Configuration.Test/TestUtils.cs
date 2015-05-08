// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGet.Configuration.Test
{
    public static class TestFilesystemUtility
    {
        private static readonly string NuGetTestFolder = Path.Combine(Path.GetTempPath(), "NuGetTestFolder");

        public static TestDirectory CreateRandomTestFolder()
        {
            var randomFolderName = Guid.NewGuid().ToString();
            var path = Path.Combine(NuGetTestFolder, randomFolderName);
            Directory.CreateDirectory(path);
            return new TestDirectory(path);
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
            if (Directory.Exists(randomTestPath))
            {
                Directory.Delete(randomTestPath, recursive: true);
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

        public static string ReadConfigurationFile(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                using (var streamReader = new StreamReader(fs))
                {
                    return RemovedLineEndings(streamReader.ReadToEnd());
                }
            }
        }

        // this method is for removing LineEndings for CI build
        public static string RemovedLineEndings(string result)
        {
            return result.Replace("\n", "").Replace("\r", "");
        }
    }
}
