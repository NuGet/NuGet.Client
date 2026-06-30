// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using Microsoft.Build.Framework;

namespace NuGet.Build.Tasks.Pack
{
    public class GetPackOutputItemsTask : Microsoft.Build.Utilities.Task, IOutputFilePathProvider, IOutputFilePath
    {
        [Required]
        public string PackageId { get; set; }

        [Required]
        public string PackageVersion { get; set; }

        [Required]
        public string PackageOutputPath { get; set; }

        [Required]
        public string NuspecOutputPath { get; set; }

        public string NuspecFile { get; set; }

        public string[] NuspecProperties { get; set; }

        public bool IncludeSymbols { get; set; }

        public bool IncludeSource { get; set; }

        public string SymbolPackageFormat { get; set; }

        public bool OutputFileNamesWithoutVersion { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public ITaskItem[] OutputPackItems { get; set; }

        public string OutputNupkgFilePath { get; set; }
        public string OutputNuspecFilePath { get; set; }
        public string OutputNupkgSymbolsFilePath { get; set; }
        public string OutputNuspecSymbolsFilePath { get; set; }

        public override bool Execute()
        {
            GetPackOutputItemsLogic.GetOutputFilePaths(this, this);
            return true;
        }
    }
}
