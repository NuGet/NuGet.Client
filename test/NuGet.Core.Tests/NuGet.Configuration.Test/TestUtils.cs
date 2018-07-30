// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NuGet.Configuration.Test
{
    public static class ConfigurationFileTestUtility
    {
        public static void CreateConfigurationFile(string configurationPath, string mockBaseDirectory, string configurationContent)
        {
            Directory.CreateDirectory(mockBaseDirectory);
            using (var file = File.Create(Path.Combine(mockBaseDirectory, configurationPath)))
            {
                var info = new UTF8Encoding(true).GetBytes(configurationContent);
                file.Write(info, 0, info.Count());
            }
        }

        public static byte[] GetFileHash(string fileName)
        {
            var hashAlgorithm = SHA512.Create();
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                return hashAlgorithm.ComputeHash(stream);
            }
        }

        public static string RemoveWhitespace(string s)
        {
            return Regex.Replace(s, @"\s+", string.Empty);
        }
    }
}
