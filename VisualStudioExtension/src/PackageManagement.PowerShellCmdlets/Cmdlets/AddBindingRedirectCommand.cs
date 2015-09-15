// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsCommon.Add, "BindingRedirect")]
    [OutputType(typeof(AssemblyBinding))]
    public class AddBindingRedirectCommand : NuGetPowerShellBaseCommand
    {
        private readonly IVsFrameworkMultiTargeting _frameworkMultiTargeting;

        public AddBindingRedirectCommand()
        {
            _frameworkMultiTargeting = ServiceLocator.GetGlobalService<SVsFrameworkMultiTargeting, IVsFrameworkMultiTargeting>();
        }

        [Parameter(Position = 0, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "PowerShell API requirement")]
        public string[] ProjectName { get; set; }

        protected override void ProcessRecordCore()
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    CheckSolutionState();

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
                            var redirects = await RuntimeHelpers.AddBindingRedirectsAsync(VsSolutionManager, project, domain, projectAssembliesCache, _frameworkMultiTargeting, this);

                            // Print out what we did
                            WriteObject(redirects, enumerateCollection: true);
                        }
                    }
                    finally
                    {
                        AppDomain.Unload(domain);
                    }
                });
        }
    }
}