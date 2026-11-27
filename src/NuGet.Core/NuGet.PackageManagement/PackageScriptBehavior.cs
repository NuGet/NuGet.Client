// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Controls script execution for a package operation.
    /// </summary>
    public enum PackageScriptBehavior
    {
        /// <summary>Stops the operation if an applicable uninstall.ps1 is present.</summary>
        Fail,
        /// <summary>Continues the operation without running uninstall.ps1.</summary>
        Skip,
        /// <summary>Runs uninstall.ps1 without implicitly running init.ps1.</summary>
        Run,
        /// <summary>Runs uninstall.ps1 without implicitly running init.ps1, like Run during uninstall.</summary>
        RunWithInit
    }
}
