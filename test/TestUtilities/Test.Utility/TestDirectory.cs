// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Test.Utility
{
    public class TestDirectory : IDisposable
    {
        private TestDirectory(string path)
        {
            Path = path;
            Info = new DirectoryInfo(path);
        }

        public string Path { get; }
        public DirectoryInfo Info { get; }

        public void Dispose()
        {
            TestFileSystemUtility.DeleteRandomTestFolder(Path);
        }

        public static TestDirectory Create(string path = null)
        {
            var randomFolderName = Guid.NewGuid().ToString();

            path = string.IsNullOrWhiteSpace(path) ? System.IO.Path.Combine(TestFileSystemUtility.NuGetTestFolder, randomFolderName) : path;

            if (Directory.Exists(path))
            {
                throw new InvalidOperationException("Guid collision");
            }

            Directory.CreateDirectory(path);

            return new TestDirectory(path);
        }

        public static implicit operator string(TestDirectory directory)
        {
            return directory.Path;
        }

        public override string ToString()
        {
            return Path;
        }
    }
}