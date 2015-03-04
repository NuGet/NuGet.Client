using System;
using System.IO;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public class WebProjectSystem : VSMSBuildNuGetProjectSystem
    {
        public WebProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        public override bool IsSupportedFile(string path)
        {
            string fileName = Path.GetFileName(path);
            return !(fileName.StartsWith("app.", StringComparison.OrdinalIgnoreCase) &&
                     fileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase));
        }
    }
}
