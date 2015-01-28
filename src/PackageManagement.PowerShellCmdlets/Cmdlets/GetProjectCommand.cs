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
        private DTE _dte;

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
            _dte = (DTE)GetPropertyValueFromHost("DTE");
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            CheckForSolutionOpen();

            if (All.IsPresent)
            {
                var projects = _dte.Solution.GetAllProjects();
                WriteObject(projects, enumerateCollection: true);
            }
            else
            {
                // No name specified; return default project (if not null)
                if (Name == null)
                {
                    string defaultProjectName = VsSolutionManager.DefaultNuGetProjectName;
                    Project defaultProject = _dte.Solution.GetAllProjects()
                        .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Name, defaultProjectName))
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
        
        /// <summary>
        /// Return all projects in the solution matching the provided names. Wildcards are supported.
        /// This method will automatically generate error records for non-wildcarded project names that
        /// are not found.
        /// </summary>
        /// <param name="projectNames">An array of project names that may or may not include wildcards.</param>
        /// <returns>Projects matching the project name(s) provided.</returns>
        protected IEnumerable<Project> GetProjectsByName(string[] projectNames)
        {
            var allValidProjectNames = GetAllValidProjectNames().ToList();

            foreach (string projectName in projectNames)
            {
                // if ctrl+c hit, leave immediately
                if (Stopping)
                {
                    break;
                }

                // Treat every name as a wildcard; results in simpler code
                var pattern = new WildcardPattern(projectName, WildcardOptions.IgnoreCase);

                var matches = from s in allValidProjectNames
                              where pattern.IsMatch(s)
                              select _dte.Solution.GetAllProjects()
                              .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Name, s) || StringComparer.OrdinalIgnoreCase.Equals(p.FullName, s))
                              .FirstOrDefault();

                int count = 0;
                foreach (var project in matches)
                {
                    count++;
                    yield return project;
                }

                // We only emit non-terminating error record if a non-wildcarded name was not found.
                // This is consistent with built-in cmdlets that support wildcarded search.
                // A search with a wildcard that returns nothing should not be considered an error.
                if ((count == 0) && !WildcardPattern.ContainsWildcardCharacters(projectName))
                {
                    ErrorHandler.WriteProjectNotFoundError(projectName, terminating: false);
                }
            }
        }

        /// <summary>
        /// Return all possibly valid project names in the current solution. This includes all
        /// unique names and safe names.
        /// </summary>
        /// <returns></returns>
        protected IEnumerable<string> GetAllValidProjectNames()
        {
            var safeNames = _dte.Solution.GetAllProjects().Select(p => p.Name);
            return safeNames;
        }
    }
}
