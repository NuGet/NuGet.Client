// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Versioning;

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

        public override bool Execute()
        {
            (string packageId, NuGetVersion version) = GetPackageIdAndVersion();

            var symbolPackageFormat = PackArgs.GetSymbolPackageFormat(MSBuildStringUtility.TrimAndGetNullForEmpty(SymbolPackageFormat));
            var nupkgFileName = PackCommandRunner.GetOutputFileName(packageId, version, isNupkg: true, symbols: false, symbolPackageFormat: symbolPackageFormat, excludeVersion: OutputFileNamesWithoutVersion);
            var nuspecFileName = PackCommandRunner.GetOutputFileName(packageId, version, isNupkg: false, symbols: false, symbolPackageFormat: symbolPackageFormat, excludeVersion: OutputFileNamesWithoutVersion);

            var outputs = new List<ITaskItem>();
            outputs.Add(new TaskItem(Path.Combine(PackageOutputPath, nupkgFileName)));
            outputs.Add(new TaskItem(Path.Combine(NuspecOutputPath, nuspecFileName)));

            if (IncludeSource || IncludeSymbols)
            {
                var nupkgSymbolsFileName = PackCommandRunner.GetOutputFileName(packageId, version, isNupkg: true, symbols: true, symbolPackageFormat: symbolPackageFormat, excludeVersion: OutputFileNamesWithoutVersion);
                var nuspecSymbolsFileName = PackCommandRunner.GetOutputFileName(packageId, version, isNupkg: false, symbols: true, symbolPackageFormat: symbolPackageFormat, excludeVersion: OutputFileNamesWithoutVersion);
                outputs.Add(new TaskItem(Path.Combine(PackageOutputPath, nupkgSymbolsFileName)));
                outputs.Add(new TaskItem(Path.Combine(NuspecOutputPath, nuspecSymbolsFileName)));
            }

            OutputPackItems = outputs.ToArray();
            return true;
        }

        private (string packageId, NuGetVersion version) GetPackageIdAndVersion()
        {
            string packageId = PackageId;
            var packageVersion = PackageVersion;
            NuGetVersion version = null;

            // Extract the version from the nuspec file if it exists and is valid, otherwise use the version from the project.
            if (!string.IsNullOrWhiteSpace(NuspecFile) && File.Exists(NuspecFile))
            {
                // Parse NuspecProperties into a dictionary used for $token$ substitution.
                Dictionary<string, string> tokenProperties;
                bool hasVersionInNuspecProperties = false;

                if (NuspecProperties != null && NuspecProperties.Length > 0)
                {
                    PackArgs packArgs = new PackArgs() { Version = packageVersion };
                    PackTaskLogic.SetPackArgsPropertiesFromNuspecProperties(packArgs, MSBuildStringUtility.TrimAndExcludeNullOrEmpty(NuspecProperties));
                    tokenProperties = packArgs.Properties;
                    // If the logic depends only on checking for a non-null value, it may incorrectly detect cases where the parsing logic changes the version based on a key other than the "version" key.
                    // Currently, supported only version property in NuspecProperties.
                    if (packArgs.Properties.ContainsKey("version"))
                    {
                        packageVersion = packArgs.Version;
                        hasVersionInNuspecProperties = true;
                    }
                }
                else
                {
                    tokenProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                var nuspecReader = new NuspecReader(NuspecFile);

                // Read the id from the nuspec and apply token substitution.
                // A literal <id> element is used as-is; NuspecProperties only replace $token$ placeholders.
                string rawId = nuspecReader.GetId();
                if (rawId != null)
                {
                    packageId = SubstituteNuspecTokens(rawId, tokenProperties, fallbackTokenValue: PackageId);
                }

                if (!hasVersionInNuspecProperties)
                {
                    // Read the raw version string to detect $token$ placeholders before attempting to parse.
                    string rawVersion = nuspecReader.GetMetadataValue("version");
                    if (!string.IsNullOrEmpty(rawVersion))
                    {
                        string resolvedVersion = SubstituteNuspecTokens(rawVersion, tokenProperties, fallbackTokenValue: packageVersion);
                        if (NuGetVersion.TryParse(resolvedVersion, out var parsedVersion))
                        {
                            version = parsedVersion;
                        }
                        // If resolvedVersion is still unparseable, leave version as null and fall back to packageVersion below.
                    }
                }
            }

            if (version == null && !NuGetVersion.TryParse(packageVersion, out version))
            {
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.InvalidPackageVersion,
                    packageVersion));
            }

            return (packageId, version);
        }

        /// <summary>
        /// Performs $token$ substitution on a nuspec metadata value using the supplied property dictionary.
        /// For any token that is not present in <paramref name="properties"/>, <paramref name="fallbackTokenValue"/> is used.
        /// Literal text (non-token) portions are preserved unchanged.
        /// </summary>
        private static string SubstituteNuspecTokens(string value, Dictionary<string, string> properties, string fallbackTokenValue)
        {
            var tokenizer = new Tokenizer(value);
            var result = new StringBuilder();
            for (; ; )
            {
                Token token = tokenizer.Read();
                if (token == null)
                {
                    break;
                }

                if (token.Category == TokenCategory.Variable)
                {
                    result.Append(properties.TryGetValue(token.Value, out string replacement) ? replacement : fallbackTokenValue);
                }
                else
                {
                    result.Append(token.Value);
                }
            }
            return result.ToString();
        }
    }
}
