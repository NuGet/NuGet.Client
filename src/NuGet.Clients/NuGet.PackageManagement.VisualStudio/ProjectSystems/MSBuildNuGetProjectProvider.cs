// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using NuGet.Common;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IProjectSystemProvider))]
    [Name(nameof(MSBuildNuGetProjectProvider))]
#if VS14
    [Order(After = nameof(ProjectKNuGetProjectProvider))]
#else
    [Order(After = nameof(LegacyCSProjPackageReferenceProjectProvider))]
#endif
    public class MSBuildNuGetProjectProvider : IProjectSystemProvider
    {
        public bool TryCreateNuGetProject(EnvDTE.Project envDTEProject, ProjectSystemProviderContext context, out NuGetProject result)
        {
            if (envDTEProject == null)
            {
                throw new ArgumentNullException(nameof(envDTEProject));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            result = null;

            var msBuildNuGetProjectSystem = MSBuildNuGetProjectSystemFactory.CreateMSBuildNuGetProjectSystem(
                                envDTEProject,
                                context.ProjectContext);

            var isWebSite = msBuildNuGetProjectSystem is WebSiteProjectSystem;

            // Web sites cannot have project.json
            if (!isWebSite)
            {
                // Find the project file path
                var projectFilePath = EnvDTEProjectUtility.GetFullProjectPath(envDTEProject);

                if (!string.IsNullOrEmpty(projectFilePath))
                {
                    var msbuildProjectFile = new FileInfo(projectFilePath);
                    var projectNameFromMSBuildPath = Path.GetFileNameWithoutExtension(msbuildProjectFile.Name);

                    // Treat projects with project.json as build integrated projects
                    // Search for projectName.project.json first, then project.json
                    // If the name cannot be determined, search only for project.json
                    string projectJsonPath = null;
                    if (string.IsNullOrEmpty(projectNameFromMSBuildPath))
                    {
                        projectJsonPath = Path.Combine(msbuildProjectFile.DirectoryName,
                            ProjectJsonPathUtilities.ProjectConfigFileName);
                    }
                    else
                    {
                        projectJsonPath = ProjectJsonPathUtilities.GetProjectConfigPath(msbuildProjectFile.DirectoryName,
                        projectNameFromMSBuildPath);
                    }

                    if (File.Exists(projectJsonPath))
                    {
                        result = new ProjectJsonBuildIntegratedProjectSystem(
                            projectJsonPath,
                            msbuildProjectFile.FullName,
                            envDTEProject,
                            EnvDTEProjectUtility.GetCustomUniqueName(envDTEProject));
                    }
                }
            }

            // Create a normal MSBuild project if no project.json was found
            if (result == null)
            {
                var folderNuGetProjectFullPath = context.PackagesPathFactory();

                // Project folder path is the packages config folder path
                var packagesConfigFolderPath = EnvDTEProjectUtility.GetFullPath(envDTEProject);

                result = new VSMSBuildNuGetProject(
                    envDTEProject,
                    msBuildNuGetProjectSystem,
                    folderNuGetProjectFullPath,
                    packagesConfigFolderPath);
            }

            return result != null;
        }
    }
}
