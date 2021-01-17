// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.VisualStudio.Telemetry
{
    public sealed class VSPowershellTelemetryConsts
    {
        // PMC, PMUI powershell telemetry consts
        public const string NuGetPMCExecuteCommandCount = nameof(NuGetPMCExecuteCommandCount);
        public const string NuGetPMUIExecuteCommandCount = nameof(NuGetPMUIExecuteCommandCount);
        public const string NuGetCommandUsed = nameof(NuGetCommandUsed);
        public const string InitPs1LoadPMUI = nameof(InitPs1LoadPMUI);
        public const string InitPs1LoadPMC = nameof(InitPs1LoadPMC);
        public const string InitPs1LoadedFromPMCFirst = nameof(InitPs1LoadedFromPMCFirst);
        public const string LoadedFromPMUI = nameof(LoadedFromPMUI);
        public const string FirstTimeLoadedFromPMUI = nameof(FirstTimeLoadedFromPMUI);
        public const string LoadedFromPMC = nameof(LoadedFromPMC);
        public const string FirstTimeLoadedFromPMC = nameof(FirstTimeLoadedFromPMC);
        public const string SolutionLoaded = nameof(SolutionLoaded);
        public const string PowerShellExecuteCommand = nameof(PowerShellExecuteCommand);
        public const string NuGetPowerShellLoaded = nameof(NuGetPowerShellLoaded);

        // PMC UI Console Container telemetry consts
        public const string PackageManagerConsoleWindowsLoad = nameof(PackageManagerConsoleWindowsLoad);
        public const string NuGetPMCWindowLoadCount = nameof(NuGetPMCWindowLoadCount);
        public const string ReOpenAtStart = nameof(ReOpenAtStart);

        // Const name for emitting when VS solution close or VS instance close.
        public const string NuGetPowershellPrefix = nameof(NuGetPowershellPrefix); // Using prefix prevent accidental same name property collission from different type telemetry.
        public const string NuGetVSSolutionClose = nameof(NuGetVSSolutionClose);
        public const string NuGetVSInstanceClose = nameof(NuGetVSInstanceClose);
        public const string SolutionCount = nameof(SolutionCount);
        public const string PMCPowerShellLoadedSolutionCount = nameof(PMCPowerShellLoadedSolutionCount);
        public const string PMUIPowerShellLoadedSolutionCount = nameof(PMUIPowerShellLoadedSolutionCount);
    }
}
