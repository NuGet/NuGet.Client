﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// This cmdlet returns the list of project names in the current solution,
    /// which is also used for tab expansion.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Project", DefaultParameterSetName = ParameterSetByName)]
    [OutputType(typeof(EnvDTE.Project))]
    public class GetProjectCommand : NuGetPowerShellBaseCommand
    {
        private const string ParameterSetByName = "ByName";
        private const string ParameterSetAllProjects = "AllProjects";

        [Parameter(Mandatory = false, Position = 0, ParameterSetName = ParameterSetByName, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "PowerShell API requirement")]
        public string[] Name { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetAllProjects)]
        public SwitchParameter All { get; set; }

        /// <summary>
        /// logging time disabled for tab command
        /// </summary>
        protected override bool IsLoggingTimeDisabled => true;

        private void Preprocess()
        {
            CheckSolutionState();
            GetNuGetProject();
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            if (All.IsPresent)
            {
                VsSolutionManager.EnsureSolutionIsLoaded();
                var projects = VsSolutionManager.GetAllVsProjectAdapters().Select(p => p.Project);

                WriteObject(projects, enumerateCollection: true);
            }
            else
            {
                // No name specified; return default project (if not null)
                if (Name == null)
                {
                    var defaultProject = GetDefaultProject();
                    if (defaultProject != null)
                    {
                        WriteObject(defaultProject.Project);
                    }
                }
                else
                {
                    // get all projects matching name(s) - handles wildcards
                    WriteObject(GetProjectsByName(Name).Select(p => p.Project), enumerateCollection: true);
                }
            }
        }
    }
}