// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace NuGetVSExtension
{
    internal static class CopilotSolutionContext
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
        };

        internal static async Task<string> CreateAsync(
            IVsSolutionManager solutionManager,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string solutionDirectory = solutionManager.SolutionDirectory;
            IEnumerable<IVsProjectAdapter> projects = await solutionManager.GetAllVsProjectAdaptersAsync();

            var projectPaths = new List<string>();

            foreach (IVsProjectAdapter project in projects)
            {
                string path = project.FullProjectPath;
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string fullPath = Path.GetFullPath(path);
                int index = projectPaths.BinarySearch(fullPath, StringComparer.OrdinalIgnoreCase);
                if (index < 0)
                {
                    projectPaths.Insert(~index, fullPath);
                }
            }

            return JsonSerializer.Serialize(
                new
                {
                    solutionDirectory,
                    projectPaths,
                },
                SerializerOptions);
        }
    }
}
