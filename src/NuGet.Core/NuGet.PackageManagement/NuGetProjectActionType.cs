﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Enum for the type of NuGetProjectAction
    /// </summary>
    public enum NuGetProjectActionType
    {
        /// <summary>
        /// Install
        /// </summary>
        Install,

        /// <summary>
        /// Uninstall
        /// </summary>
        Uninstall,

        /// <summary>
        /// Update
        /// </summary>
        Update,

        /// <summary>
        /// PreferUpdateToInstall - Install if project doesn't have package. Update if project does have it.
        /// </summary>
        PreferUpdateToInstall
    }
}
