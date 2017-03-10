// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Contains methods to migrate a UWP project to PackageReference based project.
    /// </summary>
    [ComImport]
    [Guid("2CB0AF9B-241D-4201-99ED-00F796C416BB")]
    public interface IVsProjectJsonMigrator
    {
        /// <summary>
        /// Migrates a UWP Project.json based project to Package Reference based project.
        /// </summary>
        /// <param name="projectUniqueName">The full path to the project that needs to be migrated</param>
        IVsProjectJsonMigrateResult MigrateProjectToPackageRef(string projectUniqueName);
        
    }
}
