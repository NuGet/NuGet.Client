// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace NuGet.Build.Tasks.Pack
{
    public class GetPackOutputItemsTask : Task
    {
        [Required]
        public string PackageId { get; set; }

        [Required]
        public string PackageVersion { get; set; }

        [Required]
        public string PackageOutputPath { get; set; }

        [Required]
        public string NuspecOutputPath { get; set; }

        public bool IncludeSymbols { get; set; }

        public bool IncludeSource { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public ITaskItem[] OutputPackItems { get; set; }

        public override bool Execute()
        {
            NuGetVersion version;
            if (!NuGetVersion.TryParse(PackageVersion, out version))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.InvalidPackageVersion,
                    PackageVersion));
            }

            var nupkgFileName = PackCommandRunner.GetOutputFileName(PackageId, version, isNupkg: true, symbols: false);
            var nuspecFileName = PackCommandRunner.GetOutputFileName(PackageId, version, isNupkg: false, symbols: false);
            
            var outputs = new List<ITaskItem>();
            outputs.Add(new TaskItem(Path.Combine(PackageOutputPath, nupkgFileName)));
            outputs.Add(new TaskItem(Path.Combine(NuspecOutputPath, nuspecFileName)));

            if(IncludeSource || IncludeSymbols)
            {
                var nupkgSymbolsFileName = PackCommandRunner.GetOutputFileName(PackageId, version, isNupkg: true, symbols: true);
                var nuspecSymbolsFileName = PackCommandRunner.GetOutputFileName(PackageId, version, isNupkg: false, symbols: true);

                outputs.Add(new TaskItem(Path.Combine(PackageOutputPath, nupkgSymbolsFileName)));
                outputs.Add(new TaskItem(Path.Combine(NuspecOutputPath, nuspecSymbolsFileName)));
            }           

            OutputPackItems = outputs.ToArray();
            return true;
        }
    }
}
