using EnvDTE;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// This cmdlet returns the list of project names in the current solution, 
    /// which is used for tab expansion.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Project", DefaultParameterSetName = ParameterSetByName)]
    [OutputType(typeof(Project))]
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
            CheckForSolutionOpen();
            GetNuGetProject();
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            if (All.IsPresent)
            {
                var projects = DTE.Solution.GetAllProjects();
                WriteObject(projects, enumerateCollection: true);
            }
            else
            {
                // No name specified; return default project (if not null)
                if (Name == null)
                {
                    Project defaultProject = GetDefaultProject();
                    if (defaultProject != null)
                    {
                        WriteObject(defaultProject);
                    }
                }
                else
                {
                    // get all projects matching name(s) - handles wildcards
                    WriteObject(GetProjectsByName(Name), enumerateCollection: true);
                }
            }
        }
    }
}
