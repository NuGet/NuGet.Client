// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Contains project metadata needed for project restore operation.
    /// </summary>
    [ComImport]
    [Guid("1B74A7B4-AA7C-4AE1-9A8D-3A7AEDA9C594")]
    public interface IVsProjectRestoreInfo3
    {
        /// <summary>
        /// The MSBuildProjectExtensionsPath of the project (originally BaseIntermediateOutputPath was used,
        /// but rather than create a new interface, we changed the meaning of this property).
        /// </summary>
        string BaseIntermediatePath { get; }

        /// <summary>
        /// Target frameworks metadata.
        /// </summary>
        IVsTargetFrameworks3 TargetFrameworks { get; }

        /// <summary>
        /// Collection of tool references.
        /// </summary>
        IVsReferenceItems ToolReferences { get; }

        /// <summary>
        /// Original raw value of TargetFrameworks property as set in a project file.
        /// </summary>
        string OriginalTargetFrameworks { get; }
    }
}
