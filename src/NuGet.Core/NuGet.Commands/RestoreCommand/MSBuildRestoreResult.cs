// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        public IReadOnlyList<string> Props { get; }

        /// <summary>
        /// Gets a list of MSBuild targets files provided by packages during this restore
        /// </summary>
        public IReadOnlyList<string> Targets { get; }

        public MSBuildRestoreResult(string targetsPath, string propsPath, bool success)
            : this(targetsPath,
                  propsPath, 
                  repositoryRoot: string.Empty, 
                  targets: new List<string>(),
                  props: new List<string>(),
                  success: success)
        {
        }

        public MSBuildRestoreResult(
            string targetsPath,
            string propsPath,
            string repositoryRoot,
            IReadOnlyList<string> props,
            IReadOnlyList<string> targets)
            : this(targetsPath, propsPath, repositoryRoot, props, targets, success: true)
        {
        }

        private MSBuildRestoreResult(
            string targetsPath,
            string propsPath,
            string repositoryRoot,
            IReadOnlyList<string> props,
            IReadOnlyList<string> targets,
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
                if (Targets.Count > 0)
                {
                    log.LogMinimal(string.Format(CultureInfo.CurrentCulture, Strings.Log_GeneratingMsBuildFile, TargetsPath));

                    GenerateImportsFile(TargetsPath, Targets);
                }
                else if (File.Exists(TargetsPath))
                {
                    File.Delete(TargetsPath);
                }

                if (Props.Count > 0)
                {
                    log.LogMinimal(string.Format(CultureInfo.CurrentCulture, Strings.Log_GeneratingMsBuildFile, PropsPath));

                    GenerateImportsFile(PropsPath, Props);
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

        private void GenerateImportsFile(string path, IEnumerable<string> imports)
        {
            var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "no"),

                new XElement(ns + "Project",
                    new XAttribute("ToolsVersion", "14.0"),

                    new XElement(ns + "PropertyGroup",
                        new XAttribute("Condition", "'$(NuGetPackageRoot)' == ''"),

                        new XElement(ns + "NuGetPackageRoot", ReplacePathsWithMacros(RepositoryRoot))),
                    new XElement(ns + "ImportGroup", imports.Select(i =>
                        new XElement(ns + "Import",
                            new XAttribute("Project", GetImportPath(i)),
                            new XAttribute("Condition", $"Exists('{GetImportPath(i)}')"))))));

            using (var output = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                doc.Save(output);
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
    }
}
