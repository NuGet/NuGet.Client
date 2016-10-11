// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using NuGet.Common;

namespace NuGet.Commands
{
    public class MSBuildRestoreResult
    {
        /// <summary>
        /// The macros that we may use in MSBuild to replace path roots.
        /// </summary>
        private static readonly string[] MacroCandidates = new[]
        {
            "UserProfile", // e.g. C:\users\myusername
        };

        /// <summary>
        /// Gets a boolean indicating if the necessary MSBuild file could be generated
        /// </summary>
        public bool Success { get; }

        public string TargetsPath { get; }

        public string PropsPath { get; }

        /// <summary>
        /// Gets the root of the repository containing packages with MSBuild files
        /// </summary>
        public string RepositoryRoot { get; }

        /// <summary>
        /// Gets a list of MSBuild props files provided by packages during this restore
        /// </summary>
        public IList<MSBuildRestoreImportGroup> Props { get; }

        /// <summary>
        /// Gets a list of MSBuild targets files provided by packages during this restore
        /// </summary>
        public IList<MSBuildRestoreImportGroup> Targets { get; }

        public MSBuildRestoreResult(string targetsPath, string propsPath, bool success)
            : this(targetsPath,
                  propsPath,
                  repositoryRoot: string.Empty,
                  targets: new List<MSBuildRestoreImportGroup>(),
                  props: new List<MSBuildRestoreImportGroup>(),
                  success: success)
        {
        }

        public MSBuildRestoreResult(
            string targetsPath,
            string propsPath,
            string repositoryRoot,
            IList<MSBuildRestoreImportGroup> props,
            IList<MSBuildRestoreImportGroup> targets)
            : this(targetsPath, propsPath, repositoryRoot, props, targets, success: true)
        {
        }

        private MSBuildRestoreResult(
            string targetsPath,
            string propsPath,
            string repositoryRoot,
            IList<MSBuildRestoreImportGroup> props,
            IList<MSBuildRestoreImportGroup> targets,
            bool success)
        {
            Success = success;
            TargetsPath = targetsPath;
            PropsPath = propsPath;
            RepositoryRoot = repositoryRoot;
            Props = props;
            Targets = targets;
        }

        public void Commit(ILogger log)
        {
            Commit(log, forceWrite: false, token: CancellationToken.None);
        }

        public void Commit(ILogger log, bool forceWrite, CancellationToken token)
        {
            // Ensure the directories exists
            if (!Success || Targets.Count > 0 || Props.Count > 0)
            {
                var outputDirs = new HashSet<string>() { Path.GetDirectoryName(TargetsPath), Path.GetDirectoryName(PropsPath) };

                foreach (var dir in outputDirs)
                {
                    Directory.CreateDirectory(dir);
                }
            }

            if (!Success)
            {
                // Write a target containing an error
                log.LogMinimal(string.Format(CultureInfo.CurrentCulture, Strings.Log_GeneratingMsBuildFile, TargetsPath));
                GenerateMSBuildErrorFile(TargetsPath);

                // Clean up any props that exist
                if (File.Exists(PropsPath))
                {
                    File.Delete(PropsPath);
                }
            }
            else
            {
                // Generate the files as needed
                if (Targets.Any(group => group.Imports.Count > 0))
                {
                    log.LogMinimal(string.Format(CultureInfo.CurrentCulture, Strings.Log_GeneratingMsBuildFile, TargetsPath));

                    GenerateImportsFile(TargetsPath, Targets, forceWrite, log);
                }
                else if (File.Exists(TargetsPath))
                {
                    File.Delete(TargetsPath);
                }

                if (Props.Any(group => group.Imports.Count > 0))
                {
                    log.LogMinimal(string.Format(CultureInfo.CurrentCulture, Strings.Log_GeneratingMsBuildFile, PropsPath));

                    GenerateImportsFile(PropsPath, Props, forceWrite, log);
                }
                else if (File.Exists(PropsPath))
                {
                    File.Delete(PropsPath);
                }
            }
        }

