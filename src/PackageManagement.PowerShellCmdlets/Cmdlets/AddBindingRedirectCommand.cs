using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

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
            Preprocess();

            CheckForSolutionOpen();

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
                projects.AddRange(GetNuGetProjectsByName(ProjectName));
            }

            foreach (NuGetProject project in projects)
            {
                string projectName = project.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
                try
                {
                    // App domain loading and unloading is handled at the RuntimeHelpers class.
                    MSBuildNuGetProject msbuildProject = project as MSBuildNuGetProject;
                    if (msbuildProject != null)
                    {
                        msbuildProject.AddBindingRedirects();
                        LogCore(MessageLevel.Info, string.Format(Resources.Cmdlets_AddedBindingRedirects, projectName));
                    }
                    else
                    {
                        LogCore(MessageLevel.Error, Resources.Cmdlets_NotSupportBindingRedirects);
                    }
                }
                catch (Exception ex)
                {
                    LogCore(MessageLevel.Error, string.Format(Resources.Cmdlets_FailedBindingRedirects, projectName, ex.Message));
                }
            }
        }
    }
}
