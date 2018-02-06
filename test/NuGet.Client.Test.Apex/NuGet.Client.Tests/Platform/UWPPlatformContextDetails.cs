using System;
using System.Runtime.Versioning;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGetClient.Test.Foundation.TestAttributes.Context;

namespace NuGetClient.Test.Integration.Platform
{
    internal class UWPPlatformContextDetails : WindowsImmersivePlatformContextDetails
    {
        protected override ProjectTemplate DefaultProjectTemplate { get { return ProjectTemplate.UAPBlankApplication; } }

        public UWPPlatformContextDetails(Context context) : base(context)
        {
        }

        public override SolutionService OpenSolution(VisualStudioHost host, string pathToSolution)
        {
            // Update project to predefined platform version or the latest installed version if the target version isn't installed
            UWPProjectTargetPlatformVersionDetails.UpdateSolutionTargetVersions(pathToSolution);

            return base.OpenSolution(host, pathToSolution);
        }

        public override ProjectTestExtension CreateProject(VisualStudioHost host, string projectName, ProjectTemplate projectTemplate, bool addToExistingSolution = false, CodeLanguage codeLanguage = CodeLanguage.UnspecifiedLanguage)
        {
            if (projectTemplate == default(ProjectTemplate))
            {
                projectTemplate = this.DefaultProjectTemplate;
            }

            ProjectLanguage projectLanguage = codeLanguage == CodeLanguage.UnspecifiedLanguage ? this.ProjectLanguage : codeLanguage.AsProjectLanguage();
            UWPProjectTestExtension project = addToExistingSolution ?
                host.ObjectModel.Solution.AddProject<UWPProjectTestExtension>(projectLanguage, projectTemplate, this.FrameworkName, projectName) :
                host.ObjectModel.Solution.CreateProject<UWPProjectTestExtension>(projectLanguage, projectTemplate, this.FrameworkName, projectName);

            // Make sure to save any changes the wizards make to the project file
            project.Save();

            // Update project to predefined platform version or the latest installed version if the target version isn't installed
            UWPProjectTargetPlatformVersionDetails.UpdateProjectTargetVersions(project);

            return project;
        }

        protected override void RegisterPlatformContextCapabilities()
        {
            base.RegisterPlatformContextCapabilities();
        }

        protected override FrameworkName ConvertPlatformVersionToFrameworkName(PlatformVersion platformVersion)
        {
            FrameworkName frameworkName = null;
            if (this.CodeLanguage != CodeLanguage.CPP)
            {
                frameworkName = new FrameworkName(".NETCore", new Version(4, 5, 3));
            }
            return frameworkName;
        }
    }
}
