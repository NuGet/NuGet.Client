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
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            CheckForSolutionOpen();

            if (All.IsPresent)
            {
                IEnumerable<NuGetProject> projects = VsSolutionManager.GetNuGetProjects();
                WriteObject(projects, enumerateCollection: true);
            }
            else
            {
                WriteObject(Project);
            }
        }
    }
}
