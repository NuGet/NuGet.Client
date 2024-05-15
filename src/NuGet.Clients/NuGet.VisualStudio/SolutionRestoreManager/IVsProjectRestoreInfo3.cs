// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Contains project metadata needed for project restore operation.
    /// </summary>
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
        IReadOnlyList<IVsTargetFrameworkInfo4> TargetFrameworks { get; }

        /// <summary>
        /// Collection of tool references.
        /// </summary>
        IReadOnlyList<IVsReferenceItem2> ToolReferences { get; }

        /// <summary>
        /// Original raw value of TargetFrameworks property as set in a project file.
        /// </summary>
        string OriginalTargetFrameworks { get; }
    }
}
