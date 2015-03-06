using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.VisualStudio;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsCommon.Add, "BindingRedirect")]
    [OutputType(typeof(AssemblyBinding))]
    public class AddBindingRedirectCommand : NuGetPowerShellBaseCommand
    {
        private readonly IVsFrameworkMultiTargeting _frameworkMultiTargeting;

        public AddBindingRedirectCommand()
            : base()
        {
            _frameworkMultiTargeting = ServiceLocator.GetGlobalService<SVsFrameworkMultiTargeting, IVsFrameworkMultiTargeting>();
        }

        [Parameter(Position = 0, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "PowerShell API requirement")]
        public string[] ProjectName { get; set; }

        protected override void Preprocess()
        {
            base.Preprocess();
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            CheckForSolutionOpen();

            var projects = new List<Project>();

            // if no project specified, use default
            if (ProjectName == null)
            {
                Project defaultProject = GetDefaultProject();

                // if no default project (empty solution), throw terminating
                if (defaultProject == null)
                {
                    ErrorHandler.ThrowNoCompatibleProjectsTerminatingError();
                }

                projects.Add(defaultProject);
            }
            else
            {
                // get matching projects, expanding wildcards
                projects.AddRange(GetProjectsByName(ProjectName));
            }

            // Create a new app domain so we don't load the assemblies into the host app domain
            AppDomain domain = AppDomain.CreateDomain("domain");

            try
            {
                foreach (Project project in projects)
                {
                    var projectAssembliesCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    var redirects = RuntimeHelpers.AddBindingRedirects(VsSolutionManager, project, domain, projectAssembliesCache, _frameworkMultiTargeting, this);

                    // Print out what we did
                    WriteObject(redirects, enumerateCollection: true);
                }
            }
            finally
            {
                AppDomain.Unload(domain);
            }
        }
    }
}
