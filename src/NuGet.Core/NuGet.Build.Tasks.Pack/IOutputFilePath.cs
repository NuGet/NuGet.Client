// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using Microsoft.Build.Framework;

namespace NuGet.Build.Tasks.Pack
{
    public interface IOutputFilePath
    {
        [Output]
        public ITaskItem[] OutputPackItems { get; set; }

        public string OutputNupkgFilePath { get; set; }
        public string OutputNuspecFilePath { get; set; }
        public string OutputNupkgSymbolsFilePath { get; set; }
        public string OutputNuspecSymbolsFilePath { get; set; }
    }
}
