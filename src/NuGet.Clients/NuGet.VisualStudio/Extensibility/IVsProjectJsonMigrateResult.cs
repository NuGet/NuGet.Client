// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Contains the result of the migrate operation on a UWP project
    /// </summary>
    [ComImport]
    [Guid("18245DEA-7AB6-4294-BBA2-0164EA0D8D30")]
    public interface IVsProjectJsonMigrateResult
    {
        /// <summary>
        /// Returns the success value of the migration operation.
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// If migrate operation was successful, stores the path to the backup project file on disk.
        /// </summary>
        string BackupProjectFile { get; }

        /// <summary>
        /// If migrate operation was successful, stores the path to the backup project.json file on disk.
        /// </summary>
        string BackupProjectJsonFile { get; }

        /// <summary>
        /// If migrate operation was unsuccessful, stores the error message in the exception.
        /// </summary>
        string ErrorMessage { get; }
    }
}
