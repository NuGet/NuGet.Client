using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public class WixProjectSystem : VSMSBuildNuGetProjectSystem
    {
        public WixProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        public override void AddReference(string referencePath)
        {
            // References aren't allowed for WiX projects
        }

        protected override void AddGacReference(string name)
        {
            // GAC references aren't allowed for WiX projects
        }
    }
}
