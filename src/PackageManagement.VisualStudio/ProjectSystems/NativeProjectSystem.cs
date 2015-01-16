using Microsoft.VisualStudio.Shell;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;


namespace NuGet.PackageManagement.VisualStudio
{
    public class NativeProjectSystem: CpsProjectSystem
    {
        public NativeProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        public override void AddReference(string referencePath)
        {
            // We disable assembly reference for native projects
        }

        protected override void AddFileToProject(string path)
        {
            if (ExcludeFile(path))
            {
                return;
            }

            // Get the project items for the folder path
            string folderPath = Path.GetDirectoryName(path);
            string fullPath = FileSystemUtility.GetFullPath(EnvDTEProjectUtility.GetFullPath(EnvDTEProject),path);;

            ThreadHelper.Generic.Invoke(() =>
            {
                VCProjectHelper.AddFileToProject(EnvDTEProject.Object, fullPath, folderPath);   
            });

            NuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_AddedFileToProject, path, ProjectName);
        }
    }


}
