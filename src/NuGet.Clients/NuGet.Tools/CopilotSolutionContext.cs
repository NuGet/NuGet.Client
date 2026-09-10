// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;

namespace NuGetVSExtension
{
    internal static class CopilotSolutionContext
    {
        private static readonly IReadOnlyList<string> ConfigurationFileNames =
        [
            "NuGet.Config",
            "Directory.Packages.props",
            "Directory.Build.props",
            "Directory.Build.targets",
        ];

        internal static async Task<string> CreateAsync(
            IVsSolutionManager solutionManager,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string solutionDirectory = solutionManager.SolutionDirectory;
            string solutionFilePath = await solutionManager.GetSolutionFilePathAsync();
            IEnumerable<NuGetProject> projects = await solutionManager.GetNuGetProjectsAsync();

            IReadOnlyList<string> projectPaths = projects
                .Select(project => project.TryGetMetadata(NuGetProjectMetadataKeys.FullPath, out string path) ? path : null)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => Path.GetFullPath(path!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            IReadOnlyList<string> configurationPaths = GetConfigurationPaths(solutionDirectory, projectPaths);

            return JsonConvert.SerializeObject(
                new
                {
                    solutionDirectory,
                    solutionFilePath,
                    projectPaths,
                    otherMsbuildFilePaths = configurationPaths,
                },
                Formatting.Indented);
        }

        internal static IReadOnlyList<string> GetConfigurationPaths(
            string solutionDirectory,
            IReadOnlyList<string> projectPaths)
        {
            if (string.IsNullOrEmpty(solutionDirectory))
            {
                return [];
            }

            string normalizedSolutionDirectory = Path.GetFullPath(solutionDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var configurationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddConfigurationFiles(normalizedSolutionDirectory, configurationPaths);

            foreach (string projectPath in projectPaths)
            {
                DirectoryInfo? directory = new FileInfo(projectPath).Directory;
                while (directory is not null && IsWithinSolution(directory.FullName, normalizedSolutionDirectory))
                {
                    AddConfigurationFiles(directory.FullName, configurationPaths);

                    if (string.Equals(directory.FullName, normalizedSolutionDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    directory = directory.Parent;
                }
            }

            return configurationPaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddConfigurationFiles(string directory, HashSet<string> configurationPaths)
        {
            foreach (string fileName in ConfigurationFileNames)
            {
                string path = Path.Combine(directory, fileName);
                if (File.Exists(path))
                {
                    configurationPaths.Add(path);
                }
            }
        }

        private static bool IsWithinSolution(string directory, string solutionDirectory)
        {
            if (string.Equals(directory, solutionDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string solutionDirectoryPrefix = solutionDirectory + Path.DirectorySeparatorChar;
            return directory.StartsWith(solutionDirectoryPrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
