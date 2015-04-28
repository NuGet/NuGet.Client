using Microsoft.VisualStudio.Shell;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    public class NativeProjectSystem : CpsProjectSystem
    {
        public NativeProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        public override void AddReference(string referencePath)
        {
            // We disable assembly reference for native projects
        }

        protected override async Task AddFileToProjectAsync(string path)
        {
            if (ExcludeFile(path))
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            // Get the project items for the folder path
            string folderPath = Path.GetDirectoryName(path);
            string fullPath = FileSystemUtility.GetFullPath(ProjectFullPath, path); ;

            VCProjectHelper.AddFileToProject(EnvDTEProject.Object, fullPath, folderPath);

            NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_AddedFileToProject, path, ProjectName);
        }

        public override bool ReferenceExists(string name)
        {
            // We disable assembly reference for native projects
            return true;
        }

        public override void RemoveReference(string name)
        {
            // We disable assembly reference for native projects
        }

        public override void RemoveFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string folderPath = Path.GetDirectoryName(path);
            string fullPath = FileSystemUtility.GetFullPath(ProjectFullPath, path);

            bool succeeded;
            succeeded = VCProjectHelper.RemoveFileFromProject(EnvDTEProject.Object, fullPath, folderPath);
            if (succeeded)
            {
                // The RemoveFileFromProject() method only removes file from project.
                // We want to delete it from disk too.
                FileSystemUtility.DeleteFileAndParentDirectoriesIfEmpty(ProjectFullPath, path, NuGetProjectContext);

                if (!String.IsNullOrEmpty(folderPath))
                {
                    NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemovedFileFromFolder, Path.GetFileName(path), folderPath);
                }
                else
                {
                    NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemovedFile, Path.GetFileName(path));
                }
            }
        }
    }


}
