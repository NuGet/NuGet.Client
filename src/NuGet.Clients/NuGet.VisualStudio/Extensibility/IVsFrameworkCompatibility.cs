﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Contains methods to discover frameworks and compatibility between frameworks.
    /// </summary>
    [ComImport]
    [Guid("3B742C14-3DCB-463D-9198-F0C004BF65DD")]
    public interface IVsFrameworkCompatibility
    {
        /// <summary>
        /// Gets all .NETStandard frameworks currently supported, in ascending order by version.
        /// </summary>
        IEnumerable<FrameworkName> GetNetStandardFrameworks();

        /// <summary>
        /// Gets frameworks that support packages of the provided .NETStandard version.
        /// </summary>
        /// <remarks>
        /// The result list is not exhaustive as it is meant to human-readable. For example,
        /// equivalent frameworks are not returned. Additionally, a framework name with version X
        /// in the result implies that framework names with versions greater than or equal to X
        /// but having the same <see cref="FrameworkName.Identifier"/> are also supported.
        /// </remarks>
        /// <param name="frameworkName">The .NETStandard version to get supporting frameworks for.</param>
        IEnumerable<FrameworkName> GetFrameworksSupportingNetStandard(FrameworkName frameworkName);

        /// <summary>
        /// Selects the framework from <paramref name="frameworks"/> that is nearest
        /// to the <paramref name="targetFramework"/>, according to NuGet's framework
        /// compatibility rules. <c>null</c> is returned of none of the frameworks
        /// are compatible.
        /// </summary>
        /// <param name="targetFramework">The target framework.</param>
        /// <param name="frameworks">The list of frameworks to choose from.</param>
        /// <exception cref="ArgumentException">If any of the arguments are <c>null</c>.</exception>
        /// <returns>The nearest framework.</returns>
        FrameworkName GetNearest(FrameworkName targetFramework, IEnumerable<FrameworkName> frameworks);
    }
}
