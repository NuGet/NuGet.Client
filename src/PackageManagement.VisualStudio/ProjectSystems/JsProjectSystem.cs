// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.Shell;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;
using EnvDTEProjectItems = EnvDTE.ProjectItems;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    public class JsProjectSystem : CpsProjectSystem
    {
        public JsProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        private string _projectName;

        public override string ProjectName
        {
            get
            {
                if (String.IsNullOrEmpty(_projectName))
                {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                            _projectName = EnvDTEProjectUtility.GetName(EnvDTEProject);
                        });
                }
                return _projectName;
            }
        }

        public override void AddFile(string path, Stream stream)
        {
            // ensure the parent folder is created before adding file to the project
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    await EnvDTEProjectUtility.GetProjectItemsAsync(EnvDTEProject, Path.GetDirectoryName(path), createIfNotExists: true);
                    base.AddFile(path, stream);
                });
        }

        public override void AddFile(string path, Action<Stream> writeToStream)
        {
            // ensure the parent folder is created before adding file to the project    
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    await EnvDTEProjectUtility.GetProjectItemsAsync(EnvDTEProject, Path.GetDirectoryName(path), createIfNotExists: true);
                    base.AddFile(path, writeToStream);
                });
        }

        protected override async Task AddFileToProjectAsync(string path)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            if (ExcludeFile(path))
            {
                return;
            }

            string folderPath = Path.GetDirectoryName(path);
            string fullPath = FileSystemUtility.GetFullPath(ProjectFullPath, path);

            // Add the file to project or folder
            EnvDTEProjectItems container = await EnvDTEProjectUtility.GetProjectItemsAsync(EnvDTEProject, folderPath, createIfNotExists: true);
            if (container == null)
            {
                throw new ArgumentException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_FailedToCreateParentFolder,
                        path,
                        ProjectName));
            }
            AddFileToContainer(fullPath, folderPath, container);

            NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_AddedFileToProject, path, ProjectName);
        }
    }
}
