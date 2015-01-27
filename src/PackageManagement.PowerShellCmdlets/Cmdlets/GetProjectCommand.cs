using NuGet.ProjectManagement;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// This cmdlet returns the list of project names in the current solution, 
    /// which is used for tab expansion.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Project", DefaultParameterSetName = ParameterSetByName)]
    [OutputType(typeof(NuGetProject))]
    public class GetProjectCommand : NuGetPowerShellBaseCommand
    {
        private const string ParameterSetByName = "ByName";
        private const string ParameterSetAllProjects = "AllProjects";

        public GetProjectCommand()
            : base()
        {
        }

        [Parameter(Mandatory = false, Position = 0, ParameterSetName = ParameterSetByName, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "PowerShell API requirement")]
        public string[] Name { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetAllProjects)]
        public SwitchParameter All { get; set; }

        protected override void Preprocess()
        {
            base.Preprocess();
            GetNuGetProject();
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            CheckForSolutionOpen();

            if (All.IsPresent)
            {
                IEnumerable<NuGetProject> projects = VsSolutionManager.GetNuGetProjects();
                IEnumerable<PowerShellProject> psProjects = GetPSProjectRepresentation(projects);
                WriteObject(psProjects, enumerateCollection: true);
            }
            else
            {
                PowerShellProject psProject = GetPSProjectRepresentation(Project);
                WriteObject(psProject);
            }
        }

        /// <summary>
        /// Get the list of PowerShell project representation
        /// Used by Get-Project -All command
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        private IEnumerable<PowerShellProject> GetPSProjectRepresentation(IEnumerable<NuGetProject> projects)
        {
            List<PowerShellProject> psProjectList = new List<PowerShellProject>();
            foreach (NuGetProject project in projects)
            {
                PowerShellProject psProj = GetPSProjectRepresentation(project);
                psProjectList.Add(psProj);
            }
            return psProjectList;
        }

        /// <summary>
        /// Get the PowerShell project representation
        /// Used by Get-Project command
        /// </summary>
        /// <param name="project"></param>
        /// <returns></returns>
        private PowerShellProject GetPSProjectRepresentation(NuGetProject project)
        {
            PowerShellProject psProject = new PowerShellProject();
            psProject.ProjectName = project.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
            psProject.TargetFramework = project.GetMetadata<string>(NuGetProjectMetadataKeys.TargetFramework);
            psProject.FullPath = project.GetMetadata<string>(NuGetProjectMetadataKeys.FullPath);
            return psProject;
        }
    }

    /// <summary>
    /// Represent powershell project format
    /// </summary>
    internal class PowerShellProject
    {
        public string ProjectName { get; set; }

        public string TargetFramework { get; set; }

        public string FullPath { get; set; }
    }
}
