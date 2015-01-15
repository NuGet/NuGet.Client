using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;
using EnvDTEProjectItem = EnvDTE.ProjectItem;
using EnvDTEProjectItems = EnvDTE.ProjectItems;

namespace NuGet.PackageManagement.VisualStudio
{
    public class JsProjectSystem: CpsProjectSystem
    {
        public JsProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }
       
        public override void AddFile(string path, Stream stream)
        {
            // ensure the parent folder is created before adding file to the project            
            EnvDTEProjectUtility.GetProjectItems(EnvDTEProject,Path.GetDirectoryName(path), createIfNotExists: true);
            base.AddFile(path, stream);
        }

        public override void AddFile(string path, System.Action<Stream> writeToStream)
        {
            // ensure the parent folder is created before adding file to the project            
            EnvDTEProjectUtility.GetProjectItems(EnvDTEProject, Path.GetDirectoryName(path), createIfNotExists: true);
            base.AddFile(path, writeToStream);
        }

        protected override void AddFileToProject(string path)
        {
            if (ExcludeFile(path))
            {
                return;
            }

            string folderPath = Path.GetDirectoryName(path);
            string fullPath = FileSystemUtility.GetFullPath(EnvDTEProjectUtility.GetFullPath(EnvDTEProject),path);

            // Add the file to project or folder
            EnvDTEProjectItems container = EnvDTEProjectUtility.GetProjectItems(EnvDTEProject,folderPath, createIfNotExists: true);
            if (container == null)
            {
                throw new ArgumentException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        VsResources.Error_FailedToCreateParentFolder,
                        path,
                        ProjectName));
            }
            AddFileToContainer(fullPath, folderPath, container);

            NuGetProjectContext.Log(MessageLevel.Debug, VsResources.Debug_AddedFileToProject, path, ProjectName);
        }
    }


}
