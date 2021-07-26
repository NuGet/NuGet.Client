// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class NativeProjectSystem : CpsProjectSystem
    {
        public NativeProjectSystem(IVsProjectAdapter vsProjectAdapter, INuGetProjectContext nuGetProjectContext)
            : base(vsProjectAdapter, nuGetProjectContext)
        {
        }

        public override Task AddReferenceAsync(string referencePath)
        {
            // We disable assembly reference for native projects
            return Task.CompletedTask;
        }

        protected override async Task AddFileToProjectAsync(string path)
        {
            if (ExcludeFile(path))
            {
                return;
            }

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            // Get the project items for the folder path
            var folderPath = Path.GetDirectoryName(path);
            var fullPath = FileSystemUtility.GetFullPath(ProjectFullPath, path);

            VCProjectHelper.AddFileToProject(VsProjectAdapter.Project.Object, fullPath, folderPath);

            NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_AddedFileToProject, path, ProjectName);
        }

        public override Task<bool> ReferenceExistsAsync(string name)
        {
            // We disable assembly reference for native projects
            return Task.FromResult(true);
        }

        public override Task RemoveReferenceAsync(string name)
        {
            // We disable assembly reference for native projects
            return Task.CompletedTask;
        }

        public override void RemoveFile(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var folderPath = Path.GetDirectoryName(path);
            var fullPath = FileSystemUtility.GetFullPath(ProjectFullPath, path);

            bool succeeded;
            succeeded = VCProjectHelper.RemoveFileFromProject(VsProjectAdapter.Project.Object, fullPath, folderPath);
            if (succeeded)
            {
                // The RemoveFileFromProject() method only removes file from project.
                // We want to delete it from disk too.
                FileSystemUtility.DeleteFileAndParentDirectoriesIfEmpty(ProjectFullPath, path, NuGetProjectContext);

                if (!string.IsNullOrEmpty(folderPath))
                {
                    NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_RemovedFileFromFolder, Path.GetFileName(path), folderPath);
                }
                else
                {
                    NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_RemovedFile, Path.GetFileName(path));
                }
            }
        }
    }
}
