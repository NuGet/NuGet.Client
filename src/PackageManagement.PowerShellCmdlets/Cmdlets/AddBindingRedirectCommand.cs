using NuGet.Client;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Runtime.CompilerServices;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsCommon.Add, "BindingRedirect")]
    //[OutputType(typeof(AssemblyBinding))]
    public class AddBindingRedirectCommand : NuGetPowerShellBaseCommand
    {
        //private readonly IFileSystemProvider _fileSystemProvider;
        //private readonly IVsFrameworkMultiTargeting _frameworkMultiTargeting;

        public AddBindingRedirectCommand()
            : base()
        {
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
            CheckForSolutionOpen();

            Preprocess();

            var projects = new List<NuGetProject>();

            // if no project specified, use default
            if (ProjectName == null)
            {
                NuGetProject project = VsSolutionManager.DefaultNuGetProject;

                // if no default project (empty solution), throw terminating
                if (project == null)
                {
                    ErrorHandler.ThrowNoCompatibleProjectsTerminatingError();
                }

                projects.Add(project);
            }
            else
            {
                // get matching projects, expanding wildcards
                projects.AddRange(VsSolutionManager.GetNuGetProjects());
            }

            // Create a new app domain so we don't load the assemblies into the host app domain
            AppDomain domain = AppDomain.CreateDomain("domain");

            try
            {
                foreach (NuGetProject project in projects)
                {
                    // TODO: Find AddBindingRedirects API
                    //var redirects = RuntimeHelpers.AddBindingRedirects(project, _fileSystemProvider, domain, _frameworkMultiTargeting);

                    // Print out what we did
                    //WriteObject(redirects, enumerateCollection: true);
                }
            }
            finally
            {
                AppDomain.Unload(domain);
            }
        }
    }
}
