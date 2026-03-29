// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using Microsoft.Build.Framework;

namespace NuGet.Build.Tasks.Pack
{
    public interface IOutputFilePathProvider
    {
        [Required]
        string PackageId { get; set; }

        [Required]
        string PackageVersion { get; set; }

        [Required]
        string PackageOutputPath { get; set; }

        [Required]
        string NuspecOutputPath { get; set; }

        string NuspecFile { get; set; }

        string[] NuspecProperties { get; set; }

        bool IncludeSource { get; set; }

        bool IncludeSymbols { get; set; }

        string SymbolPackageFormat { get; set; }

        bool OutputFileNamesWithoutVersion { get; set; }
    }
}
