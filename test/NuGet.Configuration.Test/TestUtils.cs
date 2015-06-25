// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

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
                    return FormatXmlString(streamReader.ReadToEnd());
                }
            }
        }

        // this method is for formating xml string
        public static string FormatXmlString(string result)
        {
            return XDocument.Parse(result).ToString();
        }
    }
}
