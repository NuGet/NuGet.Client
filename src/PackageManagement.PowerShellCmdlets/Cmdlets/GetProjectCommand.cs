using EnvDTE;
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
            GetNuGetProject();
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            CheckForSolutionOpen();

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
                    string defaultProjectName = VsSolutionManager.DefaultNuGetProjectName;
                    IEnumerable<Project> projects = DTE.Solution.GetAllProjects();
                    Project defaultProject = projects
                        .Where(p => defaultProjectName.EndsWith(GetDefaultName(projects, p), StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault();
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

        private string GetDefaultName(IEnumerable<Project> projects, Project project)
        {
            string defaultName = IsAmbiguous(projects, project.Name) ? project.GetCustomUniqueName() : project.GetName();
            return defaultName;
        }

        /// <summary>
        /// Determines if a short name is ambiguous
        /// </summary>
        /// <param name="shortName">short name of the project</param>
        /// <returns>true if there are multiple projects with the specified short name.</returns>
        private bool IsAmbiguous(IEnumerable<Project> projects, string shortName)
        {
            IEnumerable<string> projectNames = projects.Select(v => v.Name)
                .Where(p => p.ToLowerInvariant().Contains(shortName.ToLowerInvariant()));
            int count = projectNames.Count();
            return count > 1;
        }
    }
}
