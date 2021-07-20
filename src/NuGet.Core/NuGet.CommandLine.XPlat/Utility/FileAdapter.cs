// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.CommandLine.XPlat.Utility
{
    internal class FileAdapter : IFileAdapter
    {
        public bool Exists(string path) => File.Exists(path);
    }
}
