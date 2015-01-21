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
    public class WindowsStoreProjectSystem : VSMSBuildNuGetProjectSystem
    {
        public WindowsStoreProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        public override bool IsSupportedFile(string path)
        {
            string fileName = Path.GetFileName(path);
            if (fileName.Equals("app.config", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return base.IsSupportedFile(path);
        }
    }
}
