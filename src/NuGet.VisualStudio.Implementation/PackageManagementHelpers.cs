// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace NuGet.VisualStudio
{
    internal static class PackageManagementHelpers
    {
        /// <summary>
        /// Finds the NuGetProject from a DTE project
        /// </summary>
        public static async Task<NuGetProject> GetProjectAsync(ISolutionManager solutionManager, Project project, VSAPIProjectContext projectContext = null)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (solutionManager == null)
            {
                throw new ArgumentNullException("solution");
            }

            var projectSafeName = await EnvDTEProjectUtility.GetCustomUniqueNameAsync(project);
            NuGetProject nuGetProject = solutionManager.GetNuGetProject(projectSafeName);

            var settings = ServiceLocator.GetInstance<Configuration.ISettings>();

            // if the project does not exist in the solution (this is true for new templates) create it manually
            if (nuGetProject == null)
            {
                VSNuGetProjectFactory factory =
                    new VSNuGetProjectFactory(() => PackagesFolderPathUtility.GetPackagesFolderPath(solutionManager, settings));
                nuGetProject = factory.CreateNuGetProject(project, projectContext);
            }

            return nuGetProject;
        }

        public static IVsPackageMetadata CreateMetadata(string nupkgPath, PackageIdentity package)
        {
            IEnumerable<string> authors = Enumerable.Empty<string>();
            string description = string.Empty;
            string title = package.Id;
            string installPath = string.Empty;

            try
            {
                // installPath is the nupkg path
                FileInfo file = new FileInfo(nupkgPath);
                installPath = file.Directory.FullName;
                PackageReader reader = new PackageReader(file.OpenRead());

                using (var nuspecStream = reader.GetNuspec())
                {
                    NuspecReader nuspec = new NuspecReader(nuspecStream);

                    var metadata = nuspec.GetMetadata();

                    authors = GetNuspecValue(metadata, "authors").Split(',').ToArray();
                    title = GetNuspecValue(metadata, "title");
                    description = GetNuspecValue(metadata, "description");
                }
            }
            catch (Exception ex)
            {
                // ignore errors from reading the extra fields
                Debug.Fail(ex.ToString());
            }

            if (String.IsNullOrEmpty(title))
            {
                title = package.Id;
            }

            return new VsPackageMetadata(package, title, authors, description, installPath);
        }

        private static string GetNuspecValue(IEnumerable<KeyValuePair<string, string>> metadata, string field)
        {
            var node = metadata.Where(e => StringComparer.Ordinal.Equals(field, e.Key)).FirstOrDefault();

            return node.Value ?? string.Empty;
        }
    }
}
