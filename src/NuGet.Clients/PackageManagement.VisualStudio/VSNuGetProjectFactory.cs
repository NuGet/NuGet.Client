﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Common;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace NuGet.PackageManagement.VisualStudio
{
    public class VSNuGetProjectFactory
    {
        private readonly Func<string> _packagesPath;

        private EmptyNuGetProjectContext EmptyNuGetProjectContext { get; }

        // TODO: Add IDeleteOnRestartManager, VsPackageInstallerEvents and IVsFrameworkMultiTargeting to constructor
        public VSNuGetProjectFactory(Func<string> packagesPath)
        {
            if (packagesPath == null)
            {
                throw new ArgumentNullException(nameof(packagesPath));
            }

            _packagesPath = packagesPath;
            EmptyNuGetProjectContext = new EmptyNuGetProjectContext();
        }

        public NuGetProject CreateNuGetProject(EnvDTEProject envDTEProject)
        {
            return CreateNuGetProject(envDTEProject, EmptyNuGetProjectContext);
        }

        public NuGetProject CreateNuGetProject(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
        {
            if (envDTEProject == null)
            {
                throw new ArgumentNullException(nameof(envDTEProject));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            NuGetProject result = null;

            var projectK = GetProjectKProject(envDTEProject);
            if (projectK != null)
            {
                result = new ProjectKNuGetProject(
                    projectK,
                    envDTEProject.Name,
                    EnvDTEProjectUtility.GetCustomUniqueName(envDTEProject));
            }
            else
            {
                var msBuildNuGetProjectSystem = MSBuildNuGetProjectSystemFactory.CreateMSBuildNuGetProjectSystem(
                    envDTEProject,
                    nuGetProjectContext);

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
                            result = new BuildIntegratedProjectSystem(
                                projectJsonPath,
                                msbuildProjectFile.FullName,
                                envDTEProject,
                                msBuildNuGetProjectSystem,
                                EnvDTEProjectUtility.GetCustomUniqueName(envDTEProject));
                        }
                    }
                }

                // Create a normal MSBuild project if no project.json was found
                if (result == null)
                {
                    var folderNuGetProjectFullPath = _packagesPath();

                    // Project folder path is the packages config folder path
                    var packagesConfigFolderPath = EnvDTEProjectUtility.GetFullPath(envDTEProject);

                    result = new MSBuildNuGetProject(
                        msBuildNuGetProjectSystem,
                        folderNuGetProjectFullPath,
                        packagesConfigFolderPath);
                }
            }

            return result;
        }

        public static INuGetPackageManager GetProjectKProject(EnvDTEProject project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var vsProject = VsHierarchyUtility.ToVsHierarchy(project) as IVsProject;
            if (vsProject == null)
            {
                return null;
            }

            IServiceProvider serviceProvider = null;
            vsProject.GetItemContext(
                (uint)VSConstants.VSITEMID.Root,
                out serviceProvider);
            if (serviceProvider == null)
            {
                return null;
            }

            using (var sp = new ServiceProvider(serviceProvider))
            {
                var retValue = sp.GetService(typeof(INuGetPackageManager));
                if (retValue == null)
                {
                    return null;
                }

                if (!(retValue is INuGetPackageManager))
                {
                    // Workaround a bug in Dev14 prereleases where Lazy<INuGetPackageManager> was returned.
                    var properties = retValue.GetType().GetProperties().Where(p => p.Name == "Value");
                    if (properties.Count() == 1)
                    {
                        retValue = properties.First().GetValue(retValue);
                    }
                }

                return retValue as INuGetPackageManager;
            }
        }
    }
}
