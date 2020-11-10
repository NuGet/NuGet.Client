// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.VisualStudio.Telemetry
{
    public abstract class VsInstanceTelemetryConsts
    {
        // PMC, PMUI powershell telemetry consts
        public const string PowerShellExecuteCommand = "PowerShellExecuteCommand";
        public const string NuGetPMCExecuteCommandCount = "NuGetPMCExecuteCommandCount";
        public const string NuGetPMUIExecuteCommandCount = "NuGetPMUIExecuteCommandCount";
        public const string NuGetPowerShellLoaded = "NuGetPowerShellLoaded";
        public const string LoadFromPMC = "LoadFromPMC";
        public const string NuGetCommandUsed = "NuGetCommandUsed";
        public const string InitPs1Loaded = "InitPs1Loaded";
        public const string LoadedFromPMC = "LoadedFromPMC";
        public const string FirstTimeLoadedFromPMC = "FirstTimeLoadedFromPMC";
        public const string LoadedFromPMUI = "LoadedFromPMUI";
        public const string FirstTimeLoadedFromPMUI = "FirstTimeLoadedFromPMUI";
        public const string SolutionLoaded = "SolutionLoaded";
        // PMC UI Console Container telemetry consts
        public const string PackageManagerConsoleWindowsLoad = "PackageManagerConsoleWindowsLoad";
        public const string NuGetPMCWindowLoadCount = "NuGetPMCWindowLoadCount";
        public const string ReOpenAtStart = "ReOpenAtStart";
        // Const name for emitting when VS solution close or VS instance close.
        public const string NugetPowershellPrefix = "NugetPowershell."; // Using prefix prevent accidental same name property collission from different type telemetry.
        public const string NugetVSSolutionClose = "NugetVSSolutionClose";
        public const string NugetVSInstanceClose = "NugetVSInstanceClose";
        public const string SolutionCount = "SolutionCount";
        public const string PMCPowerShellLoadedSolutionCount = "PMCPowerShellLoadedSolutionCount";
        public const string PMUIPowerShellLoadedSolutionCount = "PMUIPowerShellLoadedSolutionCount";
    }
}