        private static string ReplacePathsWithMacros(string path)
        {
            foreach (var macroName in MacroCandidates)
            {
                string macroValue = Environment.GetEnvironmentVariable(macroName);
                if (!string.IsNullOrEmpty(macroValue)
                    && path.StartsWith(macroValue, StringComparison.OrdinalIgnoreCase))
                {
                    path = $"$({macroName})" + $"{path.Substring(macroValue.Length)}";
                }

                break;
            }

            return path;
        }

        private void GenerateMSBuildErrorFile(string path)
        {
            var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "no"),

                new XElement(ns + "Project",
                    new XAttribute("ToolsVersion", "14.0"),

                    new XElement(ns + "Target",
                        new XAttribute("Name", "EmitMSBuildWarning"),
                        new XAttribute("BeforeTargets", "Build"),

                        new XElement(ns + "Warning",
                            new XAttribute("Text", Strings.MSBuildWarning_MultiTarget)))));

            using (var output = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                doc.Save(output);
            }
        }

        private void GenerateImportsFile(string path, IList<MSBuildRestoreImportGroup> groups, bool forceWrite, ILogger log)
        {
            var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "no"),

                new XElement(ns + "Project",
                    new XAttribute("ToolsVersion", "14.0"),

                    new XElement(ns + "PropertyGroup",
                        new XAttribute("Condition", "'$(NuGetPackageRoot)' == ''"),
                        new XElement(ns + "NuGetPackageRoot", ReplacePathsWithMacros(RepositoryRoot)))));

            // Add import groups, order by position, then by the conditions to keep the results deterministic
            // Skip empty groups
            foreach (var group in groups
                .Where(e => e.Imports.Count > 0)
                .OrderBy(e => e.Position)
                .ThenBy(e => e.Condition, StringComparer.OrdinalIgnoreCase))
            {
                var itemGroup = new XElement(ns + "ImportGroup", group.Imports.Select(i =>
                            new XElement(ns + "Import",
                                new XAttribute("Project", GetImportPath(i)),
                                new XAttribute("Condition", $"Exists('{GetImportPath(i)}')"))));

                // Add a conditional statement if multiple TFMs exist or cross targeting is present
                var conditionValue = group.Condition;
                if (!string.IsNullOrEmpty(conditionValue))
                {
                    itemGroup.Add(new XAttribute("Condition", conditionValue));
                }

                // Add itemgroup to file
                doc.Root.Add(itemGroup);
            }

            // Check if the file has changes
            if (forceWrite || HasChanges(doc, path, log))
            {
                log.LogDebug($"Writing imports file to disk: {path}");

                using (var output = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    doc.Save(output);
                }
            }
            else
            {
                log.LogDebug($"No changes found. Skipping write of imports file to disk: {path}");
            }
        }

        private string GetImportPath(string importPath)
        {
            var path = importPath;

            if (importPath.StartsWith(RepositoryRoot, StringComparison.Ordinal))
            {
                path = $"$(NuGetPackageRoot){importPath.Substring(RepositoryRoot.Length)}";
            }
            else
            {
                path = ReplacePathsWithMacros(importPath);
            }

            return path;
        }

        /// <summary>
        /// Check if the file has changes compared to the original on disk.
        /// </summary>
        private bool HasChanges(XDocument newFile, string path, ILogger log)
        {
            XDocument existing = ReadExisting(path, log);

            if (existing != null)
            {
                // Use a simple string compare to check if the files match
                // This can be optimized in the future, but generally these are very small files.
                return !newFile.ToString().Equals(existing.ToString(), StringComparison.Ordinal);
            }

            return true;
        }

        private XDocument ReadExisting(string path, ILogger log)
        {
            XDocument result = null;

            if (File.Exists(path))
            {
                try
                {
                    using (var output = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        result = XDocument.Load(output);
                    }
                }
                catch (Exception ex)
                {
                    // Log a debug message and ignore, this will force an overwrite
                    log.LogDebug($"Failed to open imports file: {path} Error: {ex.Message}");
                }
            }

            return result;
        }
    }
}