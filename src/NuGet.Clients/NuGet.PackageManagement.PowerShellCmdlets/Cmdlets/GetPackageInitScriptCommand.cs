// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsCommon.Get, "PackageInitScript", DefaultParameterSetName = "Packages")]
    [OutputType(typeof(PowerShellPackageInitScript))]
    public sealed class GetPackageInitScriptCommand : NuGetPowerShellBaseCommand
    {
        [Parameter(ParameterSetName = "Packages")]
        [ValidateNotNullOrEmpty]
        public string[]? Packages { get; set; }

        protected override bool IsLoggingTimeDisabled => true;

        protected override void ProcessRecordCore()
        {
            CheckSolutionState();
            var scripts = PackageInitScriptHelper.GetScripts(VsSolutionManager, ConfigSettings, PackageManager, Token);
            WriteObject(PackageInitScriptHelper.GetMatchingScripts(scripts, Packages, WriteError), enumerateCollection: true);
        }
    }
}
