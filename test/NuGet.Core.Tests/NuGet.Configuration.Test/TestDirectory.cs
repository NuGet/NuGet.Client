// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace NuGet.Configuration.Test
{
    public class TestDirectory : IDisposable
    {
        public TestDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
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
