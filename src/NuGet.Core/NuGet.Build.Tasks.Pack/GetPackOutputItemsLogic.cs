// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Versioning;

namespace NuGet.Build.Tasks.Pack
{
    public static class GetPackOutputItemsLogic
    {
        public static void GetOutputFilePaths(IOutputFilePathProvider source, IOutputFilePath output)
        {
            var packageId = source.PackageId;
            var packageVersion = source.PackageVersion;

            if (!string.IsNullOrWhiteSpace(source.NuspecFile))
            {
                bool hasVersionInNuspecProperties = false;
                bool hasIdInNuspecProperties = false;
                if (source.NuspecProperties != null && source.NuspecProperties.Length > 0)
                {
                    PackArgs packArgs = new PackArgs() { Version = packageVersion };
                    PackTaskLogic.SetPackArgsPropertiesFromNuspecProperties(packArgs, MSBuildStringUtility.TrimAndExcludeNullOrEmpty(source.NuspecProperties));
                    // If the logic depends only on checking for a non-null value, it may incorrectly  detect cases where the parsing logic changes the version based on a key other than the "version" key.
                    if (packArgs.Properties.ContainsKey("version"))
                    {
                        packageVersion = packArgs.Version;
                        hasVersionInNuspecProperties = true;
                    }
                    if (packArgs.Properties.TryGetValue("id", out var idTemp))
                    {
                        packageId = idTemp;
                        hasIdInNuspecProperties = true;
                    }
                }

                var nuspecReader = new NuGet.Packaging.NuspecReader(source.NuspecFile);
                if (!hasIdInNuspecProperties)
                {
                    packageId = nuspecReader.GetId();
                }
                if (!hasVersionInNuspecProperties)
                {
                    packageVersion = nuspecReader.GetVersion().ToNormalizedString();
                }
            }

            if (!NuGetVersion.TryParse(packageVersion, out var versionTemp))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.InvalidPackageVersion,
                    packageVersion));
            }
            NuGetVersion version = versionTemp!;

            var symbolPackageFormat = PackArgs.GetSymbolPackageFormat(MSBuildStringUtility.TrimAndGetNullForEmpty(source.SymbolPackageFormat));
            var nupkgFileName = PackCommandRunner.GetOutputFileName(packageId, version!, isNupkg: true, symbols: false, symbolPackageFormat: symbolPackageFormat, excludeVersion: source.OutputFileNamesWithoutVersion);
            var nuspecFileName = PackCommandRunner.GetOutputFileName(packageId, version!, isNupkg: false, symbols: false, symbolPackageFormat: symbolPackageFormat, excludeVersion: source.OutputFileNamesWithoutVersion);

            var outputs = new List<ITaskItem>();

            output.OutputNupkgFilePath = Path.Combine(source.PackageOutputPath, nupkgFileName);
            output.OutputNuspecFilePath = Path.Combine(source.NuspecOutputPath, nuspecFileName);

            outputs.Add(new TaskItem(output.OutputNupkgFilePath));
            outputs.Add(new TaskItem(output.OutputNuspecFilePath));

            if (source.IncludeSource || source.IncludeSymbols)
            {
                var nupkgSymbolsFileName = PackCommandRunner.GetOutputFileName(packageId, version, isNupkg: true, symbols: true, symbolPackageFormat: symbolPackageFormat, excludeVersion: source.OutputFileNamesWithoutVersion);
                var nuspecSymbolsFileName = PackCommandRunner.GetOutputFileName(packageId, version, isNupkg: false, symbols: true, symbolPackageFormat: symbolPackageFormat, excludeVersion: source.OutputFileNamesWithoutVersion);

                output.OutputNupkgSymbolsFilePath = Path.Combine(source.PackageOutputPath, nupkgSymbolsFileName);
                output.OutputNuspecSymbolsFilePath = Path.Combine(source.NuspecOutputPath, nuspecSymbolsFileName);

                outputs.Add(new TaskItem(output.OutputNupkgSymbolsFilePath));
                outputs.Add(new TaskItem(output.OutputNuspecSymbolsFilePath));
            }

            output.OutputPackItems = outputs.ToArray();
        }

        public static IOutputFilePath GetOutputFilePaths(IOutputFilePathProvider source)
        {
            var output = new GetPackOutputItemsTask();
            Copy(source, output);
            GetOutputFilePaths(source, output);
            return output;
        }

        public static void Copy(IOutputFilePathProvider source, IOutputFilePathProvider destination)
        {
            destination.PackageId = source.PackageId;
            destination.PackageVersion = source.PackageVersion;
            destination.PackageOutputPath = source.PackageOutputPath;
            destination.NuspecOutputPath = source.NuspecOutputPath;
            destination.NuspecFile = source.NuspecFile;
            destination.NuspecProperties = source.NuspecProperties;
            destination.IncludeSource = source.IncludeSource;
            destination.IncludeSymbols = source.IncludeSymbols;
            destination.SymbolPackageFormat = source.SymbolPackageFormat;
            destination.OutputFileNamesWithoutVersion = source.OutputFileNamesWithoutVersion;
        }
    }
}
