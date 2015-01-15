using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public class WebSiteProjectSystem : VSMSBuildNuGetProjectSystem
    {
        public WebSiteProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to catch all exceptions")]
        public override void AddReference(string referencePath)
        {
            string name = Path.GetFileNameWithoutExtension(referencePath);
            try
            {
                var root = EnvDTEProjectUtility.GetFullPath(EnvDTEProject);
                EnvDTEProjectUtility.GetAssemblyReferences(EnvDTEProject).AddFromFile(PathUtility.GetAbsolutePath(root, referencePath));

                // Always create a refresh file. Vs does this for us in most cases, however for GACed binaries, it resorts to adding a web.config entry instead.
                // This may result in deployment issues. To work around ths, we'll always attempt to add a file to the bin.
                RefreshFileUtility.CreateRefreshFile(root,PathUtility.GetAbsolutePath(EnvDTEProjectUtility.GetFullPath(EnvDTEProject), referencePath));

                NuGetProjectContext.Log(MessageLevel.Debug, VsResources.Debug_AddReference, name, ProjectName);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, VsResources.FailedToAddReference, name), e);
            }
        }

        protected override void AddGacReference(string name)
        {
            EnvDTEProjectUtility.GetAssemblyReferences(EnvDTEProject).AddFromGAC(name);
        }
    }
}
